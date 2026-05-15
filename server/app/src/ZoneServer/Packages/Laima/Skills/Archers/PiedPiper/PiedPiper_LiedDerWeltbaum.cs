using System;
using System.Collections.Generic;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Archers.PiedPiper
{
	[Package("laima")]
	[SkillHandler(SkillId.PiedPiper_LiedDerWeltbaum)]
	public class PiedPiperLiedDerWeltbaum : IGroundSkillHandler, IMeleeGroundSkillHandler
	{
		private const int BaseDurationSeconds = 10;
		private const int BaseNoDamageCount = 3;
		private const float DamageDealtBonus = 1.0f;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
			=> this.Execute(skill, caster, originPos, farPos);

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, IList<ICombatEntity> targets)
			=> this.Execute(skill, caster, originPos, farPos);

		private void Execute(Skill skill, ICombatEntity caster, Position originPos, Position farPos)
		{
			if (!PiedPiperSkillHelper.TryStart(skill, caster, originPos, farPos))
				return;

			PiedPiperSkillHelper.SafePlaySound(caster, "skl_eff_liedderweltbaum");

			var duration = BaseDurationSeconds + (caster.TryGetActiveAbilityLevel(AbilityId.PiedPiper15, out var durationLevel) ? durationLevel : 0);
			var noDamageCount = BaseNoDamageCount + (caster.TryGetActiveAbilityLevel(AbilityId.PiedPiper14, out var countLevel) ? countLevel : 0);
			foreach (var ally in PiedPiperSkillHelper.GetPartyTargets(caster))
			{
				PiedPiperSkillHelper.SafePlayEffect(ally, "F_buff_LiedDerWeltbaum", 0.7f);
				PiedPiperSkillHelper.StartVisibleBuff(ally, BuffId.LiedDerWeltbaum_Buff, skill.Level, DamageDealtBonus, TimeSpan.FromSeconds(duration), caster, skill.Id);
				var noDamage = PiedPiperSkillHelper.StartVisibleBuff(ally, BuffId.LiedDerWeltbaum_NoDamage_Buff, skill.Level, noDamageCount, TimeSpan.FromSeconds(duration), caster, skill.Id);
				if (noDamage != null)
				{
					noDamage.OverbuffCounter = noDamageCount;
					noDamage.NotifyUpdate();
				}
			}
		}
	}
}
