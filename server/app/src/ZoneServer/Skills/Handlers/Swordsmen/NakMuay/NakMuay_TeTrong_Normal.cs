using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Yggdrasil.Util;
using static Melia.Shared.Util.TaskHelper;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Swordsmen.NakMuay
{
    /// <summary>
    /// Handler for the NakMuay skill Te Trong Normal.
    /// </summary>
    [SkillHandler(SkillId.NakMuay_TeTrong_Normal)]
    public class NakMuay_TeTrong_Normal : IMeleeGroundSkillHandler
    {
        /// <summary>
        /// Handles usage of the skill.
        /// </summary>
        /// <param name="skill"></param>
        /// <param name="caster"></param>
        /// <param name="originPos"></param>
        /// <param name="farPos"></param>
        /// <param name="targets"></param>
        public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, IList<ICombatEntity> targets)
        {
            if (!caster.TrySpendSp(skill))
            {
                caster.ServerMessage(Localization.Get("Not enough SP."));
                return;
            }

            skill.IncreaseOverheat();
            caster.SetAttackState(true);

            Send.ZC_SKILL_READY(caster, skill, originPos, farPos);
            Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos, null);

            skill.Run(this.Attack(skill, caster, originPos, farPos, targets));
        }

        /// <summary>
        /// Executes the actual attack after a potential delay.
        /// </summary>
        /// <param name="skill"></param>
        /// <param name="caster"></param>
        /// <param name="castPosition"></param>
        /// <param name="targetPosition"></param>
        /// <param name="targets"></param>
        private async Task Attack(Skill skill, ICombatEntity caster, Position castPosition, Position targetPosition, IEnumerable<ICombatEntity> targets)
        {
            var damageDelay = TimeSpan.FromMilliseconds(330);
            var skillHitDelay = skill.Properties.HitDelay;

            damageDelay /= skill.Properties.GetFloat(PropertyName.SklSpdRate);
            skillHitDelay /= skill.Properties.GetFloat(PropertyName.SklSpdRate);

            await skill.Wait(skillHitDelay);

            var hits = new List<SkillHitInfo>();

            foreach (var target in targets)
            {
                var skillHitResult = SCR_NakSkillHit(caster, target, skill, SkillModifier.Default);
                target.TakeDamage(skillHitResult.Damage, caster);
                var skillHit = new SkillHitInfo(caster, target, skill, skillHitResult, damageDelay, TimeSpan.Zero);
                skillHit.HitInfo.ResultType = skillHitResult.Result;
                hits.Add(skillHit);
            }

            Send.ZC_SKILL_HIT_INFO(caster, hits);
        }
    }
}
