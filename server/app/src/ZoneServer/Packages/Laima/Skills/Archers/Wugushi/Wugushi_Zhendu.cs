using System;
using Melia.Shared.Packages;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Skills.Handlers.Archers.Wugushi
{
	/// <summary>
	/// Handler for the Wugushi skill Zhendu.
	/// Applies Zhendu buff to caster and party members.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Wugushi_Zhendu)]
	public class Wugushi_ZhenduOverride : ISelfSkillHandler
	{
		private const float BuffRange = 300f;
		private const int BuffDurationSeconds = 300;
		private const float PoisonResistanceDebuffRange = 150f;
		private static readonly TimeSpan PoisonResistanceDebuffDuration = TimeSpan.FromSeconds(8);

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (!caster.TrySpendSp(this.GetSpendSp(caster, skill)))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);
			WugushiSkillHelper.ApplyPoisonMasteryIndicator(caster);

			Send.ZC_SKILL_READY(caster, skill, 1, originPos, Position.Zero);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, originPos, dir, Position.Zero);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, Position.Zero, ForceId.GetNew(), null);

			caster.StartBuff(BuffId.Zhendu_Buff, skill.Level, 0f, TimeSpan.FromSeconds(BuffDurationSeconds), caster);

			if (caster is Character character)
			{
				var party = character.Connection.Party;
				if (party != null)
				{
					var members = caster.Map.GetPartyMembersInRange(character, BuffRange, true);
					foreach (var member in members)
					{
						if (member == caster)
							continue;
						member.StartBuff(BuffId.Zhendu_Buff, skill.Level, 0f, TimeSpan.FromSeconds(BuffDurationSeconds), caster);
					}
				}
			}

			this.ApplyDecreasedPoisonResistance(caster);
		}

		private float GetSpendSp(ICombatEntity caster, Skill skill)
		{
			var spendSp = skill.Properties.GetFloat(PropertyName.SpendSP);
			if (caster.IsAbilityActive(AbilityId.Wugushi7))
				spendSp *= 1.5f;

			return spendSp;
		}

		private void ApplyDecreasedPoisonResistance(ICombatEntity caster)
		{
			if (!caster.TryGetActiveAbilityLevel(AbilityId.Wugushi7, out var abilityLevel))
				return;

			var targets = caster.Map.GetAttackableEnemiesIn(caster, new Circle(caster.Position, PoisonResistanceDebuffRange));
			foreach (var target in targets)
				target.StartBuff(BuffId.Zhendu_Debuff, abilityLevel, 0f, PoisonResistanceDebuffDuration, caster, SkillId.Wugushi_Zhendu);
		}
	}
}
