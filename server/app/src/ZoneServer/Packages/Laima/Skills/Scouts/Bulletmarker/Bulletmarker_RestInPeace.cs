using System;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Scouts.Bulletmarker
{
	[Package("laima")]
	[SkillHandler(SkillId.Bulletmarker_RestInPeace)]
	public class Bulletmarker_RestInPeace : IGroundSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!BulletmarkerSkillHelper.TryStart(skill, caster, originPos, farPos))
				return;

			var modifier = BulletmarkerSkillHelper.CreateModifier(skill);
			if (BulletmarkerSkillHelper.TryConsumeOutrage(caster))
				modifier.FinalDamageMultiplier *= 1.55f;

			if (caster.IsAbilityActive(AbilityId.Bulletmarker13))
				modifier.HitCount += 1;

			skill.Run(BulletmarkerSkillHelper.AttackArea(skill, caster, originPos, farPos, modifier, length: 130, width: 80, angle: 35, splashType: SplashType.Square));
		}
	}
}
