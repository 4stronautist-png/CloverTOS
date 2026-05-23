using System;
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
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Skills.Handlers.Scouts.Desperado
{
	[Package("laima")]
	[SkillHandler(SkillId.Desperado_Revenged)]
	public class Desperado_RevengedOverride : IGroundSkillHandler
	{
		private const int ViolentCost = 2;
		private static readonly TimeSpan FirstShotDelay = TimeSpan.FromMilliseconds(210);
		private static readonly TimeSpan SecondDashEffectDelay = TimeSpan.FromMilliseconds(35);
		private static readonly TimeSpan SecondShotDelay = TimeSpan.FromMilliseconds(70);
		private static readonly TimeSpan AfterImageDuration = TimeSpan.FromMilliseconds(320);
		private static readonly TimeSpan FirstDamageDelay = TimeSpan.FromMilliseconds(120);
		private static readonly TimeSpan SecondDamageDelay = TimeSpan.FromMilliseconds(50);

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			Send.ZC_ON_AFTER_IMAGE(caster, 300f, 500f, 160f, 130f, 30f, 100f);
			caster.SetAttackState(true);
			caster.StartBuff(BuffId.Skill_NoDamage_Buff, skill.Level, 0, TimeSpan.FromMilliseconds(700), caster);

			var castDirection = caster.Direction;
			var inputDirection = originPos != farPos ? originPos.GetDirection(farPos) : castDirection;
			var targetHandle = target?.Handle ?? 0;
			var forceId = ForceId.GetNew();

			Send.ZC_SKILL_READY(caster, skill, 1, originPos, originPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, targetHandle, originPos, castDirection, Position.Zero);
			SendInitialDashEffects(caster, originPos, castDirection);
			var attackOrigin = DesperadoSkillHelper.MoveCasterAroundTarget(caster, target, inputDirection, farPos);
			AlignCasterToTarget(caster, target);

			Send.ZC_PLAY_ANI(caster, "SKL_DESPERADO_SHOT5", readyTime: 0.5f, b1: 1);
			Send.ZC_PLAY_ANI(caster, "SKL_DESPERADO_SHOT4", readyTime: 0.3f, b1: 1);
			Send.ZC_PLAY_ANI(caster, "SKL_DESPERADO_BACKDASH", b1: 1);
			if (caster is Character character)
				DesperadoSkillHelper.SendDesperadoAnim(character, 3);

			DesperadoSkillHelper.ApplyCustomEffect(caster);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, originPos, forceId, null);

			skill.Run(this.Attack(skill, caster, attackOrigin, target));
		}

		private async Task Attack(Skill skill, ICombatEntity caster, Position attackOrigin, ICombatEntity focusedTarget)
		{
			await skill.Wait(FirstShotDelay);

			var modifier = DesperadoSkillHelper.BuildModifier(caster, skill, 3, ViolentCost);
			if (caster.TryGetBuff(BuffId.Desperado_Trail, out _))
			{
				if (caster.TryGetSkillLevel(SkillId.Desperado_Equilibrium, out var equilibriumLevel))
					modifier.SkillFactorBonus += 4f * equilibriumLevel;

				caster.RemoveBuff(BuffId.Desperado_Trail);
			}

			var damageDirection = focusedTarget != null ? attackOrigin.GetDirection(focusedTarget.Position) : caster.Direction;
			var splashParam = skill.GetSplashParameters(caster, attackOrigin, attackOrigin.GetRelative(damageDirection, 80f), length: 80f, width: 50f, angle: 90);
			var splashArea = skill.GetSplashArea(SplashType.Fan, splashParam);
			var targets = DesperadoSkillHelper.GetTargets(caster, skill, splashArea);

			if (focusedTarget != null)
				SendMuzzleEffects(caster, includeMuzzleFan: true, spread: 0.2f);

			AlignCasterToTarget(caster, focusedTarget);
			this.SendShotProjectiles(caster, targets, focusedTarget, damageDirection);

			AlignCasterToTarget(caster, focusedTarget);
			var results = DesperadoSkillHelper.DealDamage(caster, skill, targets, modifier, FirstDamageDelay);
			SendShotHitEffects(caster, targets, 0.2f);

			await skill.Wait(SecondDashEffectDelay);
			SendInitialDashEffects(caster, attackOrigin, caster.Direction);

			await skill.Wait(SecondShotDelay);
			SendMuzzleEffects(caster, includeMuzzleFan: false, spread: 0.4f);
			AlignCasterToTarget(caster, focusedTarget);
			this.SendShotProjectiles(caster, targets, focusedTarget, damageDirection);
			SendShotHitEffects(caster, targets, 0.4f, secondHitSpread: 0.25f);

			AlignCasterToTarget(caster, focusedTarget);
			results.AddRange(DesperadoSkillHelper.DealDamage(caster, skill, targets, modifier, SecondDamageDelay));
			DesperadoSkillHelper.ApplyBadGuyOnAccurateHit(caster, skill, results);

			await skill.Wait(AfterImageDuration - FirstShotDelay - SecondDashEffectDelay - SecondShotDelay);
			Send.ZC_OFF_AFTER_IMAGE(caster);
		}

		private static void SendInitialDashEffects(ICombatEntity caster, Position originPos, Direction castDirection)
		{
			var position = originPos + new Position(0, -0.1f, 0);
			Send.ZC_UNITY_GROUND_EFFECT(caster, "Teleport_SmearDash_Black_01".GetStringId(), 0.7f, position, castDirection.NormalDegreeAngle + 90f, 0f, 0f, new Direction(1f, 0f));
			Send.ZC_UNITY_GROUND_EFFECT(caster, "AerialExplosion_DarkAura_Black_01".GetStringId(), 0.4f, position, 0f, 0f, 0f, new Direction(1f, 0f));
		}

		private static void AlignCasterToTarget(ICombatEntity caster, ICombatEntity target)
		{
			if (target == null)
				return;

			caster.TurnTowards(target);
			Send.ZC_ROTATE(caster);
		}

		private static void SendMuzzleEffects(ICombatEntity caster, bool includeMuzzleFan, float spread)
		{
			var casterMuzzlePosition = caster.Position.GetRelative(caster.Direction, 20f);
			casterMuzzlePosition.Y += 20f;
			var rotationX = caster.Direction.NormalDegreeAngle + 90f;

			if (includeMuzzleFan)
				SendMuzzleFan(caster, casterMuzzlePosition, caster.Direction);
			else
				Send.ZC_UNITY_GROUND_EFFECT(caster, "Shoot_DarkMuzzleFlash_Orange_01".GetStringId(), 1f, casterMuzzlePosition, rotationX, 0f, 0f, new Direction(0f, spread));
		}

		private static void SendShotHitEffects(ICombatEntity caster, System.Collections.Generic.IEnumerable<ICombatEntity> targets, float spread, float secondHitSpread = 0f)
		{
			foreach (var target in targets)
			{
				var hitPosition = target.Position + new Position(0, 20f, 0);
				Send.ZC_UNITY_GROUND_EFFECT(caster, "Hit_Blow_Yellow_02".GetStringId(), 0.25f, hitPosition, 0f, 0f, 0f, new Direction(1f, spread));
				Send.ZC_UNITY_GROUND_EFFECT(caster, "Hit_Blow_Yellow_02".GetStringId(), 0.25f, hitPosition, 0f, 0f, 0f, new Direction(1f, secondHitSpread));
			}
		}

		private void SendShotProjectiles(ICombatEntity caster, System.Collections.Generic.IReadOnlyList<ICombatEntity> targets, ICombatEntity focusedTarget, Direction fallbackDirection)
		{
			if (targets.Count == 0)
			{
				var shotDirection = focusedTarget != null ? caster.Position.GetDirection(focusedTarget.Position) : fallbackDirection;
				if (shotDirection == Direction.Zero)
					shotDirection = caster.Direction;

				for (var i = 0; i < 3; i++)
				{
					var shotDistance = DesperadoSkillHelper.GetTrackedBulletDistance(caster, focusedTarget);
					DesperadoSkillHelper.SendTrackedBulletProjectile(caster, shotDirection, distance: shotDistance);
				}
				return;
			}

			for (var i = 0; i < targets.Count; i++)
			{
				var shotDirection = caster.Position.GetDirection(targets[i].Position);
				if (shotDirection == Direction.Zero)
					shotDirection = fallbackDirection != Direction.Zero ? fallbackDirection : caster.Direction;

				var shotDistance = DesperadoSkillHelper.GetTrackedBulletDistance(caster, targets[i]);
				DesperadoSkillHelper.SendTrackedBulletProjectile(caster, shotDirection, distance: shotDistance);
			}
		}

		private static void SendMuzzleFan(ICombatEntity caster, Position position, Direction castDirection)
		{
			var baseRotation = castDirection.NormalDegreeAngle + 90f;

			var muzzleFlash = "Shoot_DarkMuzzleFlash_Orange_01".GetStringId();
			Send.ZC_UNITY_GROUND_EFFECT(caster, muzzleFlash, 1f, position, baseRotation, 0f, 0f, new Direction(0f, 0f));
			Send.ZC_UNITY_GROUND_EFFECT(caster, muzzleFlash, 1f, position, baseRotation + 30f, 0f, 0f, new Direction(0f, 0.3f));
			Send.ZC_UNITY_GROUND_EFFECT(caster, muzzleFlash, 1f, position, baseRotation, 0f, 0f, new Direction(0f, 0.25f));
			Send.ZC_UNITY_GROUND_EFFECT(caster, muzzleFlash, 1f, position, baseRotation - 30f, 0f, 0f, new Direction(0f, 0.2f));
			Send.ZC_UNITY_GROUND_EFFECT(caster, muzzleFlash, 1f, position, baseRotation, 0f, 0f, new Direction(0f, 0.15f));
			Send.ZC_UNITY_GROUND_EFFECT(caster, muzzleFlash, 1f, position, baseRotation - 30f, 0f, 0f, new Direction(0f, 0.1f));
			Send.ZC_UNITY_GROUND_EFFECT(caster, muzzleFlash, 1f, position, baseRotation + 30f, 0f, 0f, new Direction(0f, 0.05f));
		}
	}
}
