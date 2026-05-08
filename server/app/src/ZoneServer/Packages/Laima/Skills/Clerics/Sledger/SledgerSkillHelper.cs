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
using Melia.Zone.World.Actors.CombatEntities.Components;
using Yggdrasil.Util;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Clerics.Sledger
{
	internal static class SledgerSkillHelper
	{
		public const float FightingSpiritFinalDamageBonus = 0.05f;
		public const float RollingPowerFinalDamageBonus = 0.25f;
		public const float SledgehammerDefenseIgnorePerLevel = 0.02f;
		public const float SledgehammerMaxDefenseIgnore = 0.15f;
		public const float ChannelCancelMoveDistance = 18f;
		public const BuffId RollingPowerBuffId = BuffId.Sledger_RollingPower_Buff;
		public static readonly TimeSpan FightingSpiritDuration = TimeSpan.FromSeconds(30);
		public static readonly TimeSpan WarmupDuration = TimeSpan.FromSeconds(10);
		public static readonly TimeSpan RollingPowerDuration = TimeSpan.FromSeconds(10);

		public static bool TryStart(Skill skill, ICombatEntity caster, Position originPos, Position farPos, bool allowMovement = false, Position? visualPos = null, bool sendMeleeGround = true)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return false;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(!allowMovement);
			var targetPos = visualPos ?? farPos;
			Send.ZC_SKILL_READY(caster, skill, ZoneServer.Instance.World.CreateSkillHandle(), originPos, targetPos);
			if (sendMeleeGround)
				Send.ZC_SKILL_MELEE_GROUND(caster, skill, targetPos, ForceId.GetNew(), null);

			return true;
		}

		public static List<SkillHitInfo> AttackArea(Skill skill, ICombatEntity caster, ISplashArea splashArea, int hitCount, bool appliesBigBangReduction, int bigBangReductionSeconds, float damageMultiplier = 1f)
		{
			var hits = new List<SkillHitInfo>();
			var targets = caster.Map.GetAttackableEnemiesIn(caster, splashArea, hitType: skill.Data.HitType);
			var modifier = CreateSledgerModifier(skill, caster, Math.Max(1, hitCount), damageMultiplier);

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
			{
				Send.ZC_SKILL_HIT_INFO(caster, hits);

				if (appliesBigBangReduction)
					ReduceBigBangCooldown(caster, TimeSpan.FromSeconds(bigBangReductionSeconds * Math.Max(1, hitCount)));
			}

			return hits;
		}

		public static void RunCancellable(Skill skill, Func<Task> taskFactory)
		{
			skill.PrepareCancellation();
			skill.Run(taskFactory());
		}

		public static async Task AttackAreaOverTime(Skill skill, ICombatEntity caster, Func<ISplashArea> splashAreaFactory, int hitCount, TimeSpan duration, bool appliesBigBangReduction, int bigBangReductionSeconds)
		{
			hitCount = Math.Max(1, hitCount);
			var firstHitDelay = TimeSpan.FromMilliseconds(Math.Min(180, Math.Max(80, duration.TotalMilliseconds / hitCount)));
			var remainingDuration = TimeSpan.FromMilliseconds(Math.Max(0, duration.TotalMilliseconds - firstHitDelay.TotalMilliseconds));
			var tickDelay = TimeSpan.FromMilliseconds(Math.Max(90, remainingDuration.TotalMilliseconds / Math.Max(1, hitCount - 1)));
			var startPosition = caster.Position;

			try
			{
				for (var i = 0; i < hitCount; ++i)
				{
					await skill.Wait(i == 0 ? firstHitDelay : tickDelay);

					if (caster.IsDead || caster.IsJumping() || caster.Position.Get2DDistance(startPosition) > ChannelCancelMoveDistance)
						break;

					AttackArea(skill, caster, splashAreaFactory(), 1, appliesBigBangReduction, bigBangReductionSeconds);
				}
			}
			finally
			{
				ResetSledgerAction(caster, skill);
			}
		}

		public static async Task DelayedAttackArea(Skill skill, ICombatEntity caster, ISplashArea splashArea, TimeSpan delay, bool appliesBigBangReduction, int bigBangReductionSeconds)
		{
			try
			{
				await skill.Wait(delay);

				if (!caster.IsDead && !caster.IsJumping())
					AttackArea(skill, caster, splashArea, skill.Data.MultiHitCount, appliesBigBangReduction, bigBangReductionSeconds);
			}
			finally
			{
				ResetSledgerAction(caster, skill);
			}
		}

		public static void ResetSledgerAction(ICombatEntity caster, Skill skill)
		{
			Send.ZC_SKILL_DISABLE(caster);
			Send.ZC_NORMAL.SkillCancel(caster, skill.Id);
			Send.ZC_NORMAL.SkillCancelCancel(caster, skill.Id);
			Send.ZC_NORMAL.StopAnimation(caster);
			Send.ZC_NORMAL.ResetStdAnim(caster);
			Send.ZC_NORMAL.ResetRunAnim(caster);
			caster.SetAttackState(false);
		}

		public static void ReduceBigBangCooldown(ICombatEntity caster, TimeSpan reduction)
		{
			caster.Components.Get<CooldownComponent>()?.ReduceCooldown(CooldownId.Sledger_BigBang, reduction);
		}

		public static SkillModifier CreateSledgerModifier(Skill skill, ICombatEntity caster, int hitCount, float damageMultiplier = 1f)
		{
			var modifier = SkillModifier.MultiHit(hitCount);
			modifier.DamageMultiplier *= damageMultiplier;

			var sledgehammerLevel = GetSledgehammerLevel(caster);
			if (sledgehammerLevel > 0)
			{
				var ignoreDefense = Math.Min(SledgehammerMaxDefenseIgnore, SledgehammerDefenseIgnorePerLevel * sledgehammerLevel);
				modifier.DefensePenetrationRate += ignoreDefense;
			}

			if (skill.Id != SkillId.Sledger_BigBang_Cleric && IsSledgerDamageSkill(skill.Id))
			{
				if (caster.IsBuffActive(BuffId.Sledger_Fighting_Buff))
					modifier.FinalDamageMultiplier += FightingSpiritFinalDamageBonus;

				if (caster.IsBuffActive(RollingPowerBuffId) || caster.IsBuffActive(BuffId.Sledger_RollingPower_Buff))
					modifier.FinalDamageMultiplier += RollingPowerFinalDamageBonus;

				if (caster.IsBuffActive(BuffId.Sledger_Preheat_Buff))
				{
					var warmupLevel = Math.Max(1, caster.GetAbilityLevel(AbilityId.Sledger14));
					modifier.MinCritChance = Math.Max(modifier.MinCritChance, Math.Min(50, warmupLevel * 2));
				}
			}

			return modifier;
		}

		public static int GetSledgehammerLevel(ICombatEntity caster)
		{
			if (caster.TryGetSkill(SkillId.Sledger_Sledgehammer_Cleric, out var skill))
				return skill.Level;

			return 0;
		}

		public static bool IsSledgerDamageSkill(SkillId skillId)
		{
			return skillId == SkillId.Sledger_HeavySmashing_Cleric
				|| skillId == SkillId.Sledger_RollingHammer_Cleric
				|| skillId == SkillId.Sledger_ChargeHammer_Cleric
				|| skillId == SkillId.Sledger_SwingHammer_Cleric;
		}

		public static ISplashArea CreateSquare(Skill skill, ICombatEntity caster, Position originPos, Position farPos, int length, int width)
		{
			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, length: length, width: width, angle: 0f);
			return skill.GetSplashArea(SplashType.Square, splashParam);
		}

		public static ISplashArea CreateImpactSquare(Skill skill, ICombatEntity caster, Position impactPos, int length, int width)
		{
			var originPos = impactPos.GetRelative(caster.Direction.Backwards, length * 0.5f);
			var splashParam = skill.GetSplashParameters(caster, originPos, impactPos, length: length, width: width, angle: 0f);
			return skill.GetSplashArea(SplashType.Square, splashParam);
		}

		public static ISplashArea CreateCircle(Skill skill, ICombatEntity caster, Position center, int radius)
		{
			var splashParam = skill.GetSplashParameters(caster, center, center, length: 0, width: radius, angle: 0f);
			return skill.GetSplashArea(SplashType.Circle, splashParam);
		}

		public static Position GetImpactPosition(ICombatEntity caster, Position farPos, float distance)
		{
			return caster.Position.GetRelative(caster.Direction, distance);
		}

		public static TimeSpan GetSkillActionDuration(Skill skill, int fallbackMilliseconds)
		{
			var duration = skill.Data.ShootTime;

			if (skill.Data.HoldTime != null && skill.Data.HoldTime.Count > 0)
				duration = new[] { duration }.Concat(skill.Data.HoldTime).Max();

			if (skill.Data.CancelTime > duration)
				duration = skill.Data.CancelTime;

			if (duration <= TimeSpan.Zero)
				duration = TimeSpan.FromMilliseconds(fallbackMilliseconds);

			return duration;
		}

		public static Position MoveCasterForward(ICombatEntity caster, Position farPos, float maxDistance)
		{
			var distance = (float)Math.Min(maxDistance, Math.Max(35, caster.Position.Get2DDistance(farPos)));
			var destination = caster.Position.GetRelative(caster.Direction, distance);

			if (caster.Map.Ground.TryGetNearestValidPosition(destination, out var validPosition))
				destination = validPosition;

			caster.SetPosition(destination);
			Send.ZC_NORMAL.LeapJump(caster, destination, 0f, 0.1f, 0.05f, 0.15f, 0.25f, 3f);
			return destination;
		}

		public static async Task ReturnToPosition(Skill skill, ICombatEntity caster, Position position, TimeSpan delay)
		{
			await skill.Wait(delay);

			if (caster.IsDead)
				return;

			if (caster.Map.Ground.TryGetNearestValidPosition(position, out var validPosition))
				caster.SetPosition(validPosition);
			else
				caster.SetPosition(position);
		}

		public static void PullNearbyTargets(ICombatEntity caster, Position center, int radius, int maxTargets)
		{
			var area = new Circle(center, radius);
			var targets = caster.Map.GetAttackableEnemiesIn(caster, area, maxTargets);

			foreach (var target in targets)
			{
				var pulledPosition = caster.Position.GetRelative(caster.Direction, 18);
				if (caster.Map.Ground.TryGetNearestValidPosition(pulledPosition, out var validPosition))
					target.SetPosition(validPosition);
			}
		}
	}
}
