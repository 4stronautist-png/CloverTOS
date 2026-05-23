using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.Game.Const;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Network.Helpers;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.CombatEntities.Components;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Scouts.Desperado
{
	public static class DesperadoSkillHelper
	{
		private static readonly TimeSpan BadGuyBuffDuration = TimeSpan.FromSeconds(4);
		private static readonly TimeSpan BadGuyMaxCooldownReduction = TimeSpan.FromSeconds(15);

		private static readonly HashSet<SkillId> DesperadoDamageSkills =
		[
			SkillId.Desperado_Equilibrium,
			SkillId.Desperado_Revenged,
			SkillId.Desperado_DeadlyFire,
			SkillId.Desperado_LastManStanding,
		];

		public static bool IsDesperadoDamageSkill(SkillId skillId)
			=> DesperadoDamageSkills.Contains(skillId);

		public static SkillModifier BuildModifier(ICombatEntity caster, Skill skill, int hitCount, int violentCost)
		{
			var modifier = SkillModifier.MultiHit(hitCount);

			if (TryGetBadGuySkill(caster, out var badGuySkill))
				modifier.DamageMultiplier *= 1f + 0.05f * badGuySkill.Level;

			if (TryConsumeViolent(caster, violentCost))
				modifier.SkillFactorBonus += GetViolentSkillFactorBonus(caster);

			return modifier;
		}

		public static void ApplyBadGuyOnAccurateHit(ICombatEntity caster, Skill skill, IEnumerable<SkillHitResult> results)
		{
			if (!IsDesperadoDamageSkill(skill.Id))
				return;

			var materializedResults = results as IList<SkillHitResult> ?? results.ToList();

			if (TryGetBadGuySkill(caster, out var badGuySkill))
			{
				ApplyBadGuyCooldownReduction(caster, materializedResults, badGuySkill.Level);

				if (caster.IsAbilityActive(AbilityId.Desperado20) && materializedResults.Any(result => result.Damage > 0 && result.Result != HitResultType.Dodge))
					caster.StartBuff(BuffId.Desperado20_Buff, skill.Level, 0, BadGuyBuffDuration, caster, SkillId.Desperado_BadGuy);
			}
		}

		public static void ApplyMaxBadGuyStacks(ICombatEntity caster, Skill skill)
		{
			if (!caster.IsAbilityActive(AbilityId.Desperado20))
				return;

			caster.StartBuff(BuffId.Desperado20_Buff, skill.Level, 0, BadGuyBuffDuration, caster, SkillId.Desperado_BadGuy, buff => buff.OverbuffCounter = 5);
		}

		public static List<ICombatEntity> GetTargets(ICombatEntity caster, Skill skill, ISplashArea splashArea)
			=> caster.Map.GetAttackableEnemiesIn(caster, splashArea, hitType: skill.Data.HitType).LimitBySDR(caster, skill).ToList();

		public static void SendSkillEffect(ICombatEntity caster, ICombatEntity target, Position originPos, Position farPos)
		{
			var targetHandle = target?.Handle ?? 0;
			Send.ZC_NORMAL.UpdateSkillEffect(caster, targetHandle, originPos, originPos.GetDirection(farPos), farPos);
		}

		public static Position MoveCaster(ICombatEntity caster, Direction direction, float distance, float moveTime = 0.2f)
		{
			var from = caster.Position;
			var destination = caster.Position.GetRelative(direction, distance);
			destination = caster.Map.Ground.GetLastValidPosition(caster.Position, destination);
			caster.Position = destination;
			Send.ZC_MOVE_POS(caster, from, destination, 220, moveTime);
			return destination;
		}

		public static Position LeapCaster(ICombatEntity caster, Direction direction, float distance, float height = 0.15f, float moveTime = 0.2f)
		{
			var destination = caster.Position.GetRelative(direction, distance);
			destination = caster.Map.Ground.GetLastValidPosition(caster.Position, destination);
			caster.Position = destination;
			Send.ZC_NORMAL.LeapJump(caster, destination, height, 0.1f, 0.1f, moveTime, 0.1f, 3);
			return destination;
		}

		public static Position MoveCasterAroundTarget(ICombatEntity caster, ICombatEntity target, Direction inputDirection, Position fallbackPos)
		{
			if (target == null)
			{
				var fallbackDirection = inputDirection;
				if (fallbackDirection == Direction.Zero)
					fallbackDirection = caster.Position.GetDirection(fallbackPos);
				if (fallbackDirection == Direction.Zero)
					fallbackDirection = caster.Direction;

				return LeapCaster(caster, fallbackDirection, 80, moveTime: 0.25f);
			}

			var directionToTarget = caster.Position.GetDirection(target.Position);
			var delta = NormalizeSignedAngle(inputDirection.DegreeAngle - directionToTarget.DegreeAngle);
			var directionFromTarget = target.Position.GetDirection(caster.Position);

			Direction offsetDirection;
			if (Math.Abs(delta) <= 45)
				offsetDirection = target.Direction.Backwards;
			else if (delta > 0)
				offsetDirection = directionFromTarget.Right;
			else
				offsetDirection = directionFromTarget.Left;

			var from = caster.Position;
			var destination = target.Position.GetRelative(offsetDirection, 45);
			destination = caster.Map.Ground.GetLastValidPosition(caster.Position, destination);
			caster.Position = destination;
			Send.ZC_MOVE_POS(caster, from, destination, 260, 0.25f);
			caster.TurnTowards(target);
			return destination;
		}

		public static void ApplyCustomEffect(ICombatEntity caster)
			=> PlayEffectIfKnown(caster, "SKL_DESPERADO_SHOT1");

		public static void PlayAnimationIfKnown(ICombatEntity caster, string animationName, bool stopOnLastFrame = false)
		{
			if (IsKnownPacketString(animationName))
				caster.PlayAnimation(animationName, stopOnLastFrame);
		}

		public static void AttachLastManStandingAura(ICombatEntity caster)
		{
			if (IsKnownPacketString("BodyAura_Dark_Red_01"))
				caster.AttachEffect("BodyAura_Dark_Red_01", 1.2f, EffectLocation.Middle);
		}

		public static void DetachLastManStandingAura(ICombatEntity caster)
		{
			if (IsKnownPacketString("BodyAura_Dark_Red_01"))
				Send.ZC_NORMAL.RemoveEffectByName(caster, "BodyAura_Dark_Red_01", true);
		}

		public static void SendDesperadoAnim(Character caster, int value)
			=> Send.ZC_SEND_PC_EXPROP(caster, new MsgParameter("DESPERADO_ANIM", value));

		public static void SendOrangeMuzzleLight(ICombatEntity caster, Position originPos, Direction direction, float scale = 0.5f, float spread = 0.55f)
		{
			var muzzlePosition = originPos.GetRelative(direction, 20f);
			muzzlePosition.Y += 20f;
			Send.ZC_UNITY_GROUND_EFFECT(caster, "Shoot_DarkMuzzleFlash_Orange_03".GetStringId(), scale, muzzlePosition, direction.NormalDegreeAngle + 90f, 0f, 0f, new Direction(0f, spread));
		}

		public static void SendOrangeMuzzleLight(ICombatEntity caster, Direction direction, float scale = 0.5f, float spread = 0.55f)
			=> SendOrangeMuzzleLight(caster, caster.Position, direction, scale, spread);

		public static void SendOrangeMuzzleLightAtPosition(ICombatEntity caster, Position muzzlePosition, Direction direction, float scale = 0.5f, float spread = 0.55f)
			=> Send.ZC_UNITY_GROUND_EFFECT(caster, "Shoot_DarkMuzzleFlash_Orange_03".GetStringId(), scale, muzzlePosition, direction.NormalDegreeAngle + 90f, 0f, 0f, new Direction(0f, spread));

		private static Position GetProjectileStartPosition(ICombatEntity sender, Direction direction)
		{
			var muzzleDistance = 1f;
			var heightOffsetY = 10f;
			var heightOffsetZ = 0f;
			return sender.Position.GetRelative(direction, muzzleDistance);
		}

		private static Position GetProjectileEndPosition(ICombatEntity sender, Direction direction, Position startPos, float distance)
		{
			var endPos = sender.Position.GetRelative(direction, distance);
			var zOffset = startPos.Z - sender.Position.GetRelative(direction, 10f).Z;
			return new Position(endPos.X, startPos.Y, endPos.Z + zOffset);
		}

		public static void SendTrackedBulletProjectile(ICombatEntity caster, float scale = 0.8f, float flightTime = 0.3f)
			=> SendTrackedBulletProjectile(caster, caster.Direction, scale, flightTime);

		public static void SendTrackedBulletProjectile(ICombatEntity caster, Direction direction, float scale = 0.8f, float flightTime = 0.5f, float distance = 200f)
		{
			var startPos = GetProjectileStartPosition(caster, direction);
			var endPos = GetProjectileEndPosition(caster, direction, startPos, distance);
			Send.ZC_NORMAL.SkillProjectile(caster, endPos, "Projectile_Bullet_Yellow_01", scale, "None", 0f, 120f, TimeSpan.FromSeconds(flightTime));
		}

		public static float GetTrackedBulletDistance(ICombatEntity caster, ICombatEntity target, float fallbackDistance = 150f)
		{
			if (target == null)
				return fallbackDistance;

			var targetDistance = (float)caster.Position.Get2DDistance(target.Position);
			if (targetDistance <= 0f)
				return fallbackDistance;

			return targetDistance * 1.1f;
		}

		public static void PlaySoundIfKnown(ICombatEntity caster, string soundName)
		{
			if (IsKnownPacketString(soundName))
				caster.PlaySound(soundName);
		}

		public static void PlayEffectIfKnown(ICombatEntity caster, string effectName, float scale = 1f, EffectLocation heightOffset = EffectLocation.Bottom)
		{
			if (IsKnownPacketString(effectName))
				caster.PlayEffect(effectName, scale, heightOffset: heightOffset);
		}

		public static void PlayEffectNodeIfKnown(ICombatEntity caster, string effectName, float duration, string nodeName)
		{
			if (IsKnownPacketString(effectName))
				caster.PlayEffectNode(effectName, duration, nodeName);
		}

		private static bool IsKnownPacketString(string packetString)
			=> packetString == null || packetString == "None" || ZoneServer.Instance.Data.PacketStringDb.TryFind(packetString, out _);

		private static float NormalizeSignedAngle(float angle)
		{
			angle %= 360;
			if (angle > 180)
				angle -= 360;
			if (angle < -180)
				angle += 360;
			return angle;
		}

		public static float GetConeAngleOffset(int shotIndex, int shotCount, float totalSpreadAngle)
		{
			if (shotCount <= 1 || totalSpreadAngle <= 0f)
				return 0f;

			var step = totalSpreadAngle / (shotCount - 1);
			return -(totalSpreadAngle / 2f) + (step * shotIndex);
		}

		public static List<SkillHitResult> DealDamage(ICombatEntity caster, Skill skill, IEnumerable<ICombatEntity> targets, SkillModifier modifier, TimeSpan damageDelay = default)
		{
			var results = new List<SkillHitResult>();

			foreach (var target in targets)
			{
				if (target == null || target.IsDead || !caster.CanDamage(target))
					continue;

				var skillHitResult = SCR_SkillHit(caster, target, skill, modifier);
				target.TakeDamage(skillHitResult.Damage, caster);
				results.Add(skillHitResult);

				var hitInfo = new HitInfo(caster, target, skill, skillHitResult, damageDelay);
				Send.ZC_HIT_INFO(caster, target, hitInfo);
			}

			return results;
		}

		public static void ResetCoreCooldowns(ICombatEntity caster)
		{
			caster.RemoveCooldown(CooldownId.Desperado_Equilibrium);
			caster.RemoveCooldown(CooldownId.Desperado_Revenged);
		}

		private static bool TryConsumeViolent(ICombatEntity caster, int cost)
		{
			if (cost <= 0)
				return false;

			if (!caster.TryGetBuff(BuffId.Violent, out var buff) || buff.OverbuffCounter < cost)
				return false;

			buff.OverbuffCounter -= cost;

			if (buff.OverbuffCounter <= 0)
			{
				caster.RemoveBuff(BuffId.Violent);
			}
			else
			{
				caster.Components.Get<BuffComponent>()?.NotifyBuffUpdate(buff);
			}

			return true;
		}

		private static float GetViolentSkillFactorBonus(ICombatEntity caster)
		{
			var bonus = 2f;
			if (caster.TryGetSkill(SkillId.Desperado_RussianRoulette, out var russianRoulette))
				bonus = 2f * russianRoulette.Level;

			if (caster.IsAbilityActive(AbilityId.Desperado24))
				bonus *= 2;

			return bonus;
		}

		private static bool TryGetBadGuySkill(ICombatEntity caster, out Skill badGuySkill)
			=> caster.TryGetSkill(SkillId.Desperado_BadGuy, out badGuySkill);

		private static void ApplyBadGuyCooldownReduction(ICombatEntity caster, IEnumerable<SkillHitResult> results, int badGuyLevel)
		{
			var critCount = results.Count(result => result.Result == HitResultType.Crit);
			if (critCount <= 0)
				return;

			var reductionPerCrit = TimeSpan.FromSeconds(0.2f * badGuyLevel);
			var totalReduction = TimeSpan.FromTicks(Math.Min((reductionPerCrit.Ticks * critCount), BadGuyMaxCooldownReduction.Ticks));

			TryReduceCooldown(caster, SkillId.Desperado_DeadlyFire, totalReduction);
			TryReduceCooldown(caster, SkillId.Desperado_LastManStanding, totalReduction);
		}

		private static void TryReduceCooldown(ICombatEntity caster, SkillId skillId, TimeSpan reduction)
		{
			if (reduction <= TimeSpan.Zero)
				return;

			if (!caster.TryGetSkill(skillId, out var skill) || !skill.IsOnCooldown)
				return;

			skill.ReduceCooldown(reduction);
		}
	}
}
