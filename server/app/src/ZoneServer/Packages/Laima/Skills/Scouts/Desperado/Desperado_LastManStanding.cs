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
using Melia.Zone.Network.Helpers;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Scouts.Desperado
{
	[Package("laima")]
	[SkillHandler(SkillId.Desperado_LastManStanding)]
	public class Desperado_LastManStandingOverride : IGroundSkillHandler
	{
		private const int BulletCount = 6;
		private const int ViolentCost = 6;
		private static readonly TimeSpan DefensiveBuffDuration = TimeSpan.FromSeconds(2);
		private static readonly TimeSpan TargetMarkerDelay = TimeSpan.FromMilliseconds(120);
		private static readonly TimeSpan FirstShotDelay = TimeSpan.FromMilliseconds(1000);
		private static readonly TimeSpan BulletDelay = TimeSpan.FromMilliseconds(150);

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			var targets = this.GetPrimaryTargets(caster, farPos);
			if (targets.Count == 0)
			{
				caster.ServerMessage(Localization.Get("No target specified."));
				return;
			}

			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			var castDirection = caster.Direction;
			var targetHandle = target?.Handle ?? 0;

			skill.IncreaseOverheat();
			caster.SetAttackState(true);
			if (caster.Map.IsPVP)
			{
				caster.StartBuff(BuffId.Skill_SuperArmor_Buff, skill.Level, 0, DefensiveBuffDuration, caster);
				caster.StartBuff(BuffId.Desperado_Reduce, skill.Level, 0, DefensiveBuffDuration, caster, skill.Id);
			}
			else
			{
				caster.StartBuff(BuffId.Skill_NoDamage_Buff, skill.Level, 0, DefensiveBuffDuration, caster);
			}

			DesperadoSkillHelper.ApplyMaxBadGuyStacks(caster, skill);
			DesperadoSkillHelper.LeapCaster(caster, castDirection, 40, moveTime: 0.2f);
			DesperadoSkillHelper.AttachLastManStandingAura(caster);
			this.SendScheduledAnimations(caster);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, targetHandle, originPos, castDirection, Position.Zero);
			Send.ZC_SKILL_READY(caster, skill, 1, originPos, originPos);
			Send.ZC_ON_AFTER_IMAGE(caster, 300f, 500f, 160f, 130f, 30f, 100f);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, originPos, ForceId.GetNew(), null);

			skill.Run(this.Attack(skill, caster, farPos, targets));
		}

		private async Task Attack(Skill skill, ICombatEntity caster, Position farPos, List<ICombatEntity> targets)
		{
			var hitCount = caster.IsAbilityActive(AbilityId.Desperado25) ? 2 : 1;
			var modifier = DesperadoSkillHelper.BuildModifier(caster, skill, hitCount, ViolentCost);

			var results = new System.Collections.Generic.List<SkillHitResult>();
			var primaryTargets = targets;
			var markerCount = primaryTargets.Count;
			for (var i = 0; i < markerCount; i++)
			{
				DesperadoSkillHelper.PlaySoundIfKnown(caster, "skl_eff_desperado_lastmanstanding_target");
				this.SendTargetMarkerEffects(caster, primaryTargets[i]);

				if (i < markerCount - 1)
					await skill.Wait(TargetMarkerDelay);
			}

			var elapsedMarkerDelay = TargetMarkerDelay.TotalMilliseconds * Math.Max(0, markerCount - 1);
			var remainingFirstShotDelay = Math.Max(0, FirstShotDelay.TotalMilliseconds - elapsedMarkerDelay);
			await skill.Wait(TimeSpan.FromMilliseconds(remainingFirstShotDelay));

			for (var i = 0; i < primaryTargets.Count; i++)
			{
				var primaryTarget = primaryTargets[i];
				var bulletTargets = caster.IsAbilityActive(AbilityId.Desperado25)
					? new[] { primaryTarget }
					: this.GetPiercingBulletTargets(skill, caster, primaryTarget, targets);
				var shotDirection = caster.Position.GetDirection(primaryTarget.Position);

				if (shotDirection != Direction.Zero)
				{
					caster.Direction = shotDirection;
					Send.ZC_ROTATE(caster);
				}

				this.SendBulletEffects(caster, primaryTarget, i);
				results.AddRange(DesperadoSkillHelper.DealDamage(caster, skill, bulletTargets, modifier));

				if (i < primaryTargets.Count - 1)
					await skill.Wait(BulletDelay);
			}

			DesperadoSkillHelper.ApplyBadGuyOnAccurateHit(caster, skill, results);

			await skill.Wait(TimeSpan.FromMilliseconds(520));
			DesperadoSkillHelper.DetachLastManStandingAura(caster);
			Send.ZC_OFF_AFTER_IMAGE(caster);
		}

		private List<ICombatEntity> GetPrimaryTargets(ICombatEntity caster, Position farPos)
		{
			return caster.SelectObjects(180)
				.Where(target => !target.IsDead && caster.CanDamage(target))
				.OrderBy(target => target.Position.Get2DDistance(farPos))
				.Take(BulletCount)
				.ToList();
		}

		private void SendScheduledAnimations(ICombatEntity caster)
		{
			Send.ZC_PLAY_ANI(caster, "SKL_DESPERADO_DEADLYFIRE_END", readyTime: 1.9f, b1: 1);
			Send.ZC_PLAY_ANI(caster, "SKL_DESPERADO_SHOT6", readyTime: 1.65f, b1: 1);
			Send.ZC_PLAY_ANI(caster, "SKL_DESPERADO_SHOT3", readyTime: 1.55f, b1: 1);
			Send.ZC_PLAY_ANI(caster, "SKL_DESPERADO_SHOT1", readyTime: 1.45f, b1: 1);
			Send.ZC_PLAY_ANI(caster, "SKL_DESPERADO_SHOT5", readyTime: 1.35f, b1: 1);
			Send.ZC_PLAY_ANI(caster, "SKL_DESPERADO_SHOT4", readyTime: 1.25f, b1: 1);
			Send.ZC_PLAY_ANI(caster, "SKL_DESPERADO_LMS", b1: 1);
		}

		private void SendTargetMarkerEffects(ICombatEntity caster, ICombatEntity target)
		{
			var skullPosition = target.Position + new Position(0, 35f, 0);
			var aimPosition = target.Position + new Position(0, 10f, 0);

			Send.ZC_GROUND_EFFECT(caster, skullPosition, "BodyAura_DarkSkull_Red_01", scale: 0.5f, duration: 0.3f);
			Send.ZC_GROUND_EFFECT(caster, aimPosition, "BodyAura_Aim_Red_01", scale: 0.5f, duration: 0.4f);
		}

		private void SendBulletEffects(ICombatEntity caster, ICombatEntity target, int bulletIndex)
		{
			_ = bulletIndex;
			var shotDirection = caster.Position.GetDirection(target.Position);
			var muzzlePosition = caster.Position.GetRelative(shotDirection, 20f) + new Position(0, 20f, 0);
			var hitPosition = target.Position + new Position(0, 20f, 0);
			var rotationX = shotDirection.NormalDegreeAngle;
			var shotDistance = DesperadoSkillHelper.GetTrackedBulletDistance(caster, target);

			DesperadoSkillHelper.SendTrackedBulletProjectile(caster, shotDirection, distance: shotDistance);
			Send.ZC_UNITY_GROUND_EFFECT(caster, "AerialExplosion_Steam_White_01".GetStringId(), 0.2f, muzzlePosition, rotationX, 0f, 0f, new Direction(0f, 0f));
			Send.ZC_UNITY_GROUND_EFFECT(caster, "Shoot_DarkMuzzleFlash_Orange_03".GetStringId(), 0.5f, muzzlePosition, rotationX, 0f, 0f, new Direction(0f, 0f));
			Send.ZC_UNITY_GROUND_EFFECT(caster, "AerialExplosion_DarkAura_Red_01".GetStringId(), 0.4f, hitPosition, rotationX + 180f, 0f, 0f, new Direction(1f, 0f));
			Send.ZC_UNITY_GROUND_EFFECT(caster, "Hit_WesternShotgun_Orange_01".GetStringId(), 0.6f, hitPosition, rotationX + 180f, 0f, 0f, new Direction(1f, 0f));
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
