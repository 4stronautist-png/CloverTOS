using System;
using System.Linq;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.World;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.CombatEntities.Components;

namespace Melia.Zone.Skills.Handlers.Archers.Wugushi
{
	/// <summary>
	/// Handler for the skill Crescendo Bane.
	/// </summary>
	[SkillHandler(SkillId.Wugushi_CrescendoBane)]
	public class Wugushi_CrescendoBane : IGroundSkillHandler
	{
		private const float BaseSplashRadius = 50f;
		private const int MaxTargets = 15;

		/// <summary>
		/// Handles skill, applying a buff to the caster.
		/// </summary>
		/// <param name="skill"></param>
		/// <param name="caster"></param>
		/// <param name="originPos"></param>
		/// <param name="target"></param>
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

			Send.ZC_SKILL_READY(caster, skill, caster.Position, caster.Position);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, caster.Handle, caster.Position, caster.Direction, Position.Zero);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, caster.Position, null);

			var splashRadius = WugushiSkillHelper.GetCrescendoBaneRadius(caster, skill.Level);
			Send.ZC_GROUND_EFFECT(caster, caster.Position, "F_archer_crescendobane_ground", Math.Max(0.05f, splashRadius / BaseSplashRadius), 1f);

			var enemiesInRange = caster.Map.GetAttackableEnemiesInPosition(caster, caster.Position, splashRadius);
			foreach (var enemy in enemiesInRange.Take(MaxTargets))
				this.CondensePoisonDebuffs(caster, enemy);
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

				DamageOverTimeBuffHandler.CondenseRemainingTicks(buff, 0.5f, 0.25f);
			}
		}
	}
}
