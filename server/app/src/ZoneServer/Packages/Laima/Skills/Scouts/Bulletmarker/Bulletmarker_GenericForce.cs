using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Scouts.Bulletmarker
{
	[Package("laima")]
	[SkillHandler(SkillId.Bulletmarker_FullMetalJacket, SkillId.Bulletmarker_SmashBullet)]
	public class Bulletmarker_GenericForce : IForceSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!BulletmarkerSkillHelper.TryStart(skill, caster, originPos, farPos))
				return;

			var modifier = BulletmarkerSkillHelper.CreateModifier(skill);
			if (skill.Id == SkillId.Bulletmarker_SmashBullet && caster.IsAbilityActive(AbilityId.Bulletmarker15))
				modifier.Unblockable = true;

			skill.Run(BulletmarkerSkillHelper.AttackTarget(skill, caster, target, modifier));
		}
	}
}
