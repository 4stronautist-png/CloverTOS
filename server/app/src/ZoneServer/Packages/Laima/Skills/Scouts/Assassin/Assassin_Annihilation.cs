using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Melia.Shared.Packages;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.CombatEntities.Components;
using Yggdrasil.Logging;
using Yggdrasil.Util;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Scouts.Assassin
	{
		/// <summary>
		/// Handler for the Assassin Skill Annihilation
		/// </summary>
	[Package("laima")]
		[SkillHandler(SkillId.Assassin_Annihilation)]
		public class Assassin_AnnihilationOverride : IGroundSkillHandler
		{
			private const int NormalHitCount = 20;
			private const int HighSpeedHitCount = 40;
			private const int HitsPerWave = 2;
			private const float PiercingHeartMarkDamageBonus = 0.20f;
			private const float HighSpeedDamageMultiplier = 0.5f;
			private static readonly TimeSpan ExitSceneDuration = TimeSpan.FromSeconds(5);
			private static readonly BuffId[] BleedingBuffs =
			{
				BuffId.Behead_Debuff,
				BuffId.HeavyBleeding,
				BuffId.BleedingPierce_Debuff,
				BuffId.Mythic_Bleeding_Debuff,
				BuffId.UC_bleed,
			};

		/// <summary>
		/// Handles skill, damaging targets.
		/// </summary>
		/// <param name="skill"></param>
		/// <param name="caster"></param>
		/// <param name="originPos"></param>
		/// <param name="farPos"></param>
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);

			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, length: 0, width: 100, angle: 0);
			var splashArea = skill.GetSplashArea(SplashType.Circle, splashParam);

			Send.ZC_SKILL_READY(caster, skill, originPos, farPos);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos);

			skill.Run(this.Attack(skill, caster, splashArea));
		}

		/// <summary>
		/// Executes the actual attack after a delay.
		/// </summary>
		/// <param name="skill"></param>
		/// <param name="caster"></param>
		/// <param name="splashArea"></param>
		private async Task Attack(Skill skill, ICombatEntity caster, ISplashArea splashArea)
		{
			var aniTime = TimeSpan.FromMilliseconds(50);
			var skillHitDelay = TimeSpan.Zero;

			var hits = new List<SkillHitInfo>();

			// Assassin23 changes Annihilation to a fast 40-hit version.
			var isFastVariant = caster.IsAbilityActive(AbilityId.Assassin23);
			var delayBetweenHits = TimeSpan.FromMilliseconds(isFastVariant ? 80 : 400);
			var totalHits = isFastVariant ? HighSpeedHitCount : NormalHitCount;
			var waveCount = totalHits / HitsPerWave;
			var consumedPiercingHeartMarks = new HashSet<ICombatEntity>();

			// Caster has invincibility during the normal version
			if (!isFastVariant)
				caster.StartBuff(BuffId.Skill_NoDamage_Buff, skill.Level, 0, TimeSpan.FromMilliseconds(3200), caster);

			for (var i = 0; i < waveCount; i++)
			{
				var targets = caster.Map.GetAttackableEnemiesIn(caster, splashArea);

				foreach (var target in targets.LimitBySDR(caster, skill))
				{
					var modifier = SkillModifier.MultiHit(HitsPerWave);

					if (isFastVariant)
						modifier.DamageMultiplier *= HighSpeedDamageMultiplier;

					var bleedDetonationDamage = 0f;

					// Assassin17 detonates the remaining bleeding damage on hit.
					if ((caster.IsAbilityActive(AbilityId.Assassin17) || caster.GetAbilityLevel(AbilityId.Assassin17) > 0) && target.IsBuffActiveByKeyword(BuffTag.Wound))
					{
						bleedDetonationDamage = this.ConsumeBleedingDamage(caster, target);
					}

					if (target.TryGetBuff(BuffId.Assassin_Request_Buff, out var piercingHeartMark) && piercingHeartMark.Caster == caster)
					{
						modifier.DamageMultiplier += PiercingHeartMarkDamageBonus;
						consumedPiercingHeartMarks.Add(target);
					}

					// Increase damage by 10% if target is under the effect of
					// Assassination Target from the caster
					if (target.TryGetBuff(BuffId.Assassin_Target_Debuff, out var assassinTargetDebuff))
					{
						if (assassinTargetDebuff.Caster == caster)
							modifier.DamageMultiplier += 0.10f;
					}

					var skillHitResult = SCR_SkillHit(caster, target, skill, modifier);
					skillHitResult.Damage += bleedDetonationDamage;
					target.TakeDamage(skillHitResult.Damage, caster);

					var skillHit = new SkillHitInfo(caster, target, skill, skillHitResult, aniTime, skillHitDelay);
					skillHit.HitEffect = HitEffect.Impact;
					hits.Add(skillHit);
				}

				Send.ZC_SKILL_HIT_INFO(caster, hits);

				hits.Clear();

				// we actually have to wait for the last hit here as well
				// due to the animation cancel and cloaking effect
				if (i < waveCount - 1)
					await skill.Wait(delayBetweenHits);
			}

			// Have to send this to make you reappear afterwards			
			Send.ZC_NORMAL.SkillCancelCancel(caster, skill.Id);
			Send.ZC_PLAY_ANI(caster, "idle1");
			caster.SetAttackState(false);

			foreach (var target in consumedPiercingHeartMarks)
				target.StopBuff(BuffId.Assassin_Request_Buff);

			// Assassin16 gives cloak after the slow version
			var exitSceneLevel = caster.GetAbilityLevel(AbilityId.Assassin16);
			if (caster.TryGetActiveAbilityLevel(AbilityId.Assassin16, out var activeExitSceneLevel))
				exitSceneLevel = Math.Max(exitSceneLevel, activeExitSceneLevel);

			if (!isFastVariant && exitSceneLevel > 0)
			{
				caster.StartBuff(BuffId.Cloaking_Buff, skill.Level, 5, ExitSceneDuration, caster, SkillId.Scout_Cloaking);
			}
		}

		private float ConsumeBleedingDamage(ICombatEntity caster, ICombatEntity target)
		{
			var totalDamage = 0f;

			foreach (var buffId in BleedingBuffs)
			{
				if (!target.TryGetBuff(buffId, out var buff))
					continue;

				if (buff.Caster != null && buff.Caster != caster)
					continue;

				var remainingTicks = MathF.Ceiling((float)buff.RemainingDuration.TotalSeconds);
				if (remainingTicks <= 0 || buff.NumArg2 <= 0)
					continue;

				totalDamage += buff.NumArg2 * remainingTicks;
				target.StopBuff(buffId);
			}

			return totalDamage;
		}
	}
}
