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

namespace Melia.Zone.Skills.Handlers.Scouts.Desperado
{
	[Package("laima")]
	[SkillHandler(SkillId.Desperado_Equilibrium)]
	public class Desperado_EquilibriumOverride : IGroundSkillHandler
	{
		private const int ViolentCost = 1;
		private const float ForwardLeapDistance = 80f;
		private const float RetreatLeapDistance = 60f;
		private const float RetreatProjectileDistance = 118f;
		private const float RetreatProjectileSpreadAngle = 90f;

		private static readonly TimeSpan ForwardAfterImageDuration = TimeSpan.FromMilliseconds(270);
		private static readonly TimeSpan RetreatAfterImageDuration = TimeSpan.FromMilliseconds(170);
		private static readonly TimeSpan ForwardSecondHitDelay = TimeSpan.FromMilliseconds(270);
		private static readonly TimeSpan FirstDamageDelay = TimeSpan.FromMilliseconds(100);
		private static readonly TimeSpan SecondDamageDelay = TimeSpan.FromMilliseconds(50);

		private const int RetreatProjectileIntervalMs = 33;

		private static readonly EquilibriumGroundEffect[] ForwardGroundEffects =
		{
			new("Teleport_SmearDash_Black_01", 0.80f, 0f, -0.10f, useCastAngleForRotationX: true, rotationXOffset: 90f, rotationY: 0f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.00f, b1: 0),
			new("Stomp_Dust_LightBrown_01", 0.25f, 80f, 0f, useCastAngleForRotationX: false, rotationXOffset: 0f, rotationY: 0f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.24f, b1: 0),
			new("Stomp_Dust_LightBrown_01", 0.29f, 53f, 0f, useCastAngleForRotationX: false, rotationXOffset: 0f, rotationY: 0f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.18f, b1: 0),
			new("Stomp_Dust_LightBrown_01", 0.33f, 40f, 0f, useCastAngleForRotationX: false, rotationXOffset: 0f, rotationY: 0f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.18f, b1: 0),
			new("Stomp_Dust_LightBrown_01", 0.37f, 26f, 0f, useCastAngleForRotationX: false, rotationXOffset: 0f, rotationY: 0f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.12f, b1: 0),
			new("Stomp_Dust_LightBrown_01", 0.41f, 20f, 0f, useCastAngleForRotationX: false, rotationXOffset: 0f, rotationY: 0f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.06f, b1: 0),
			new("Stab_Dark_Orange_01", 1.30f, 0f, 20f, useCastAngleForRotationX: true, rotationXOffset: 90f, rotationY: 0f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.00f, b1: 0),
			new("AerialExplosion_DarkAura_Black_01", 0.50f, 0f, 0f, useCastAngleForRotationX: false, rotationXOffset: 0f, rotationY: 0f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.00f, b1: 0),
			new("Stomp_Dust_LightBrown_01", 0.45f, 0f, 0f, useCastAngleForRotationX: false, rotationXOffset: 0f, rotationY: 0f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.00f, b1: 0),
			new("eff_slash_normal_red_01", 0.60f, 85f, 20f, useCastAngleForRotationX: true, rotationXOffset: 0f, rotationY: -30f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.40f, b1: 0),
			new("eff_slash_normal_red_01", 0.60f, 85f, 20f, useCastAngleForRotationX: true, rotationXOffset: 0f, rotationY: 0f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.30f, b1: 0),
			new("eff_slash_normal_red_01", 0.60f, 85f, 20f, useCastAngleForRotationX: true, rotationXOffset: 0f, rotationY: 30f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.20f, b1: 0),
			new("AerialExplosion_DarkAura_Black_01", 0.35f, 80f, 0f, useCastAngleForRotationX: false, rotationXOffset: 0f, rotationY: 0f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.24f, b1: 0),
		};

