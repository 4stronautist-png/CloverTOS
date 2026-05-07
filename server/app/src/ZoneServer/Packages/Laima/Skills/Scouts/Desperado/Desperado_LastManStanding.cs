using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Scouts.Desperado
{
	[Package("laima")]
	[SkillHandler(SkillId.Desperado_LastManStanding)]
	public class Desperado_LastManStandingOverride : IGroundSkillHandler
	{
		private const int BulletCount = 6;
		private const int ViolentCost = 6;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);
			if (caster.Map.IsPVP)
			{
				caster.StartBuff(BuffId.Skill_SuperArmor_Buff, skill.Level, 0, TimeSpan.FromSeconds(2), caster);
				caster.StartBuff(BuffId.Desperado_Reduce, skill.Level, 0, TimeSpan.FromSeconds(2), caster, skill.Id);
			}
			else
			{
				caster.StartBuff(BuffId.Skill_NoDamage_Buff, skill.Level, 0, TimeSpan.FromSeconds(2), caster);
			}

			DesperadoSkillHelper.ApplyMaxBadGuyStacks(caster, skill);
			Send.ZC_SKILL_READY(caster, skill, originPos, farPos);
			DesperadoSkillHelper.SendSkillEffect(caster, target, originPos, farPos);
			DesperadoSkillHelper.AttachLastManStandingAura(caster);
			DesperadoSkillHelper.PlayAnimationIfKnown(caster, "SKL_DESPERADO_LMS", true);
			DesperadoSkillHelper.LeapCaster(caster, caster.Position.GetDirection(farPos), 40, moveTime: 0.2f);
			DesperadoSkillHelper.PlaySoundIfKnown(caster, "skl_eff_desperado_lastmanstanding_cast");
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos);

			skill.Run(this.Attack(skill, caster, farPos));
		}

		private async Task Attack(Skill skill, ICombatEntity caster, Position farPos)
		{
			await skill.Wait(TimeSpan.FromMilliseconds(1200));
			DesperadoSkillHelper.PlaySoundIfKnown(caster, "skl_eff_desperado_lastmanstanding_shot");

			await skill.Wait(TimeSpan.FromMilliseconds(50));

			var hitCount = caster.IsAbilityActive(AbilityId.Desperado25) ? 2 : 1;
			var modifier = DesperadoSkillHelper.BuildModifier(caster, skill, hitCount, ViolentCost);
			var targets = caster.SelectObjects(180)
				.Where(target => !target.IsDead && caster.CanDamage(target))
				.OrderBy(target => target.Position.Get2DDistance(farPos))
				.Take(BulletCount)
				.ToList();

			if (targets.Count == 0)
			{
				await skill.Wait(TimeSpan.FromMilliseconds(550));
				DesperadoSkillHelper.DetachLastManStandingAura(caster);
				return;
			}

			var results = new System.Collections.Generic.List<SkillHitResult>();
			var primaryTargets = targets;
			foreach (var primaryTarget in primaryTargets)
				DesperadoSkillHelper.PlayEffectIfKnown(primaryTarget, "skl_eff_desperado_lastmanstanding_target", 1.5f);

			for (var i = 0; i < BulletCount; i++)
			{
				var primaryTarget = primaryTargets[i % primaryTargets.Count];
				DesperadoSkillHelper.PlayEffectIfKnown(caster, $"SKL_DESPERADO_SHOT{i + 1}", 1f, EffectLocation.Middle);
				var bulletTargets = caster.IsAbilityActive(AbilityId.Desperado25)
					? new[] { primaryTarget }
					: this.GetPiercingBulletTargets(skill, caster, primaryTarget, targets);

				results.AddRange(DesperadoSkillHelper.DealDamage(caster, skill, bulletTargets, modifier));

				if (i < BulletCount - 1)
					await skill.Wait(TimeSpan.FromMilliseconds(80));
			}

			DesperadoSkillHelper.ApplyBadGuyOnAccurateHit(caster, skill, results);

			await skill.Wait(TimeSpan.FromMilliseconds(550));
			DesperadoSkillHelper.DetachLastManStandingAura(caster);
		}

		private IEnumerable<ICombatEntity> GetPiercingBulletTargets(Skill skill, ICombatEntity caster, ICombatEntity primaryTarget, List<ICombatEntity> fallbackTargets)
		{
			var directionTarget = primaryTarget?.Position ?? caster.Position.GetRelative(caster.Direction, 130f);
			var splashParam = skill.GetSplashParameters(caster, caster.Position, directionTarget, length: 130f, width: 35f, angle: 0);
			var splashArea = skill.GetSplashArea(SplashType.Square, splashParam);
			var targets = DesperadoSkillHelper.GetTargets(caster, skill, splashArea);

			if (primaryTarget != null && !targets.Contains(primaryTarget))
				targets.Insert(0, primaryTarget);

			return targets.Count == 0 ? fallbackTargets.Take(1) : targets.Take(15);
		}
	}
}
