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
using Yggdrasil.Util;
using static Melia.Shared.Util.TaskHelper;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Swordsmen.NakMuay
{
	/// <summary>
	/// Handler for the NakMuay skill Te Kha.
	/// </summary>
	[SkillHandler(SkillId.NakMuay_TeKha)]
	public class NakMuay_TeKha : IGroundSkillHandler
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
			caster.TurnTowards(farPos);
			caster.SetAttackState(true);
			
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
				var modifier = SkillModifier.Default;

				// 50% increased damage for small or boss monsters
				if (target is Mob mob)
				{
					if (mob.Data.Size == SizeType.S || mob.Data.Rank == MonsterRank.Boss)
						modifier.DamageMultiplier = 1.5f;
				}

				var skillHitResult = SCR_NakSkillHit(caster, target, skill, modifier);
				target.TakeDamage(skillHitResult.Damage, caster);

				// Apply Ram Muay: Shock debuff (immobilize)
				target.StartBuff(BuffId.RamMuay_Debuff, 0, 0, TimeSpan.FromMilliseconds(1500), caster);

				var skillHit = new SkillHitInfo(caster, target, skill, skillHitResult, damageDelay, TimeSpan.Zero);
				hits.Add(skillHit);
			}
			Send.ZC_SKILL_HIT_INFO(caster, hits);
		}
	}
}
