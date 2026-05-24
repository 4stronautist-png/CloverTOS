using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Melia.Shared.Packages;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Yggdrasil.Geometry.Shapes;
using static Melia.Zone.Skills.SkillUseFunctions;
using static Melia.Zone.Skills.Helpers.SkillDamageHelper;

namespace Melia.Zone.Skills.Handlers.Wizards.Necromancer
{
	/// <summary>
	/// Handler for the Necromancer skill Gather Corpse.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Necromancer_GatherCorpse)]
	public class Necromancer_GatherCorpseOverride : IForceSkillHandler
	{
		private const int HitCount = 4;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity designatedTarget)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();

			if (designatedTarget == null)
			{
				Send.ZC_NORMAL.SkillTargetAnimation(caster, skill, caster.Direction, 1);
				Send.ZC_SKILL_FORCE_TARGET(caster, null, skill);
				return;
			}

			caster.TurnTowards(designatedTarget);
			caster.SetAttackState(true);

			Send.ZC_SKILL_READY(caster, skill, designatedTarget.Position, Position.Zero);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, designatedTarget.Handle, caster.Position, designatedTarget.Direction, Position.Zero);

			var skillHitDelay = skill.Properties.HitDelay;

			var splashArea = new CircleF(designatedTarget.Position, (int)skill.Properties.GetFloat(PropertyName.SplRange));
			var targets = caster.Map.GetAttackableEnemiesIn(caster, splashArea);

			var skeletonCount = caster is Character character ? NecromancerSkillHelper.CountSkeletons(character) : 0;
			var damageMultiplier = 1f + (skeletonCount * 0.15f);
			if (caster.IsAbilityActive(AbilityId.Necromancer34))
				damageMultiplier += 0.50f;

			var hitTargets = targets.LimitBySDR(caster, skill);
			if (caster.IsAbilityActive(AbilityId.Necromancer34))
				hitTargets = hitTargets.Take(1);

			skill.Run(this.HandleHits(skill, caster, designatedTarget, hitTargets.ToList(), damageMultiplier, skillHitDelay, skeletonCount));
		}

		private async Task HandleHits(Skill skill, ICombatEntity caster, ICombatEntity designatedTarget, List<ICombatEntity> hitTargets, float damageMultiplier, TimeSpan skillHitDelay, int skeletonCount)
		{
			var hitsByDelay = new List<(ICombatEntity Target, SkillHitResult Result, TimeSpan Delay)>();
			var skillHits = new List<SkillHitInfo>();

			for (var hitIndex = 0; hitIndex < HitCount; hitIndex++)
			{
				foreach (var target in hitTargets.Where(target => !target.IsDead))
				{
					var skillHitResult = SCR_SkillHit(caster, target, skill);
					skillHitResult.Damage *= damageMultiplier;
					var damageDelay = TimeSpan.FromMilliseconds(hitIndex * 120);
					var skillHit = new SkillHitInfo(caster, target, skill, skillHitResult, damageDelay, skillHitDelay);
					skillHit.ForceId = ForceId.GetNew();
					skillHits.Add(skillHit);
					hitsByDelay.Add((target, skillHitResult, damageDelay));

					if (hitIndex == 0)
					{
						target.StartBuff(BuffId.NecromancerPoison_Debuff, TimeSpan.FromSeconds(20), caster);
						target.StartBuff(BuffId.GatherCorpse_Debuff, TimeSpan.FromSeconds(6), caster);
					}
				}
			}

			if (skillHits.Count > 0)
				Send.ZC_SKILL_FORCE_TARGET(caster, designatedTarget, skill, skillHits);

			var elapsed = TimeSpan.Zero;
			foreach (var hit in hitsByDelay.OrderBy(hit => hit.Delay))
			{
				var wait = hit.Delay - elapsed;
				if (wait > TimeSpan.Zero)
				{
					await skill.Wait(wait);
					elapsed = hit.Delay;
				}

				if (!hit.Target.IsDead)
					hit.Target.TakeDamage(hit.Result.Damage, caster);
			}

			Send.ZC_NORMAL.Skill_45(caster);
			Send.ZC_NORMAL.SkillCancel(caster, skill.Id);
			Send.ZC_NORMAL.SkillCancelCancel(caster, skill.Id);

			if (skeletonCount > 0)
				skill.ReduceCooldown(TimeSpan.FromSeconds(Math.Min(19, skeletonCount)));
		}
	}
}
