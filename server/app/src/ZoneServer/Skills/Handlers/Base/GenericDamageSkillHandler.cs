using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.Game.Properties;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Base
{
	/// <summary>
	/// Data-driven fallback for offensive skills without a dedicated handler.
	/// </summary>
	public class GenericDamageSkillHandler : SimpleMonsterAttackSkill, IGroundSkillHandler, IForceSkillHandler, IForceGroundSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (target != null && skill.Data.TargetType == SkillTargetType.Actor)
			{
				this.Handle(skill, caster, target);
				return;
			}

			skill.Run(this.AttackGround(skill, caster, originPos, farPos));
		}

		private async Task AttackGround(Skill skill, ICombatEntity caster, Position originPos, Position farPos)
		{
			if (!caster.TrySpendSp(skill))
				return;

			skill.IncreaseOverheat();

			var splashArea = this.GetSplashArea(skill, originPos, farPos);
			var hitDelay = skill.Properties.HitDelay;
			var aniTime = (skill.Data.HitTime.Count > 0 ? skill.Data.HitTime.First() : TimeSpan.Zero) + hitDelay;
			var speedRate = Math.Max(0.01f, skill.Properties.GetFloat(PropertyName.SklSpdRate));
			aniTime /= speedRate;

			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos, null);

			await skill.Wait(aniTime);

			if (!caster.CanFight())
			{
				Send.ZC_SKILL_DISABLE(caster);
				return;
			}

			var hits = new List<SkillHitInfo>();
			foreach (var target in caster.Map.GetAttackableEnemiesIn(caster, splashArea, hitType: skill.Data.HitType).LimitBySDR(caster, skill))
			{
				if (target == null || target.IsDead || !caster.CanDamage(target))
					continue;

				var skillHitResult = SCR_SkillHit(caster, target, skill);
				target.TakeDamage(skillHitResult.Damage, caster);
				hits.Add(new SkillHitInfo(caster, target, skill, skillHitResult, hitDelay, skill.Properties.HitDelay));
			}

			Send.ZC_SKILL_HIT_INFO(caster, hits);
		}

		private ISplashArea GetSplashArea(Skill skill, Position originPos, Position farPos)
		{
			var direction = originPos.GetDirection(farPos);

			switch (skill.Data.SplashType)
			{
				case SplashType.Circle:
				{
					var radius = Math.Max(skill.Properties.GetFloat(PropertyName.SplHeight), skill.Properties.GetFloat(PropertyName.SplRange));
					return new Circle(farPos, Math.Max(1, radius));
				}
				case SplashType.Fan:
				{
					var height = Math.Max(1, skill.Properties.GetFloat(PropertyName.SplHeight));
					var angle = Math.Max(1, skill.Properties.GetFloat(PropertyName.SplAngle));
					return new Fan(originPos, direction, height, angle);
				}
				default:
				{
					var height = Math.Max(1, skill.Properties.GetFloat(PropertyName.SplHeight));
					var width = Math.Max(1, skill.Properties.GetFloat(PropertyName.SplRange));
					return new Square(originPos, direction, height, width);
				}
			}
		}
	}
}
