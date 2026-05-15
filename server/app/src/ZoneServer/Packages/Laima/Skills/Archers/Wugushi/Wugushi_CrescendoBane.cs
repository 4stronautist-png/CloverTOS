using System;
using System.Linq;
using System.Threading.Tasks;
using Melia.Shared.Packages;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.World;
using Melia.Zone.Buffs;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.CombatEntities.Components;
using static Melia.Zone.Skills.SkillUseFunctions;
using static Melia.Zone.Skills.Helpers.SkillDamageHelper;

namespace Melia.Zone.Skills.Handlers.Archers.Wugushi
{
	/// <summary>
	/// Handler for the Wugushi skill Crescendo Bane.
	/// Condenses poison damage over time on enemies in range.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Wugushi_CrescendoBane)]
	public class Wugushi_CrescendoBaneOverride : IGroundSkillHandler
	{
		private const float BaseSplashRadius = 50f;
		private const int MaxTargets = 15;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);
			WugushiSkillHelper.ApplyPoisonMasteryIndicator(caster);

			var targetHandle = target?.Handle ?? 0;
			Send.ZC_SKILL_READY(caster, skill, 1, originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, targetHandle, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos, ForceId.GetNew(), null);

			skill.Run(this.HandleSkill(caster, skill, originPos, farPos));
		}

		private async Task HandleSkill(ICombatEntity caster, Skill skill, Position originPos, Position farPos)
		{
			await skill.Wait(TimeSpan.FromMilliseconds(50));

			var splashRadius = this.GetSplashRadius(caster, skill);
			var effectScale = Math.Max(0.05f, splashRadius / BaseSplashRadius);

			Send.ZC_GROUND_EFFECT(caster, caster.Position, "F_archer_crescendobane_ground", effectScale, 1f);

			var enemiesInRange = caster.Map.GetAttackableEnemiesInPosition(caster, caster.Position, splashRadius);

			foreach (var target in enemiesInRange.Take(MaxTargets))
			{
				this.CondensePoisonDebuffs(caster, target);
			}
		}

		private void CondensePoisonDebuffs(ICombatEntity caster, ICombatEntity target)
		{
			var buffs = target.Components.Get<BuffComponent>();
			if (buffs == null)
				return;

			var poisonDebuffs = buffs.GetAll(b => b.Data.Tags.HasAny(BuffTag.Poison));

			foreach (var buff in poisonDebuffs)
			{
				if (buff.Caster != caster)
					continue;

				DamageOverTimeBuffHandler.CondenseRemainingTicks(buff, 0.5f, 0.5f);
			}
		}

		private float GetSplashRadius(ICombatEntity caster, Skill skill)
		{
			return WugushiSkillHelper.GetCrescendoBaneRadius(caster, skill.Level);
		}
	}
}
