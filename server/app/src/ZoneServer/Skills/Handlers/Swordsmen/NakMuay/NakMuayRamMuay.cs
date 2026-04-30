using System;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Swordsmen.NakMuay
{
	/// <summary>
	/// Handler for the NakMuay skill Ram Muay.
	/// </summary>
	[SkillHandler(SkillId.NakMuay_RamMuay)]
	public class NakMuayRamMuay : ISelfSkillHandler
	{
		/// <summary>
		/// Handles skill, applying the Ram Muay buff to the caster.
		/// </summary>
		/// <param name="skill"></param>
		/// <param name="caster"></param>
		/// <param name="originPos"></param>
		/// <param name="dir"></param>
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			if (caster is Character character)
			{
				Send.ZC_STANCE_CHANGE(character);

				if (caster.IsBuffActive(BuffId.RamMuay_Buff))
				{
					caster.StopBuff(BuffId.RamMuay_Buff);
				}
				else
					caster.StartBuff(BuffId.RamMuay_Buff, skill.Level, 0, TimeSpan.Zero, caster, skill.Id);

				Send.ZC_STANCE_CHANGE(character);
			}

			// Notify client about the stance change and skill animation
			Send.ZC_SKILL_MELEE_TARGET(caster, skill, caster, null);

			skill.IncreaseOverheat();
			caster.SetAttackState(true);
		}
	}
}