		private static readonly EquilibriumGroundEffect[] RetreatGroundEffects =
		{
			new("Shoot_DarkMuzzleFlash_Orange_01", 1.00f, 54f, 20f, useCastAngleForRotationX: true, rotationXOffset: 120f, rotationY: 0f, rotationZ: 0f, directionCos: 0.00f, directionSin: 0.30f, b1: 0, backwards: true),
			new("Shoot_DarkMuzzleFlash_Orange_01", 1.00f, 54f, 20f, useCastAngleForRotationX: true, rotationXOffset: 90f, rotationY: 0f, rotationZ: 0f, directionCos: 0.00f, directionSin: 0.25f, b1: 0, backwards: true),
			new("Shoot_DarkMuzzleFlash_Orange_01", 1.00f, 54f, 20f, useCastAngleForRotationX: true, rotationXOffset: 60f, rotationY: 0f, rotationZ: 0f, directionCos: 0.00f, directionSin: 0.20f, b1: 0, backwards: true),
			new("Shoot_DarkMuzzleFlash_Orange_01", 1.00f, 36f, 20f, useCastAngleForRotationX: true, rotationXOffset: 90f, rotationY: 0f, rotationZ: 0f, directionCos: 0.00f, directionSin: 0.15f, b1: 0, backwards: true),
			new("Shoot_DarkMuzzleFlash_Orange_01", 1.00f, 18f, 20f, useCastAngleForRotationX: true, rotationXOffset: 60f, rotationY: 0f, rotationZ: 0f, directionCos: 0.00f, directionSin: 0.10f, b1: 0, backwards: true),
			new("Shoot_DarkMuzzleFlash_Orange_01", 1.00f, 0f, 20f, useCastAngleForRotationX: true, rotationXOffset: 120f, rotationY: 0f, rotationZ: 0f, directionCos: 0.00f, directionSin: 0.05f, b1: 0, backwards: true),
			new("Shoot_DarkMuzzleFlash_Orange_01", 1.00f, -18f, 20f, useCastAngleForRotationX: true, rotationXOffset: 90f, rotationY: 0f, rotationZ: 0f, directionCos: 0.00f, directionSin: 0.00f, b1: 0, backwards: true),
			new("AerialExplosion_DarkAura_Black_01", 0.35f, 80f, -0.10f, useCastAngleForRotationX: false, rotationXOffset: 0f, rotationY: 0f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.24f, b1: 0, backwards: true),
			new("Stomp_Dust_LightBrown_01", 0.25f, 80f, -0.10f, useCastAngleForRotationX: false, rotationXOffset: 0f, rotationY: 0f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.24f, b1: 0, backwards: true),
			new("Stomp_Dust_LightBrown_01", 0.29f, 53f, -0.10f, useCastAngleForRotationX: false, rotationXOffset: 0f, rotationY: 0f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.18f, b1: 0, backwards: true),
			new("Stomp_Dust_LightBrown_01", 0.33f, 40f, -0.10f, useCastAngleForRotationX: false, rotationXOffset: 0f, rotationY: 0f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.18f, b1: 0, backwards: true),
			new("Stomp_Dust_LightBrown_01", 0.37f, 26f, -0.10f, useCastAngleForRotationX: false, rotationXOffset: 0f, rotationY: 0f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.12f, b1: 0, backwards: true),
			new("Stomp_Dust_LightBrown_01", 0.41f, 20f, -0.10f, useCastAngleForRotationX: false, rotationXOffset: 0f, rotationY: 0f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.06f, b1: 0, backwards: true),
			new("AerialExplosion_DarkAura_Black_01", 0.50f, 0f, -0.10f, useCastAngleForRotationX: false, rotationXOffset: 0f, rotationY: 0f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.00f, b1: 0),
			new("Teleport_SmearDash_Black_01", 0.80f, 0f, -0.10f, useCastAngleForRotationX: true, rotationXOffset: 90f, rotationY: 0f, rotationZ: 0f, directionCos: 1.00f, directionSin: 0.00f, b1: 0),
		};

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			var retreatShot = caster.IsBuffActive(BuffId.Equilibrium_toggle_Buff);
			var maintainDistance = caster.IsAbilityActive(AbilityId.Desperado26);
			var castDirection = caster.Direction;
			if (castDirection == Direction.Zero)
				castDirection = originPos.GetDirection(farPos);
			var targetHandle = target?.Handle ?? 0;

			skill.IncreaseOverheat();
			Send.ZC_ON_AFTER_IMAGE(caster, 300f, 500f, 160f, 130f, 30f, 100f);

			if (retreatShot || maintainDistance)
				this.HandleRetreatShot(skill, caster, originPos, farPos, castDirection, targetHandle, target);
			else
				this.HandleForwardCast(skill, caster, originPos, castDirection, targetHandle);

			skill.Run(this.Attack(skill, caster, originPos, castDirection, retreatShot || maintainDistance));
		}

