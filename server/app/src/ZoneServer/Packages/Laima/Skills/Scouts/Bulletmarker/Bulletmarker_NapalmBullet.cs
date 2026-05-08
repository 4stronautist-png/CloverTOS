using System;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Scouts.Bulletmarker
{
	[Package("laima")]
	[SkillHandler(SkillId.Bulletmarker_NapalmBullet)]
	public class Bulletmarker_NapalmBullet : IForceSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!BulletmarkerSkillHelper.TryStart(skill, caster, originPos, farPos))
				return;

			var modifier = BulletmarkerSkillHelper.CreateModifier(skill);
			var applyTase = BulletmarkerSkillHelper.TryConsumeOutrage(caster);

			skill.Run(BulletmarkerSkillHelper.AttackTarget(skill, caster, target, modifier, (hitTarget, _) =>
			{
				if (applyTase)
					hitTarget.StartBuff(BuffId.Tase_Debuff, skill.Level, skill.Properties.GetFloat(PropertyName.SkillFactor) * 0.055f, TimeSpan.FromSeconds(10), caster, skill.Id);
			}));
		}
	}
}
