using System;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Archers.Wugushi
{
	/// <summary>
	/// Handler for the Wugushi skill Wugong Gu.
	/// </summary>
	[SkillHandler(SkillId.Wugushi_WugongGu)]
	public class Wugushi_WugongGu : ITargetSkillHandler
	{
		private static readonly TimeSpan PoisonDuration = TimeSpan.FromSeconds(10);

		/// <summary>
		/// Handles skill, damages targets and apply a debuff.
		/// </summary>
		/// <param name="skill"></param>
		/// <param name="caster"></param>
		/// <param name="target"></param>
		public void Handle(Skill skill, ICombatEntity caster, ICombatEntity target)
		{
			if (target == null)
			{
				// TODO: Skill_42 not implemented
				//Send.ZC_NORMAL.Skill_42(caster, skill.Id, caster.Direction, ForceId.GetNew());
				Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, caster.Position, caster.Direction, Position.Zero);
				Send.ZC_SKILL_FORCE_TARGET(caster, null, skill, null);
				// TODO: Skill_43 not implemented
				//Send.ZC_NORMAL.Skill_43(caster);
				return;
			}

			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			WugushiSkillHelper.ApplyPoisonMasteryIndicator(caster);
			caster.TurnTowards(target.Position);

			var aniTime = TimeSpan.Zero;
			var skillHitResult = SCR_SkillHit(caster, target, skill);

			target.TakeDamage(skillHitResult.Damage, caster);

			var skillHit = new SkillHitInfo(caster, target, skill, skillHitResult, aniTime, TimeSpan.Zero);

			Send.ZC_SKILL_FORCE_TARGET(caster, target, skill, skillHit);

			if (skillHitResult.Damage <= 0)
				return;

			var poison = target.StartBuff(BuffId.Virus_Debuff, skill.Level, skillHitResult.Damage, PoisonDuration, caster, skill.Id);
			if (poison != null)
			{
				poison.SetUpdateTime(500);
				poison.IncreaseDuration(PoisonDuration);
				poison.NotifyUpdate();
			}
		}
	}
}