		private void HandleForwardCast(Skill skill, ICombatEntity caster, Position originPos, Direction castDirection, int targetHandle)
		{
			caster.StartBuff(BuffId.Equilibrium_toggle_Buff, skill.Level, 0, TimeSpan.FromSeconds(5), caster, skill.Id);
			if (caster.IsAbilityActive(AbilityId.Desperado23))
				caster.StartBuff(BuffId.Desperado_Weight, skill.Level, 0, TimeSpan.FromSeconds(3), caster, skill.Id);

			var forceId = ForceId.GetNew();
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, targetHandle, originPos, castDirection, Position.Zero);
			this.SendGroundEffects(caster, originPos, castDirection, ForwardGroundEffects);
			Send.ZC_SKILL_READY(caster, skill, 1, originPos, originPos);
			caster.SetAttackState(true);
			DesperadoSkillHelper.PlaySoundIfKnown(caster, "skl_eff_desperado_equilibrium_rush_shot");
			DesperadoSkillHelper.PlayAnimationIfKnown(caster, "SKL_DESPERADO_DASH");
			DesperadoSkillHelper.ApplyCustomEffect(caster);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, originPos, forceId, null);
			DesperadoSkillHelper.LeapCaster(caster, castDirection, ForwardLeapDistance);
			caster.RemoveCooldown(CooldownId.Desperado_Equilibrium);
		}

		private void HandleRetreatShot(Skill skill, ICombatEntity caster, Position originPos, Position farPos, Direction castDirection, int targetHandle, ICombatEntity target)
		{
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 1, targetHandle, originPos, castDirection, Position.Zero);
			Send.ZC_SKILL_READY(caster, skill, 1, originPos, originPos);
			this.SendGroundEffects(caster, originPos, castDirection, RetreatGroundEffects);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, originPos, ForceId.GetNew(), null);
			DesperadoSkillHelper.PlaySoundIfKnown(caster, "skl_eff_desperado_equilibrium_back_shot");
			DesperadoSkillHelper.PlayAnimationIfKnown(caster, "SKL_DESPERADO_BACKDASH");
			DesperadoSkillHelper.ApplyCustomEffect(caster);
			DesperadoSkillHelper.LeapCaster(caster, castDirection.Backwards, RetreatLeapDistance);

			var splashParam = GetDamageSplashParameters(caster, caster.Position, castDirection, retreatShot: true);
			var splashArea = skill.GetSplashArea(SplashType.Fan, splashParam);
			var targets = DesperadoSkillHelper.GetTargets(caster, skill, splashArea);
			if (targets.Count == 0 && target != null)
				targets.Add(target);

			skill.Run(this.SendRetreatProjectiles(skill, caster, castDirection, farPos, targets));

			if (caster.IsAbilityActive(AbilityId.Desperado22))
				caster.StartBuff(BuffId.Desperado_Trail, skill.Level, 0, TimeSpan.FromSeconds(3), caster, skill.Id);
		}

		private async Task Attack(Skill skill, ICombatEntity caster, Position originPos, Direction castDirection, bool retreatShot)
		{
			var afterImageDuration = retreatShot ? RetreatAfterImageDuration : ForwardAfterImageDuration;
			await skill.Wait(afterImageDuration);
			Send.ZC_OFF_AFTER_IMAGE(caster);

			if (retreatShot)
				caster.RemoveBuff(BuffId.Equilibrium_toggle_Buff);

			var modifier = DesperadoSkillHelper.BuildModifier(caster, skill, 3, ViolentCost);
			var attackOriginPos = retreatShot ? caster.Position : originPos;
			var splashParam = GetDamageSplashParameters(caster, attackOriginPos, castDirection, retreatShot);
			var splashArea = skill.GetSplashArea(retreatShot ? SplashType.Fan : SplashType.Square, splashParam);
			var targets = DesperadoSkillHelper.GetTargets(caster, skill, splashArea);
			var results = DesperadoSkillHelper.DealDamage(caster, skill, targets, modifier, FirstDamageDelay);

			if (!retreatShot)
			{
				await skill.Wait(ForwardSecondHitDelay);
				SendHitEffects(caster, targets, castDirection);
				results.AddRange(DesperadoSkillHelper.DealDamage(caster, skill, targets, modifier, SecondDamageDelay));
			}
			else
			{
				SendRetreatHitEffects(caster, targets, castDirection);
			}

			DesperadoSkillHelper.ApplyBadGuyOnAccurateHit(caster, skill, results);
		}

		private static SplashParameters GetDamageSplashParameters(ICombatEntity caster, Position originPos, Direction castDirection, bool retreatShot)
		{
			var direction = castDirection;
			var length = retreatShot ? 100f : 80f;

			return new SplashParameters
			{
				Length = length + SizeTypeRadius.GetRadius(caster.EffectiveSize),
				Width = 45f,
				Angle = retreatShot ? 90f : 70f,
				Direction = direction,
				OriginPos = originPos,
				FarPos = originPos.GetRelative(direction, length),
			};
		}

		private static void SendHitEffects(ICombatEntity caster, System.Collections.Generic.IEnumerable<ICombatEntity> targets, Direction castDirection)
		{
			foreach (var target in targets)
			{
				var position = target.Position + new Position(0, 20f, 0);
				var hitSlashRed = "Hit_Slash_Red_01".GetStringId();
				Send.ZC_UNITY_GROUND_EFFECT(caster, hitSlashRed, 0.8f, position, 0f, 0f, 0f, new Direction(castDirection.Cos, 0.2f));
				Send.ZC_UNITY_GROUND_EFFECT(caster, hitSlashRed, 0.8f, position, 0f, 0f, 0f, new Direction(castDirection.Cos, 0.1f));
				Send.ZC_UNITY_GROUND_EFFECT(caster, hitSlashRed, 0.8f, position, 0f, 0f, 0f, new Direction(castDirection.Cos, 0f));
			}
		}

		private static void SendRetreatHitEffects(ICombatEntity caster, System.Collections.Generic.IEnumerable<ICombatEntity> targets, Direction castDirection)
		{
			foreach (var target in targets)
			{
				var position = target.Position + new Position(0, 20f, 0);
				var hitBlowYellow = "Hit_Blow_Yellow_02".GetStringId();
				Send.ZC_UNITY_GROUND_EFFECT(caster, hitBlowYellow, 0.25f, position, 0f, 0f, 0f, new Direction(castDirection.Cos, 0.2f));
				Send.ZC_UNITY_GROUND_EFFECT(caster, hitBlowYellow, 0.25f, position, 0f, 0f, 0f, new Direction(castDirection.Cos, 0.1f));
				Send.ZC_UNITY_GROUND_EFFECT(caster, hitBlowYellow, 0.25f, position, 0f, 0f, 0f, new Direction(castDirection.Cos, 0f));
			}
		}

		private async Task SendRetreatProjectiles(Skill skill, ICombatEntity caster, Direction castDirection, Position farPos, System.Collections.Generic.IReadOnlyList<ICombatEntity> targets)
		{
			var finalShotDirection = caster.Direction != Direction.Zero ? caster.Direction : castDirection;

			for (var i = 0; i < 5; i++)
			{
				var angleOffset = DesperadoSkillHelper.GetConeAngleOffset(i, 5, RetreatProjectileSpreadAngle);
				var projectileDirection = finalShotDirection.AddDegreeAngle(angleOffset);
				DesperadoSkillHelper.SendTrackedBulletProjectile(caster, projectileDirection, distance: 100f);

				if (i < 4)
					await skill.Wait(TimeSpan.FromMilliseconds(RetreatProjectileIntervalMs));
			}
		}

		private void SendGroundEffects(ICombatEntity caster, Position originPos, Direction castDirection, EquilibriumGroundEffect[] effects)
		{
			var castAngle = castDirection.NormalDegreeAngle;

			foreach (var effect in effects)
			{
				var direction = effect.Backwards ? castDirection.Backwards : castDirection;
				var position = originPos.GetRelative(direction, effect.Distance);
				position.Y += effect.HeightOffset;

				var rotationX = effect.UseCastAngleForRotationX ? castAngle + effect.RotationXOffset : effect.RotationXOffset;
				var effectDirection = new Direction(effect.DirectionCos, effect.DirectionSin);

				Send.ZC_UNITY_GROUND_EFFECT(caster, effect.PacketStringName.GetStringId(), effect.Scale, position, rotationX, effect.RotationY, effect.RotationZ, effectDirection, effect.B1);
			}
		}

		private readonly struct EquilibriumGroundEffect
		{
			public string PacketStringName { get; }
			public float Scale { get; }
			public float Distance { get; }
			public float HeightOffset { get; }
			public bool UseCastAngleForRotationX { get; }
			public float RotationXOffset { get; }
			public float RotationY { get; }
			public float RotationZ { get; }
			public float DirectionCos { get; }
			public float DirectionSin { get; }
			public byte B1 { get; }
			public bool Backwards { get; }

			public EquilibriumGroundEffect(string packetStringName, float scale, float distance, float heightOffset, bool useCastAngleForRotationX, float rotationXOffset, float rotationY, float rotationZ, float directionCos, float directionSin, byte b1, bool backwards = false)
			{
				this.PacketStringName = packetStringName;
				this.Scale = scale;
				this.Distance = distance;
				this.HeightOffset = heightOffset;
				this.UseCastAngleForRotationX = useCastAngleForRotationX;
				this.RotationXOffset = rotationXOffset;
				this.RotationY = rotationY;
				this.RotationZ = rotationZ;
				this.DirectionCos = directionCos;
				this.DirectionSin = directionSin;
				this.B1 = b1;
				this.Backwards = backwards;
			}
		}
	}
}
