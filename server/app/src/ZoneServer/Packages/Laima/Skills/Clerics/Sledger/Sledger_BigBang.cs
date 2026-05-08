using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;
using Yggdrasil.Util;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Clerics.Sledger
{
	/// <summary>
	/// Handles Big Bang.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Sledger_BigBang_Cleric)]
	public class Sledger_BigBangOverride : IGroundSkillHandler
	{
		private const int GroundExplosionTicks = 10;
		private static readonly TimeSpan GroundExplosionTickInterval = TimeSpan.FromSeconds(1);

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!SledgerSkillHelper.TryStart(skill, caster, originPos, farPos, allowMovement: true))
				return;

			var area = SledgerSkillHelper.CreateCircle(skill, caster, farPos, radius: 55);
			SledgerSkillHelper.AttackArea(skill, caster, area, skill.Data.MultiHitCount, appliesBigBangReduction: false, bigBangReductionSeconds: 0);

			caster.StartBuff(BuffId.Sledger_Fighting_Buff, skill.Level, SledgerSkillHelper.FightingSpiritFinalDamageBonus, SledgerSkillHelper.FightingSpiritDuration, caster, skill.Id);

			if (caster.IsAbilityActive(AbilityId.Sledger15))
				skill.Run(this.GroundExplosion(skill, caster, farPos));
		}

		private async Task GroundExplosion(Skill skill, ICombatEntity caster, Position position)
		{
			for (var i = 0; i < GroundExplosionTicks; ++i)
			{
				await skill.Wait(GroundExplosionTickInterval);

				if (caster.IsDead)
					break;

				var area = new Circle(position, 55);
				var hits = new List<SkillHitInfo>();
				var targets = caster.Map.GetAttackableEnemiesIn(caster, area, hitType: skill.Data.HitType);
				var modifier = SkillModifier.MultiHit(1);
				modifier.DamageMultiplier *= 0.35f;

				foreach (var target in targets.LimitBySDR(caster, skill))
				{
					if (target == null || target.IsDead || !caster.CanDamage(target))
						continue;

					var result = SCR_SkillHit(caster, target, skill, modifier);
					target.TakeDamage(result.Damage, caster);

					var hit = new SkillHitInfo(caster, target, skill, result, TimeSpan.Zero, TimeSpan.Zero);
					hit.ForceId = 0;
					hit.HitEffect = HitEffect.Impact;
					hits.Add(hit);
				}

				if (hits.Count > 0)
					Send.ZC_SKILL_HIT_INFO(caster, hits);
			}
		}
	}
}
