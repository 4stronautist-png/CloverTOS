using System;
using System.Collections.Generic;
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
using Melia.Zone.World.Actors.Characters;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Scouts.Assassin
{
	[Package("laima")]
	[SkillHandler(SkillId.Assassin_Behead)]
	public class Assassin_BeheadOverride : IGroundSkillHandler
	{
		private const float BackAttackAngle = 90f;
		private const float InstantAccelerationStunDamageBonus = 0.45f;
		private const float BleedingDamageRate = 0.125f;
		private static readonly TimeSpan BleedingDuration = TimeSpan.FromSeconds(15);
		private static readonly TimeSpan DeepWoundBleedingDuration = TimeSpan.FromSeconds(7.5);
		private static readonly TimeSpan SilenceDuration = TimeSpan.FromSeconds(2);

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);

			var attackTarget = this.GetAssassinationTarget(caster) ?? target;
			if (attackTarget != null)
			{
				this.TryTeleportBehindAssassinationTarget(caster, attackTarget);
				farPos = attackTarget.Position;
			}

			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, length: 40, width: 20, angle: 0);
			var splashArea = skill.GetSplashArea(SplashType.Square, splashParam);

			Send.ZC_SKILL_READY(caster, skill, originPos, farPos);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos, null);
			caster.SetAttackState(false);

			skill.Run(this.Attack(skill, caster, splashArea));
		}

		private async Task Attack(Skill skill, ICombatEntity caster, ISplashArea splashArea)
		{
			await skill.Wait(TimeSpan.FromMilliseconds(30));

			var hits = new List<SkillHitInfo>();
			var targets = caster.Map.GetAttackableEnemiesIn(caster, splashArea);

				foreach (var target in targets.LimitBySDR(caster, skill))
				{
					var modifier = SkillModifier.MultiHit(skill.Data.MultiHitCount);
					this.ApplyAssassinationTargetBonus(caster, target, modifier);
					var isBackOrCloaked = this.IsBackOrCloaked(caster, target, modifier);

					if (isBackOrCloaked)
						modifier.MinCritChance = 100;

					if (target.IsBuffActive(BuffId.Stun))
						modifier.DamageMultiplier += InstantAccelerationStunDamageBonus;

					var skillHitResult = SCR_SkillHit(caster, target, skill, modifier);
					if (isBackOrCloaked)
						Send.ZC_NORMAL.PlayTextEffect(target, caster, "SHOW_CUSTOM_TEXT", 50, "Backstab!");

					target.TakeDamage(skillHitResult.Damage, caster);
					hits.Add(new SkillHitInfo(caster, target, skill, skillHitResult, TimeSpan.FromMilliseconds(120), TimeSpan.Zero) { HitEffect = HitEffect.Impact });

					if (skillHitResult.Damage > 0 && this.HasAbility(caster, AbilityId.Assassin5))
						target.StartBuff(BuffId.Common_Silence, skill.Level, 0, SilenceDuration, caster, skill.Id);

					if (skillHitResult.Damage <= 0 || !isBackOrCloaked)
						continue;

					var bleedingDamage = skillHitResult.Damage * BleedingDamageRate;
					var bleedingDuration = BleedingDuration;

					if (target.Rank != MonsterRank.Boss && this.HasAbility(caster, AbilityId.Assassin6))
					{
						bleedingDamage = MathF.Min(caster.Properties.GetFloat(PropertyName.MHP) * 0.05f, target.Properties.GetFloat(PropertyName.MHP) * 0.05f);
						bleedingDuration = DeepWoundBleedingDuration;
					}

					target.StartBuff(BuffId.Behead_Debuff, skill.Level, bleedingDamage, bleedingDuration, caster, skill.Id);

				}

			Send.ZC_SKILL_HIT_INFO(caster, hits);
			caster.SetAttackState(false);
		}

		private ICombatEntity GetAssassinationTarget(ICombatEntity caster)
		{
			if (caster is not Character character || !character.Variables.Temp.TryGetInt("Melia.AssassinationTarget", out var targetHandle))
				return null;

			if (!caster.Map.TryGetCombatEntity(targetHandle, out var target))
			{
				character.Variables.Temp.Remove("Melia.AssassinationTarget");
				return null;
			}

			if (!target.TryGetBuff(BuffId.Assassin_Target_Debuff, out var debuff) || debuff.Caster != caster)
			{
				character.Variables.Temp.Remove("Melia.AssassinationTarget");
				return null;
			}

			return target;
		}

		private void TryTeleportBehindAssassinationTarget(ICombatEntity caster, ICombatEntity target)
		{
			if (!target.TryGetBuff(BuffId.Assassin_Target_Debuff, out var debuff) || debuff.Caster != caster)
				return;

			var desiredPos = target.Position.GetRelative(target.Direction.Backwards, 25f);
			if (caster.Map.Ground.TryGetNearestValidPosition(desiredPos, out var validPos, maxDistance: 40f))
				desiredPos = validPos;
			else if (caster.Map.Ground.TryGetHeightAt(desiredPos, out var height))
				desiredPos.Y = height;
			else
				return;

			caster.Position = desiredPos;
			caster.Direction = caster.Position.GetDirection(target.Position);
			Send.ZC_SET_POS(caster, desiredPos);
		}

		private void ApplyAssassinationTargetBonus(ICombatEntity caster, ICombatEntity target, SkillModifier modifier)
		{
			if (target.TryGetBuff(BuffId.Assassin_Target_Debuff, out var assassinTargetDebuff) && assassinTargetDebuff.Caster == caster)
				modifier.DamageMultiplier += 0.10f;
		}

			private bool HasAbility(ICombatEntity caster, AbilityId abilityId)
				=> caster.IsAbilityActive(abilityId) || caster.GetAbilityLevel(abilityId) > 0;

			private bool IsBackOrCloaked(ICombatEntity caster, ICombatEntity target, SkillModifier modifier)
				=> caster.IsBuffActive(BuffId.Cloaking_Buff) || caster.IsBehind(target, BackAttackAngle) || modifier.ForcedBackAttack;
		}
	}
