using System;
using Melia.Shared.Packages;
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
	[Package("laima")]
	[SkillHandler(SkillId.Wugushi_WugongGu)]
	public class Wugushi_WugongGuOverride : IForceSkillHandler
	{
		private static readonly TimeSpan PoisonDuration = TimeSpan.FromSeconds(10);

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (target == null)
			{
				Send.ZC_SKILL_FORCE_TARGET(caster, null, skill);
				return;
			}

			if (!caster.InSkillUseRange(skill, target))
			{
				caster.ServerMessage(Localization.Get("Too far away."));
				Send.ZC_SKILL_FORCE_TARGET(caster, null, skill);
				return;
			}

			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.TurnTowards(target);
			WugushiSkillHelper.ApplyPoisonMasteryIndicator(caster);

			var skillHitResult = SCR_SkillHit(caster, target, skill);
			target.TakeDamage(skillHitResult.Damage, caster);

			var skillHit = new SkillHitInfo(caster, target, skill, skillHitResult, TimeSpan.Zero, TimeSpan.Zero);

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
