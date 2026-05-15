using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Monsters;

namespace Melia.Zone.Skills.Handlers.Archers.PiedPiper
{
	[Package("laima")]
	[SkillHandler(SkillId.PiedPiper_Wiegenlied)]
	public class PiedPiperWiegenlied : IGroundSkillHandler, IMeleeGroundSkillHandler
	{
		private const float Range = 160f;
		private const int MaxTargets = 14;
		private const int LullabyDuration = 10;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
			=> this.Execute(skill, caster, originPos, farPos, null);

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, IList<ICombatEntity> targets)
			=> this.Execute(skill, caster, originPos, farPos, targets);

		private void Execute(Skill skill, ICombatEntity caster, Position originPos, Position farPos, IList<ICombatEntity> targets)
		{
			if (!PiedPiperSkillHelper.TryStart(skill, caster, originPos, farPos))
				return;

			PiedPiperSkillHelper.SummonMouseFromSong(caster, skill);
			PiedPiperSkillHelper.SafePlaySound(caster, "skl_eff_piedpiper_wiegenlied_shot");
			PiedPiperSkillHelper.SafePlayEffect(caster, "E_Wiegenlied", 1f);
			PiedPiperSkillHelper.SafePlayEffectToGround(caster, "F_buff_basic052##3.2", caster.Position, 3.2f, 100);

			foreach (var enemy in PiedPiperSkillHelper.GetEnemiesFromHitListOrRange(caster, targets, Range).Where(CanAffect).Take(MaxTargets))
				PiedPiperSkillHelper.StartVisibleBuff(enemy, BuffId.Lullaby_Debuff, skill.Level, 0, TimeSpan.FromSeconds(LullabyDuration), caster, skill.Id);
		}

		private static bool CanAffect(ICombatEntity target)
			=> target is not Mob mob || mob.Rank != MonsterRank.Boss;
	}
}
