using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.World;
using Melia.Zone.Buffs;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;
using static Melia.Zone.Skills.Helpers.SkillDamageHelper;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Scouts.Bulletmarker
{
	internal static class BulletmarkerSkillHelper
	{
		private static readonly TimeSpan DefaultBuffDuration = TimeSpan.FromSeconds(60);
		private static readonly TimeSpan OutrageDuration = TimeSpan.FromSeconds(12);
		private const int RequiredOutrageOverheatingStacks = 40;

		public static bool TryStart(Skill skill, ICombatEntity caster, Position originPos, Position farPos)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return false;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);
			Send.ZC_SKILL_READY(caster, skill, ZoneServer.Instance.World.CreateSkillHandle(), originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, originPos, originPos.GetDirection(farPos), Position.Zero);

			return true;
		}

		public static bool TryStartSelf(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return false;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);
			Send.ZC_SKILL_READY(caster, skill, ZoneServer.Instance.World.CreateSkillHandle(), originPos, Position.Zero);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, originPos, dir, Position.Zero);

			return true;
		}

		public static void PlaySelfSkill(ICombatEntity caster, Skill skill)
			=> Send.ZC_SKILL_MELEE_TARGET(caster, skill, caster);

		public static void PlayGroundSkill(ICombatEntity caster, Skill skill, Position farPos)
			=> Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos, ForceId.GetNew(), null);

		public static void PlayTargetSkill(ICombatEntity caster, Skill skill, ICombatEntity target)
			=> Send.ZC_SKILL_FORCE_TARGET(caster, target, skill, null);

		public static TimeSpan GetBuffDuration(Skill skill, double minimumSeconds = 30)
			=> TimeSpan.FromSeconds(minimumSeconds);

		public static TimeSpan GetDefaultBuffDuration()
			=> DefaultBuffDuration;

		public static void StartOutrage(ICombatEntity caster, Skill skill)
		{
			if (!caster.TryGetBuff(BuffId.Overheating_Buff, out var overheatBuff) || overheatBuff.OverbuffCounter < RequiredOutrageOverheatingStacks)
				return;

			caster.RemoveBuff(BuffId.Overheating_Buff);
			caster.StartBuff(BuffId.Outrage_Buff, skill.Level, 0, OutrageDuration, caster, skill.Id);
		}

		public static void StartOutrageEndCooldown(ICombatEntity caster, TimeSpan cooldown)
		{
			if (!caster.TryGetSkill(SkillId.Bulletmarker_Outrage, out var skill))
				return;

			skill.StartCooldown(cooldown);
		}

		public static bool TryConsumeOutrage(ICombatEntity caster)
		{
			return false;
		}

		public static bool HasEnoughOutrageStacks(ICombatEntity caster)
			=> caster.TryGetBuff(BuffId.Overheating_Buff, out var buff) && buff.OverbuffCounter >= RequiredOutrageOverheatingStacks;

		public static void AddOverheating(ICombatEntity caster, SkillId sourceSkillId)
		{
			if (caster.IsBuffActive(BuffId.Outrage_Buff))
				return;

			caster.StartBuff(BuffId.Overheating_Buff, 1, 0, TimeSpan.Zero, caster, sourceSkillId);
		}

		public static SkillModifier CreateModifier(Skill skill, int? hitCount = null)
		{
			var modifier = SkillModifier.MultiHit(Math.Max(1, hitCount ?? skill.Data.MultiHitCount));
			return modifier;
		}

		public static void ApplyTracerBullet(ICombatEntity caster, SkillModifier modifier)
		{
			if (!caster.TryGetSkill(SkillId.Bulletmarker_TracerBullet, out var tracerSkill) || tracerSkill.Level <= 0)
				return;

			modifier.HitRateMultiplier += 0.05f * tracerSkill.Level;
			modifier.MinCritChance = Math.Max(modifier.MinCritChance, Math.Min(50, 5 + tracerSkill.Level));
		}

		public static async Task<List<SkillHitInfo>> AttackArea(Skill skill, ICombatEntity caster, Position originPos, Position farPos, SkillModifier modifier, int length = 90, int width = 70, int angle = 30, SplashType splashType = SplashType.Circle, Action<ICombatEntity>? afterHit = null, TimeSpan? animationTime = null)
		{
			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, length: length, width: width, angle: angle);
			var splashArea = skill.GetSplashArea(splashType, splashParam);
			var aniTime = TimeSpan.Zero;
			var skillHitDelay = TimeSpan.Zero;

			var hits = new List<SkillHitInfo>();
			var targets = caster.Map.GetAttackableEnemiesIn(caster, splashArea);

			foreach (var target in targets.LimitBySDR(caster, skill))
			{
				var hitModifier = CloneModifier(modifier);
				ApplyTracerBullet(caster, hitModifier);

				var skillHitResult = SCR_SkillHit(caster, target, skill, hitModifier);
				target.TakeDamage(skillHitResult.Damage, caster);
				afterHit?.Invoke(target);

				var hit = CreateImmediateHit(caster, target, skill, skillHitResult, aniTime, skillHitDelay);
				hit.HitEffect = HitEffect.Impact;
				hits.Add(hit);
			}

			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos, hits);
			await Task.CompletedTask;
			return hits;
		}

		public static async Task<SkillHitInfo?> AttackTarget(Skill skill, ICombatEntity caster, ICombatEntity target, SkillModifier modifier, Action<ICombatEntity, SkillHitResult>? afterHit = null)
		{
			if (target == null)
			{
				Send.ZC_SKILL_FORCE_TARGET(caster, null, skill, null);
				return null;
			}

			if (!caster.InSkillUseRange(skill, target))
			{
				caster.ServerMessage(Localization.Get("Too far away."));
				Send.ZC_SKILL_FORCE_TARGET(caster, null, skill, null);
				return null;
			}

			caster.TurnTowards(target);

			var hitModifier = CloneModifier(modifier);
			ApplyTracerBullet(caster, hitModifier);

			var skillHitResult = SCR_SkillHit(caster, target, skill, hitModifier);
			target.TakeDamage(skillHitResult.Damage, caster);
			afterHit?.Invoke(target, skillHitResult);

			var hit = CreateImmediateHit(caster, target, skill, skillHitResult, TimeSpan.Zero, TimeSpan.Zero);
			Send.ZC_SKILL_FORCE_TARGET(caster, target, skill, hit);

			await Task.CompletedTask;
			return hit;
		}

		public static async Task<List<SkillHitInfo>> AttackBloodyOverdrive(Skill skill, ICombatEntity caster, Position farPos, SkillModifier modifier, int totalShots, int ricochetChancePercent, TimeSpan? animationTime = null)
		{
			var radius = Math.Max(30, skill.Data.SplashRange * 4);
			var splashArea = new Circle(caster.Position, radius);
			var targets = caster.Map.GetAttackableEnemiesIn(caster, splashArea).LimitBySDR(caster, skill).ToList();
			var hits = new List<SkillHitInfo>();

			if (targets.Count == 0)
			{
				Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos, hits);
				await Task.CompletedTask;
				return hits;
			}

			var distributedShots = new Dictionary<ICombatEntity, int>();
			for (var i = 0; i < totalShots; ++i)
			{
				var target = targets[Random.Shared.Next(targets.Count)];
				distributedShots.TryGetValue(target, out var currentHits);
				distributedShots[target] = currentHits + 1;
			}

			var aniTime = TimeSpan.Zero;
			var ricochetHits = new Dictionary<ICombatEntity, int>();

			foreach (var entry in distributedShots)
			{
				var target = entry.Key;
				var targetHits = entry.Value;
				var hitModifier = CloneModifier(modifier);
				hitModifier.HitCount = targetHits;
				ApplyTracerBullet(caster, hitModifier);

				var result = SCR_SkillHit(caster, target, skill, hitModifier);
				target.TakeDamage(result.Damage, caster);
				var hit = CreateImmediateHit(caster, target, skill, result, aniTime, TimeSpan.Zero);
				hit.HitEffect = HitEffect.Impact;
				hits.Add(hit);

				for (var i = 0; i < targetHits; ++i)
					TryQueueRicochet(caster, target, targets, ricochetHits, ricochetChancePercent);
			}

			foreach (var entry in ricochetHits)
			{
				var target = entry.Key;
				var hitModifier = CloneModifier(modifier);
				hitModifier.HitCount = entry.Value;
				hitModifier.FinalDamageMultiplier *= 0.5f;
				ApplyTracerBullet(caster, hitModifier);

				var result = SCR_SkillHit(caster, target, skill, hitModifier);
				target.TakeDamage(result.Damage, caster);
				var hit = CreateImmediateHit(caster, target, skill, result, TimeSpan.Zero, TimeSpan.Zero);
				hit.HitEffect = HitEffect.Impact;
				hits.Add(hit);
			}

			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos, hits);
			await Task.CompletedTask;
			return hits;
		}

		public static void TryRicochet(ICombatEntity caster, Skill skill, ICombatEntity firstTarget, SkillModifier modifier, int chancePercent)
		{
			if (chancePercent <= 0 || Random.Shared.Next(100) >= chancePercent)
				return;

			var area = new Circle(firstTarget.Position, 90);
			foreach (var target in caster.Map.GetAttackableEnemiesIn(caster, area))
			{
				if (target == firstTarget)
					continue;

				var ricochetModifier = CloneModifier(modifier);
				ricochetModifier.FinalDamageMultiplier *= 0.5f;

				var result = SCR_SkillHit(caster, target, skill, ricochetModifier);
				target.TakeDamage(result.Damage, caster);
				Send.ZC_SKILL_HIT_INFO(caster, new List<SkillHitInfo>
				{
					new(caster, target, skill, result, TimeSpan.FromMilliseconds(180), TimeSpan.Zero)
				});
				return;
			}
		}

		private static void TryQueueRicochet(ICombatEntity caster, ICombatEntity sourceTarget, List<ICombatEntity> originalTargets, Dictionary<ICombatEntity, int> ricochetHits, int chancePercent)
		{
			if (chancePercent <= 0 || Random.Shared.Next(100) >= chancePercent)
				return;

			var ricochetArea = new Circle(sourceTarget.Position, 55);
			var candidates = caster.Map.GetAttackableEnemiesIn(caster, ricochetArea)
				.Where(target => target != sourceTarget)
				.ToList();

			if (candidates.Count == 0)
				return;

			var target = candidates[Random.Shared.Next(candidates.Count)];
			ricochetHits.TryGetValue(target, out var currentHits);
			ricochetHits[target] = currentHits + 1;
		}

		private static SkillModifier CloneModifier(SkillModifier source)
		{
			return new SkillModifier
			{
				BonusPAtk = source.BonusPAtk,
				BonusMAtk = source.BonusMAtk,
				BonusDamage = source.BonusDamage,
				HitRateMultiplier = source.HitRateMultiplier,
				BlockPenetrationMultiplier = source.BlockPenetrationMultiplier,
				BonusDodgeChance = source.BonusDodgeChance,
				DefenseBonus = source.DefenseBonus,
				DefensePenetrationRate = source.DefensePenetrationRate,
				DamageMultiplier = source.DamageMultiplier,
				CritRateMultiplier = source.CritRateMultiplier,
				CritDamageMultiplier = source.CritDamageMultiplier,
				MinCritChance = source.MinCritChance,
				MaxCritChance = source.MaxCritChance,
				CritChanceMultiplier = source.CritChanceMultiplier,
				CritHitRateMultiplier = source.CritHitRateMultiplier,
				CritDodgeRateMultiplier = source.CritDodgeRateMultiplier,
				BonusCritChance = source.BonusCritChance,
				SkillFactorBonus = source.SkillFactorBonus,
				FinalDamageMultiplier = source.FinalDamageMultiplier,
				HitCount = source.HitCount,
				Unblockable = source.Unblockable,
				ForcedBlock = source.ForcedBlock,
				ForcedHit = source.ForcedHit,
				ForcedEvade = source.ForcedEvade,
				ForcedCritical = source.ForcedCritical,
				ForcedBackAttack = source.ForcedBackAttack,
				BackAttackDamageBonus = source.BackAttackDamageBonus,
				AttackAttribute = source.AttackAttribute,
				DefenseAttribute = source.DefenseAttribute,
				AttackType = source.AttackType,
				DefenseArmorType = source.DefenseArmorType,
			};
		}

		private static SkillHitInfo CreateImmediateHit(ICombatEntity caster, ICombatEntity target, Skill skill, SkillHitResult result, TimeSpan aniTime, TimeSpan hitDelay)
		{
			var hit = new SkillHitInfo(caster, target, skill, result, aniTime, hitDelay);
			hit.ForceId = 0;
			return hit;
		}
	}
}
