using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.Game.Const;
using Melia.Shared.World;
using Melia.Zone;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.CombatEntities.Components;
using Melia.Zone.World.Actors.Effects;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Scouts.Desperado
{
	public static class DesperadoSkillHelper
	{
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

			if (TryConsumeViolent(caster, violentCost))
				modifier.SkillFactorBonus += GetViolentSkillFactorBonus(caster);

			return modifier;
		}

		public static void ApplyBadGuyOnAccurateHit(ICombatEntity caster, Skill skill, IEnumerable<SkillHitResult> results)
		{
			if (!IsDesperadoDamageSkill(skill.Id) || !caster.IsAbilityActive(AbilityId.Desperado20))
				return;

			if (results.Any(result => result.Damage > 0 && result.Result != HitResultType.Dodge))
				caster.StartBuff(BuffId.Desperado20_Buff, skill.Level, 0, TimeSpan.FromSeconds(5), caster, SkillId.Desperado_BadGuy);
		}

		public static void ApplyMaxBadGuyStacks(ICombatEntity caster, Skill skill)
		{
			if (!caster.IsAbilityActive(AbilityId.Desperado20))
				return;

			caster.StartBuff(BuffId.Desperado20_Buff, skill.Level, 0, TimeSpan.FromSeconds(5), caster, SkillId.Desperado_BadGuy, buff => buff.OverbuffCounter = 5);
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
				return LeapCaster(caster, caster.Position.GetDirection(fallbackPos), 80, moveTime: 0.25f);

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

		public static List<SkillHitResult> DealDamage(ICombatEntity caster, Skill skill, IEnumerable<ICombatEntity> targets, SkillModifier modifier)
		{
			var results = new List<SkillHitResult>();

			foreach (var target in targets)
			{
				if (target == null || target.IsDead || !caster.CanDamage(target))
					continue;

				var skillHitResult = SCR_SkillHit(caster, target, skill, modifier);
				target.TakeDamage(skillHitResult.Damage, caster);
				results.Add(skillHitResult);

				var hitInfo = new HitInfo(caster, target, skill, skillHitResult, TimeSpan.Zero);
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
	}
}
