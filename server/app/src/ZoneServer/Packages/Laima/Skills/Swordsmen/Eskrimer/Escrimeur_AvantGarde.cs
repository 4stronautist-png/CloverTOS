using System;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Swordsmen.Eskrimer
{
	[Package("laima")]
	[SkillHandler(SkillId.Escrimeur_AvantGarde)]
	public class Escrimeur_AvantGarde : ISelfSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (!EskrimerSkillHelper.TryBeginSelfSkill(skill, caster, originPos))
				return;

			ApplyBuff(skill, caster);
			EskrimerSkillHelper.KeepAttackPosture(caster);
		}

		public void StartDynamicCast(Skill skill, ICombatEntity caster, float maxCastTime)
		{
			ApplyBuff(skill, caster);
			EskrimerSkillHelper.KeepAttackPosture(caster);
		}

		public void EndDynamicCast(Skill skill, ICombatEntity caster, float maxCastTime)
		{
			ApplyBuff(skill, caster);
			EskrimerSkillHelper.KeepAttackPosture(caster);
		}

		private static void ApplyBuff(Skill skill, ICombatEntity caster)
		{
			caster.StartBuff(BuffId.AdvantGarde_Buff, skill.Level, EskrimerSkillHelper.GetAvantGardeFinalDamageBonus(skill), TimeSpan.FromMinutes(30), caster, skill.Id);
		}
	}
}
