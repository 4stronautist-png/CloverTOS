using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Monsters;

namespace Melia.Zone.Skills.Handlers.Archers.PiedPiper
{
	[Package("laima")]
	[SkillHandler(SkillId.PiedPiper_Dissonanz)]
	public class PiedPiperDissonanz : IGroundSkillHandler, IMeleeGroundSkillHandler
	{
		private const float Range = 160f;
		private const int DurationSeconds = 5;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
			=> this.Execute(skill, caster, originPos, farPos, null);

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, IList<ICombatEntity> targets)
			=> this.Execute(skill, caster, originPos, farPos, targets);

		private void Execute(Skill skill, ICombatEntity caster, Position originPos, Position farPos, IList<ICombatEntity> targets)
		{
			if (!PiedPiperSkillHelper.TryStart(skill, caster, originPos, farPos))
				return;

			PiedPiperSkillHelper.SummonMouseFromSong(caster, skill);
			PiedPiperSkillHelper.SafePlaySound(caster, "skl_eff_piedpiper_dissonanz_shot");
			PiedPiperSkillHelper.SafePlayEffectToGround(caster, "F_archer_Dissonanz_ground##3.2", caster.Position, 3.2f, 400);

			skill.Run(skill.Wait(TimeSpan.FromMilliseconds(250)).ContinueWith(_ => this.Apply(skill, caster, targets)));
			skill.Run(this.TickDamage(skill, caster, targets));
		}

		private void Apply(Skill skill, ICombatEntity caster, IList<ICombatEntity> targets)
		{
			foreach (var enemy in PiedPiperSkillHelper.GetEnemiesFromHitListOrRange(caster, targets, Range).Where(CanAffect).Take(10))
			{
				var hidden = enemy.IsBuffActive(BuffId.Cloaking_Buff) || enemy.IsBuffActive(BuffId.Burrow_Rogue);
				enemy.RemoveBuff(BuffId.Cloaking_Buff);
				enemy.RemoveBuff(BuffId.Burrow_Rogue);
				PiedPiperSkillHelper.StartVisibleBuff(enemy, BuffId.Dissonanz_Stun_Debuff, skill.Level, 0, TimeSpan.FromSeconds(DurationSeconds), caster, skill.Id);
				PiedPiperSkillHelper.StartVisibleBuff(enemy, BuffId.Stun, skill.Level, 0, TimeSpan.FromSeconds(DurationSeconds), caster, skill.Id);

				if (caster.TryGetActiveAbilityLevel(AbilityId.PiedPiper2, out var soundWaveLevel))
					PiedPiperSkillHelper.StartVisibleBuff(enemy, BuffId.Dissonanz_Debuff, 1, 0, TimeSpan.FromSeconds(soundWaveLevel * 2), caster, skill.Id);
			}
		}

		private async System.Threading.Tasks.Task TickDamage(Skill skill, ICombatEntity caster, IList<ICombatEntity> targets)
		{
			for (var i = 0; i < DurationSeconds; ++i)
			{
				await skill.Wait(TimeSpan.FromSeconds(1));

				if (caster.IsDead)
					break;

				var hits = new List<SkillHitInfo>();
				foreach (var enemy in PiedPiperSkillHelper.GetEnemiesFromHitListOrRange(caster, targets, Range).Where(CanAffect).Take(10))
				{
					if (enemy == null || enemy.IsDead || !caster.CanDamage(enemy))
						continue;

					var hitResult = SkillUseFunctions.SCR_SkillHit(caster, enemy, skill);
					if (hitResult.Damage <= 0)
						hitResult.Damage = MathF.Max(1, caster.Properties.GetFloat(PropertyName.PATK) * skill.SkillFactor / 100f);

					enemy.TakeDamage(hitResult.Damage, caster);

					var hit = new SkillHitInfo(caster, enemy, skill, hitResult, TimeSpan.Zero, skill.Properties.HitDelay)
					{
						HitEffect = HitEffect.Impact,
					};
					hits.Add(hit);
				}

				if (hits.Count != 0)
					Send.ZC_SKILL_HIT_INFO(caster, hits);
			}
		}

		private static bool CanAffect(ICombatEntity target)
			=> target is not Mob mob || mob.Rank != MonsterRank.Boss;
	}
}
