using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;
using Yggdrasil.Util;
using static Melia.Shared.Util.TaskHelper;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Swordsmen.NakMuay
{
    /// <summary>
    /// Handler for the NakMuay skill Khao Loi.
    /// </summary>
    [SkillHandler(SkillId.NakMuay_KhaoLoi)]
    public class NakMuay_KhaoLoi : IGroundSkillHandler
    {
	    
	    private const float JumpDistance = 80;
	    private const float MaxJumpDistance = 80;

        /// <summary>
        /// Handles usage of the skill.
        /// </summary>
        /// <param name="skill"></param>
        /// <param name="caster"></param>
        /// <param name="originPos"></param>
        /// <param name="farPos"></param>
        /// <param name="target"></param>
        public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
        {
	        if (!caster.TrySpendSp(skill))
	        {
		        caster.ServerMessage(Localization.Get("Not enough SP."));
		        return;
	        }

	        skill.IncreaseOverheat();
	        caster.SetAttackState(true);
	        
	        if (caster.TryGetActiveAbility(AbilityId.NakMuay13, out _))
	        {
		        JumpToTarget(caster, target);
	        }

	        Send.ZC_SKILL_READY(caster, skill, originPos, farPos);
	        Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos, null);
	        
	        var splashParam = skill.GetSplashParameters(caster, originPos, farPos, length: 45, width: 40, angle: 10f);
	        var splashArea = skill.GetSplashArea(SplashType.Square, splashParam);
	        
	        skill.Run(this.Attack(skill, caster, splashArea));
        }
        
        private async Task Attack(Skill skill, ICombatEntity caster, ISplashArea splashArea)
        {
            var damageDelay = TimeSpan.FromMilliseconds(330);
            var skillHitDelay = skill.Properties.HitDelay;

            damageDelay /= skill.Properties.GetFloat(PropertyName.SklSpdRate);
            skillHitDelay /= skill.Properties.GetFloat(PropertyName.SklSpdRate);

            await skill.Wait(skillHitDelay);

            var hits = new List<SkillHitInfo>();
            var targets = caster.Map.GetAttackableEnemiesIn(caster, splashArea);

            foreach (var target in targets.LimitBySDR(caster, skill))
            {
	            var skillHitResult = SCR_NakSkillHit(caster, target, skill, SkillModifier.Default);
	            target.TakeDamage(skillHitResult.Damage, caster);
	            
	            if (caster.TryGetActiveAbility(AbilityId.NakMuay13, out _)) target.StartBuff(BuffId.Stun,
		            skill.Level, 0, TimeSpan.FromSeconds(1.5), caster);

	            var skillHit = new SkillHitInfo(caster, target, skill, skillHitResult, damageDelay, TimeSpan.Zero)
	            {
		            HitInfo = { ResultType = skillHitResult.Result }
	            };
	            hits.Add(skillHit);
            }
            Send.ZC_SKILL_HIT_INFO(caster, hits);
        }
        
        private void JumpToTarget(ICombatEntity caster, ICombatEntity target)
        {
	        var casterPos = caster.Position;
	        var targetPos = target.Position;

	        var jumpDest = casterPos.GetRelative(targetPos, JumpDistance);
	        var isValidDest = caster.Map.Ground.IsValidPosition(jumpDest);
	        if (!isValidDest)
		        return;

	        var dist = casterPos.Get2DDistance(jumpDest);
	        if (dist is <= 0 or > MaxJumpDistance)
		        return;

	        caster.Position = jumpDest;
	        caster.TurnTowards(target);

	        Send.ZC_SET_POS(caster);
        }
    }
}
