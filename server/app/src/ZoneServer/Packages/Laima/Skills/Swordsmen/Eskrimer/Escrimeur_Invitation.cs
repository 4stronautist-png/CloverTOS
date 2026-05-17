using System;
using System.Threading.Tasks;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Swordsmen.Eskrimer
{
	[Package("laima")]
	[SkillHandler(SkillId.Escrimeur_Invitation)]
	public class Escrimeur_Invitation : ISelfSkillHandler, IDynamicCasted
	{
		public void StartDynamicCast(Skill skill, ICombatEntity caster, float maxCastTime)
		{
			EskrimerSkillHelper.KeepAttackPosture(caster);
			caster.StartBuff(BuffId.Invitation_Buff, skill.Level, 0f, TimeSpan.FromSeconds(2), caster, skill.Id);
		}

		public void EndDynamicCast(Skill skill, ICombatEntity caster, float maxCastTime)
		{
			caster.StopBuff(BuffId.Invitation_Buff);
			caster.StartBuff(BuffId.Pret_Buff, skill.Level, EskrimerSkillHelper.PretPasataFinalDamageCap, TimeSpan.FromSeconds(10), caster, skill.Id);
			EskrimerSkillHelper.KeepAttackPosture(caster);
		}

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (!EskrimerSkillHelper.TryBeginSelfSkill(skill, caster, originPos))
				return;

			skill.Run(this.HandleSkill(caster, skill));
		}

		private async Task HandleSkill(ICombatEntity caster, Skill skill)
		{
			try
			{
				caster.StartBuff(BuffId.Invitation_Buff, skill.Level, 0f, TimeSpan.FromSeconds(2), caster, skill.Id);

				await skill.Wait(TimeSpan.FromMilliseconds(2000));

				if (!caster.IsDead)
				{
					caster.StopBuff(BuffId.Invitation_Buff);
					caster.StartBuff(BuffId.Pret_Buff, skill.Level, EskrimerSkillHelper.PretPasataFinalDamageCap, TimeSpan.FromSeconds(10), caster, skill.Id);
				}
			}
			finally
			{
				EskrimerSkillHelper.KeepAttackPosture(caster);
			}
		}
	}
}
