using System;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Swordsmen.NakMuay
{
    /// <summary>
    /// Handler for the NakMuay skill Muay Thai.
    /// </summary>
    [SkillHandler(SkillId.NakMuay_MuayThai)]
    public class NakMuay_MuayThai : ISelfSkillHandler
    {
        /// <summary>
        /// Handles skill, applying the Muay Thai buff to the caster.
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

            skill.IncreaseOverheat();
            caster.SetAttackState(true);
            
            if (caster.IsBuffActive(BuffId.MuayThai_Buff)) caster.StopBuff(BuffId.MuayThai_Buff);

            // Apply Muay Thai buff - increases final damage
            var duration = TimeSpan.FromMinutes(30);

            if (caster.TryGetActiveAbility(AbilityId.NakMuay12, out _))
	            duration = TimeSpan.FromSeconds(20);
            
            caster.StartBuff(BuffId.MuayThai_Buff, skill.Level, 0, duration, caster);
            Send.ZC_SKILL_MELEE_TARGET(caster, skill, caster, null);
        }
    }
}
