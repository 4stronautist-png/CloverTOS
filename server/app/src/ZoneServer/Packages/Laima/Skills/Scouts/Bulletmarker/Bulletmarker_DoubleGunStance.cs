using System;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.Packages;
using Melia.Zone.Network;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Skills.Handlers.Scouts.Bulletmarker
{
	/// <summary>
	/// Handles Double Gun Stance as a stance buff instead of an attack.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Bulletmarker_DoubleGunStance)]
	public class Bulletmarker_DoubleGunStance : ISelfSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Melia.Shared.World.Position originPos, Melia.Shared.World.Direction dir)
		{
			if (caster is not Character casterCharacter)
				return;

			if (casterCharacter.IsBuffActive(BuffId.DoubleGunStance_Buff))
			{
				Send.ZC_SKILL_READY(caster, skill, ZoneServer.Instance.World.CreateSkillHandle(), originPos, Melia.Shared.World.Position.Zero);
				Send.ZC_SKILL_MELEE_TARGET(caster, skill, caster);
				casterCharacter.RemoveBuff(BuffId.DoubleGunStance_Buff);
				caster.SetAttackState(false);
				return;
			}

			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);

			Send.ZC_SKILL_READY(caster, skill, ZoneServer.Instance.World.CreateSkillHandle(), originPos, Melia.Shared.World.Position.Zero);
			Send.ZC_SKILL_MELEE_TARGET(caster, skill, caster);
			casterCharacter.StartBuff(BuffId.DoubleGunStance_Buff, skill.Level, 0, TimeSpan.Zero, casterCharacter, skill.Id);
		}
	}
}
