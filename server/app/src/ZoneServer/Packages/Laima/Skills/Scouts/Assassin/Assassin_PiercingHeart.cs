using System;
using System.Collections.Generic;
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
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Scouts.Assassin
{
	/// <summary>
	/// Handler for the Assassin skill Piercing Heart
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Assassin_PiercingHeart)]
		public class Assassin_PiercingHeartOverride : IGroundSkillHandler
		{
			private const float BackAttackAngle = 90f;
			private const float ArmorPenetrationRate = 0.35f;
			private static readonly TimeSpan HealBlockDuration = TimeSpan.FromSeconds(5);
			private static readonly TimeSpan AnnihilationMarkDuration = TimeSpan.FromSeconds(15);

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

			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, length: 35, width: 20, angle: 0);
			var splashArea = skill.GetSplashArea(SplashType.Square, splashParam);

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
			var hitDelay = TimeSpan.FromMilliseconds(80);
			var aniTime = TimeSpan.FromMilliseconds(50);
			var skillHitDelay = TimeSpan.Zero;

			await skill.Wait(hitDelay);

			var targets = caster.Map.GetAttackableEnemiesIn(caster, splashArea);
			var hits = new List<SkillHitInfo>();

			foreach (var target in targets.LimitBySDR(caster, skill))
			{
				var modifier = SkillModifier.MultiHit(skill.Data.MultiHitCount);
				var isBackOrCloaked = this.IsBackOrCloaked(caster, target, modifier);

				if (target.ArmorMaterial == ArmorMaterialType.Cloth || target.ArmorMaterial == ArmorMaterialType.Leather)
					modifier.DefensePenetrationRate += ArmorPenetrationRate;

				// Increase damage by 10% if target is under the effect of
				// Assassination Target from the caster
				if (target.TryGetBuff(BuffId.Assassin_Target_Debuff, out var assassinTargetDebuff))
				{
					if (assassinTargetDebuff.Caster == caster)
						modifier.DamageMultiplier += 0.10f;
				}

				var skillHitResult = SCR_SkillHit(caster, target, skill, modifier);

				if (isBackOrCloaked)
					Send.ZC_NORMAL.PlayTextEffect(target, caster, "SHOW_CUSTOM_TEXT", 50, "Backstab!");

				target.TakeDamage(skillHitResult.Damage, caster);

				var skillHit = new SkillHitInfo(caster, target, skill, skillHitResult, aniTime, skillHitDelay);
				skillHit.HitEffect = HitEffect.Impact;

				hits.Add(skillHit);

				if (skillHitResult.Damage > 0)
				{
					target.StartBuff(BuffId.PiercingHeart_Debuff, skill.Level, 0, HealBlockDuration, caster);

					if (isBackOrCloaked)
						target.StartBuff(BuffId.Assassin_Request_Buff, skill.Level, 0, AnnihilationMarkDuration, caster, skill.Id);
				}
			}

			// Assassin14 adds a critical buff
			if (caster.IsAbilityActive(AbilityId.Assassin14))
			{
				caster.StartBuff(BuffId.PiercingHeart_Buff, skill.Level, 0, TimeSpan.FromSeconds(10), caster);
			}

			Send.ZC_SKILL_HIT_INFO(caster, hits);
		}

		private bool IsBackOrCloaked(ICombatEntity caster, ICombatEntity target, SkillModifier modifier)
			=> caster.IsBuffActive(BuffId.Cloaking_Buff) || caster.IsBehind(target, BackAttackAngle) || modifier.ForcedBackAttack;
	}
}
