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
	[SkillHandler(SkillId.Desperado_DeadlyFire)]
	public class Desperado_DeadlyFireOverride : IGroundSkillHandler
	{
		private const short RetailImpactGroundEffectS2 = 10360;
		private const int ViolentCost = 3;
		private static readonly TimeSpan FirstDamageDelay = TimeSpan.FromMilliseconds(100);
		private static readonly TimeSpan SecondDamageDelay = TimeSpan.FromMilliseconds(50);
		private static readonly TimeSpan ProjectileLeadDelay = TimeSpan.FromMilliseconds(300);
		private static readonly TimeSpan ProjectileToImpactDelay = TimeSpan.FromMilliseconds(190);
		private static readonly TimeSpan RetreatProjectileDelay = TimeSpan.FromMilliseconds(70);
		private static readonly DeadlyFireGroundEffect[] OpeningChainEffects =
		{
			new("GroundImpact_IronChain_Grey_01", 0.40f, 120f, -0.10f, 0f, 0f, 0f, 1.00f, 0.40f),
			new("GroundImpact_IronChain_Grey_01", 0.40f, 90f, -0.10f, 0f, 0f, 0f, 1.00f, 0.30f),
			new("GroundImpact_IronChain_Grey_01", 0.40f, 60f, -0.10f, 0f, 0f, 0f, 1.00f, 0.20f),
			new("GroundImpact_IronChain_Grey_01", 0.40f, 30f, -0.10f, 0f, 0f, 0f, 1.00f, 0.10f),
		};

		private static readonly DeadlyFireGroundEffect[] RetreatDustEffects =
		{
			new("Stomp_Dust_LightBrown_01", 0.45f, 135f, 5.00f, 0f, 0f, 0f, 1.00f, 0.40f),
			new("Stomp_Dust_LightBrown_01", 0.50f, 105f, 5.00f, 0f, 0f, 0f, 1.00f, 0.30f),
			new("Stomp_Dust_LightBrown_01", 0.55f, 75f, 5.00f, 0f, 0f, 0f, 1.00f, 0.20f),
			new("Stomp_Dust_LightBrown_01", 0.60f, 45f, -0.10f, 0f, 0f, 0f, 1.00f, 0.10f),
			new("Stomp_Dust_LightBrown_01", 0.70f, 15f, -0.10f, 0f, 0f, 0f, 1.00f, 0.00f),
		};

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			var castDirection = caster.Direction;
			var targetHandle = target?.Handle ?? 0;
			var forceId = ForceId.GetNew();

			skill.IncreaseOverheat();
			caster.SetAttackState(true);
			Send.ZC_SKILL_READY(caster, skill, 1, originPos, originPos);
			this.SendOpeningEffects(caster, originPos, castDirection);
			Send.ZC_PLAY_ANI(caster, "SKL_DESPERADO_DEADLYFIRE", readyTime: 0.2f, animationSpeed: 2f, b1: 1);
			Send.ZC_PLAY_ANI(caster, "SKL_DESPERADO_DASH2", animationSpeed: 2f, b1: 1);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, targetHandle, originPos, castDirection, Position.Zero);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, originPos, forceId, null);

			skill.Run(this.Attack(skill, caster, originPos, farPos, castDirection));
		}

		private async Task Attack(Skill skill, ICombatEntity caster, Position originPos, Position farPos, Direction castDirection)
		{
			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, length: 130f, width: 45f, angle: 35);
			var splashArea = skill.GetSplashArea(SplashType.Square, splashParam);
			var targets = DesperadoSkillHelper.GetTargets(caster, skill, splashArea);

			var modifier = DesperadoSkillHelper.BuildModifier(caster, skill, 3, ViolentCost);
			if (caster.TryGetBuff(BuffId.Desperado_Weight, out _))
			{
				if (caster.TryGetSkillLevel(SkillId.Desperado_Equilibrium, out var equilibriumLevel))
					modifier.SkillFactorBonus += 5f * equilibriumLevel;

				caster.RemoveBuff(BuffId.Desperado_Weight);
			}

			var results = DesperadoSkillHelper.DealDamage(caster, skill, targets, modifier, FirstDamageDelay);

			foreach (var target in targets)
				target.StartBuff(BuffId.DeadlyFire_Debuff, skill.Level, 0, TimeSpan.FromSeconds(1), caster, skill.Id);

			this.SendTargetBindEffects(caster, targets);

			await skill.Wait(ProjectileLeadDelay);
			await this.SendSecondShotProjectiles(skill, caster, targets);

			await skill.Wait(ProjectileToImpactDelay);
			this.SendSecondHitEffects(caster, targets);
			results.AddRange(DesperadoSkillHelper.DealDamage(caster, skill, targets, modifier, SecondDamageDelay));
			DesperadoSkillHelper.ApplyBadGuyOnAccurateHit(caster, skill, results);

			await skill.Wait(RetreatProjectileDelay);
			await this.SendRetreatProjectileEffects(skill, caster, targets);
			Send.ZC_OFF_AFTER_IMAGE(caster);
			this.SendRetreatDustEffects(caster, originPos, castDirection);
			DesperadoSkillHelper.LeapCaster(caster, castDirection.Backwards, 40, moveTime: 0.15f);
			DesperadoSkillHelper.ApplyCustomEffect(caster);
		}

		private void SendOpeningEffects(ICombatEntity caster, Position originPos, Direction castDirection)
		{
			DesperadoSkillHelper.SendOrangeMuzzleLight(caster, originPos, castDirection, scale: 1f);

			this.SendGroundEffects(caster, originPos, castDirection, OpeningChainEffects);

			var slashPosition = originPos + new Position(0, 20f, 0);
			Send.ZC_UNITY_GROUND_EFFECT(caster, "eff_slash_normal_red_01".GetStringId(), 0.7f, slashPosition, 0f, 30f, 0f, new Direction(0.7f, 0f));
		}

		private void SendTargetBindEffects(ICombatEntity caster, System.Collections.Generic.IEnumerable<ICombatEntity> targets)
		{
			foreach (var target in targets)
			{
				var position = target.Position + new Position(0, 10f, 0);
				Send.ZC_UNITY_GROUND_EFFECT(caster, "AerialExplosion_DarkAura_Red_01".GetStringId(), 0.8f, position, 0f, 0f, 0f, new Direction(0.5f, 0f));
				Send.ZC_UNITY_GROUND_EFFECT(caster, "BodyAura_IronChain_Grey_01".GetStringId(), 0.8f, position, 0f, 0f, 0f, new Direction(0.7f, 0f));
			}
		}

		private void SendSecondHitEffects(ICombatEntity caster, System.Collections.Generic.IEnumerable<ICombatEntity> targets)
		{
			foreach (var target in targets)
			{
				var position = target.Position + new Position(0, 10f, 0);
				Send.ZC_UNITY_GROUND_EFFECT(caster, "GroundImpact_FireExplosion_Orange_01".GetStringId(), 0.4f, position, 0f, 0f, 0f, new Direction(1f, 0.25f));
				Send.ZC_UNITY_GROUND_EFFECT(caster, "AerialExplosion_IronChain_Grey_01".GetStringId(), 1f, position, 0f, 0f, 0f, new Direction(1f, 0.2f));
				this.SendRetailImpactGroundEffects(caster, target.Position);
			}
		}

		private async Task SendSecondShotProjectiles(Skill skill, ICombatEntity caster, System.Collections.Generic.IEnumerable<ICombatEntity> targets)
		{
			var targetList = targets as System.Collections.Generic.IReadOnlyList<ICombatEntity> ?? new System.Collections.Generic.List<ICombatEntity>(targets);
			for (var i = 0; i < targetList.Count; i++)
			{
				var shotDirection = caster.Position.GetDirection(targetList[i].Position);
				if (shotDirection == Direction.Zero)
					shotDirection = caster.Direction;

				var shotDistance = DesperadoSkillHelper.GetTrackedBulletDistance(caster, targetList[i]);
				DesperadoSkillHelper.SendTrackedBulletProjectile(caster, shotDirection, distance: shotDistance);
			}

			await skill.Wait(TimeSpan.Zero);
		}

		private async Task SendRetreatProjectileEffects(Skill skill, ICombatEntity caster, System.Collections.Generic.IEnumerable<ICombatEntity> targets)
		{
			Send.ZC_NORMAL.SetActorColor(caster, 255, 255, 255, 255, 0f, 1);
			var targetList = targets as System.Collections.Generic.IReadOnlyList<ICombatEntity> ?? new System.Collections.Generic.List<ICombatEntity>(targets);
			for (var i = 0; i < targetList.Count; i++)
			{
				var shotDirection = caster.Position.GetDirection(targetList[i].Position);
				if (shotDirection == Direction.Zero)
					shotDirection = caster.Direction;

				var shotDistance = DesperadoSkillHelper.GetTrackedBulletDistance(caster, targetList[i]);
				DesperadoSkillHelper.SendTrackedBulletProjectile(caster, shotDirection, distance: shotDistance);
			}

			await skill.Wait(TimeSpan.Zero);
		}

		private void SendRetailImpactGroundEffects(ICombatEntity caster, Position targetPosition)
		{
			Send.ZC_GROUND_EFFECT(caster, targetPosition, "", scale: 1.2f, s2: RetailImpactGroundEffectS2);
			Send.ZC_GROUND_EFFECT(caster, targetPosition, "", scale: 1.2f, s2: RetailImpactGroundEffectS2);
		}

		private void SendRetreatDustEffects(ICombatEntity caster, Position originPos, Direction castDirection)
			=> this.SendGroundEffects(caster, originPos, castDirection, RetreatDustEffects);

		private void SendGroundEffects(ICombatEntity caster, Position originPos, Direction castDirection, DeadlyFireGroundEffect[] effects)
		{
			foreach (var effect in effects)
			{
				var position = originPos.GetRelative(castDirection, effect.Distance);
				position.Y += effect.HeightOffset;

				Send.ZC_UNITY_GROUND_EFFECT(caster, effect.PacketStringName.GetStringId(), effect.Scale, position, effect.RotationX, effect.RotationY, effect.RotationZ, new Direction(effect.DirectionCos, effect.DirectionSin));
			}
		}

		private readonly struct DeadlyFireGroundEffect
		{
			public string PacketStringName { get; }
			public float Scale { get; }
			public float Distance { get; }
			public float HeightOffset { get; }
			public float RotationX { get; }
			public float RotationY { get; }
			public float RotationZ { get; }
			public float DirectionCos { get; }
			public float DirectionSin { get; }

			public DeadlyFireGroundEffect(string packetStringName, float scale, float distance, float heightOffset, float rotationX, float rotationY, float rotationZ, float directionCos, float directionSin)
			{
				this.PacketStringName = packetStringName;
				this.Scale = scale;
				this.Distance = distance;
				this.HeightOffset = heightOffset;
				this.RotationX = rotationX;
				this.RotationY = rotationY;
				this.RotationZ = rotationZ;
				this.DirectionCos = directionCos;
				this.DirectionSin = directionSin;
			}
		}
	}
}
