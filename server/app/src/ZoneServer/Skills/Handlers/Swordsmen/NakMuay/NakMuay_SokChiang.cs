using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Monsters;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Swordsmen.NakMuay
{
	/// <summary>
	/// Handler for the NakMuay skill Sok Chiang.
	/// </summary>
	[SkillHandler(SkillId.NakMuay_SokChiang)]
	public class NakMuay_SokChiang : IGroundSkillHandler
	{
		/// <summary>
		/// Handles usage of the skill.
		/// </summary>
		/// <param name="skill"></param>
		/// <param name="caster"></param>
		/// <param name="originPos"></param>
		/// <param name="farPos"></param>
		/// <param name="target"></param>
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);
			
			if (caster.TryGetSkill(SkillId.NakMuay_MuayThai, out var skillMuayThai)) 
				skillMuayThai.ReduceCooldown(TimeSpan.FromSeconds(3));

			Send.ZC_SKILL_READY(caster, skill, originPos, farPos);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos, null);
			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, length: 45, width: 40, angle: 10f);
			var splashArea = skill.GetSplashArea(SplashType.Square, splashParam);
			skill.Run(this.Attack(skill, caster, splashArea));
		}

		private async Task Attack(Skill skill, ICombatEntity caster, ISplashArea splashArea)
		{
			var damageDelay = TimeSpan.FromMilliseconds(330);
			var skillHitDelay = skill.Properties.HitDelay;

			damageDelay /= skill.Properties.GetFloat(PropertyName.SklSpdRate);
			skillHitDelay /= skill.Properties.GetFloat(PropertyName.SklSpdRate);

			await skill.Wait(skillHitDelay);

			var hits = new List<SkillHitInfo>();
			var targets = caster.Map.GetAttackableEnemiesIn(caster, splashArea);

			foreach (var target in targets.LimitBySDR(caster, skill))
			{
				var skillHitResult = SCR_NakSkillHit(caster, target, skill, SkillModifier.Default);
				target.TakeDamage(skillHitResult.Damage, caster);

				// Apply Bleeding debuff
				var bleedingDuration = TimeSpan.FromSeconds(6 + skill.Level);
				if (target is Mob mob && mob.Data.Rank == MonsterRank.Boss)
					bleedingDuration = TimeSpan.FromSeconds(3);

				target.StartBuff(BuffId.HeavyBleeding, skill.Level, skillHitResult.Damage, bleedingDuration, caster);

				var skillHit = new SkillHitInfo(caster, target, skill, skillHitResult, damageDelay, TimeSpan.Zero)
				{
					HitInfo = { ResultType = skillHitResult.Result }
				};
				hits.Add(skillHit);
			}
			
			Send.ZC_SKILL_HIT_INFO(caster, hits);
		}
	}
}
