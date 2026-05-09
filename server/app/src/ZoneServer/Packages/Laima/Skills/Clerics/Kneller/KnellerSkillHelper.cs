using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;
using Yggdrasil.Geometry;
using Yggdrasil.Geometry.Shapes;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Clerics.Kneller
{
	internal static class KnellerSkillHelper
	{
		private const float DeathKnellFinalDamageBonusPerMourningStack = 0.10f;
		private const float DeepResonanceDamageBonusPerLevel = 0.10f;
		private static readonly TimeSpan MourningDuration = TimeSpan.FromMinutes(1);
		private static readonly TimeSpan FrostGraveDuration = TimeSpan.FromSeconds(5);

		public static bool TryStart(Skill skill, ICombatEntity caster, Position originPos, Position farPos)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return false;
			}

			skill.IncreaseOverheat();
			caster.TurnTowards(farPos);
			caster.SetAttackState(true);

			Send.ZC_SKILL_READY(caster, skill, originPos, farPos);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos, null);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, originPos, originPos.GetDirection(farPos), farPos);
			return true;
		}

		public static async Task AttackArea(Skill skill, ICombatEntity caster, IShapeF area, Action<ICombatEntity, SkillModifier> configureHit = null, Action<ICombatEntity, SkillHitResult> afterHit = null, int delayMs = 0, int aniTimeMs = 120, int hitCount = 0, int maxTargets = int.MaxValue)
		{
			await skill.Wait(TimeSpan.FromMilliseconds(delayMs));

			if (caster.IsDead)
				return;

			var hits = new List<SkillHitInfo>();
			var targets = caster.Map.GetAttackableEnemiesIn(caster, area);

			foreach (var target in targets.Take(maxTargets))
			{
				if (target == null || target.IsDead || !caster.CanDamage(target))
					continue;

				var modifier = SkillModifier.MultiHit(Math.Max(1, hitCount > 0 ? hitCount : skill.Data.MultiHitCount));
				ApplyDeepResonance(caster, target, modifier);
				configureHit?.Invoke(target, modifier);

				var result = SCR_SkillHit(caster, target, skill, modifier);
				target.TakeDamage(result.Damage, caster);

				var skillHit = new SkillHitInfo(caster, target, skill, result, TimeSpan.FromMilliseconds(aniTimeMs), skill.Properties.HitDelay)
				{
					HitEffect = HitEffect.Impact
				};

				hits.Add(skillHit);
				afterHit?.Invoke(target, result);
			}

			Send.ZC_SKILL_HIT_INFO(caster, hits);
		}

		public static async Task AttackCircleOverTime(Skill skill, ICombatEntity caster, Func<int, Position> positionFactory, int ticks, int intervalMs, float radius, Action<ICombatEntity, SkillModifier> configureHit = null, Action<ICombatEntity, SkillHitResult> afterHit = null, int hitCountPerTick = 1, string tickEffect = null, float tickEffectScale = 1f, int maxTargets = int.MaxValue)
		{
			for (var i = 0; i < ticks; i++)
			{
				if (caster.IsDead)
					break;

				var position = positionFactory(i);
				PlayGroundEffect(caster, tickEffect, position, tickEffectScale, Math.Max(700, intervalMs));

				await AttackArea(skill, caster, new CircleF(position, radius), configureHit, afterHit, hitCount: hitCountPerTick, maxTargets: maxTargets);

				if (i < ticks - 1)
					await skill.Wait(TimeSpan.FromMilliseconds(intervalMs));
			}
		}

		public static void RunAndReset(Skill skill, ICombatEntity caster, Func<Task> taskFactory)
		{
			skill.PrepareCancellation();
			skill.Run(RunAndResetAsync(skill, caster, taskFactory));
		}

		private static async Task RunAndResetAsync(Skill skill, ICombatEntity caster, Func<Task> taskFactory)
		{
			try
			{
				await taskFactory();
			}
			catch (OperationCanceledException)
			{
			}
			finally
			{
				ResetAction(caster, skill);
			}
		}

		public static void ResetAction(ICombatEntity caster, Skill skill)
		{
			Send.ZC_SKILL_DISABLE(caster);
			Send.ZC_NORMAL.SkillCancel(caster, skill.Id);
			Send.ZC_NORMAL.SkillCancelCancel(caster, skill.Id);
			Send.ZC_NORMAL.StopAnimation(caster);
			Send.ZC_NORMAL.ResetStdAnim(caster);
			Send.ZC_NORMAL.ResetRunAnim(caster);
			caster.SetAttackState(false);
		}

		public static void ApplyMourning(ICombatEntity caster, ICombatEntity target, Skill skill)
			=> target.StartBuff(BuffId.Mourning_Debuff, skill.Level, 0, MourningDuration, caster, skill.Id);

		public static int ConsumeMourningStacks(ICombatEntity target)
		{
			if (!target.TryGetBuff(BuffId.Mourning_Debuff, out var buff))
				return 0;

			var stacks = buff.OverbuffCounter;
			target.RemoveBuff(BuffId.Mourning_Debuff);
			return stacks;
		}

		public static void ApplyDeathKnellBonus(ICombatEntity target, SkillModifier modifier)
		{
			if (!target.TryGetBuff(BuffId.Mourning_Debuff, out var buff))
				return;

			modifier.FinalDamageMultiplier += DeathKnellFinalDamageBonusPerMourningStack * buff.OverbuffCounter;
		}

		public static void ApplyFrostGrave(ICombatEntity caster, ICombatEntity target)
		{
			target.StartBuff(BuffId.GraveChill_Debuff, 1, 0, FrostGraveDuration, caster);
			target.PlayEffect("I_sys_target001_circle", 1.5f);
		}

		public static bool ConsumeFrostGrave(ICombatEntity caster, ICombatEntity target)
		{
			if (!target.IsBuffActive(BuffId.GraveChill_Debuff))
				return false;

			target.RemoveBuff(BuffId.GraveChill_Debuff);
			return true;
		}

		private static void ApplyDeepResonance(ICombatEntity caster, ICombatEntity target, SkillModifier modifier)
		{
			if (!target.IsBuffActive(BuffId.Mourning_Debuff))
				return;

			if (caster.TryGetSkillLevel(SkillId.Kneller_DeeperResonance_Cleric, out var level))
				modifier.DamageMultiplier += DeepResonanceDamageBonusPerLevel * level;
		}

		public static void PlayGroundEffect(ICombatEntity caster, string effectName, Position position, float scale = 1f, float duration = 0f)
		{
			if (string.IsNullOrEmpty(effectName))
				return;

			caster.PlayEffectToGround(effectName, position, scale, duration);
		}

		public static void PlayProjectile(ICombatEntity caster, Position position, string projectileEffect, string impactEffect, float projectileScale = 1f, float impactScale = 1f, float range = 120f, int flightMs = 350)
			=> Send.ZC_NORMAL.SkillProjectile(caster, position, projectileEffect, projectileScale, impactEffect, impactScale, range, TimeSpan.FromMilliseconds(flightMs), TimeSpan.Zero, 0f, 1f);
	}
}
