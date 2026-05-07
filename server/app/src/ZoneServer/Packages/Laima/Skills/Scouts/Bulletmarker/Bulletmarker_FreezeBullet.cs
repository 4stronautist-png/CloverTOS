using System;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Buffs;
using Melia.Zone.Network;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Skills.Handlers.Scouts.Bulletmarker
{
	[Package("laima")]
	[SkillHandler(SkillId.Bulletmarker_FreezeBullet)]
	public class Bulletmarker_FreezeBullet : ISelfSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (caster.IsBuffActive(BuffId.Outrage_Buff))
			{
				Send.ZC_SKILL_DISABLE(caster);
				return;
			}

			if (!BulletmarkerSkillHelper.TryStartSelf(skill, caster, originPos, dir))
				return;

			BulletmarkerSkillHelper.PlaySelfSkill(caster, skill);

			var duration = TimeSpan.FromSeconds(8);
			caster.StartBuff(BuffId.FreezeBullet_Buff, skill.Level, 0, duration, caster, skill.Id);

			if (caster.IsAbilityActive(AbilityId.Bulletmarker24))
				caster.StartBuff(BuffId.SilverBullet_Buff, skill.Level, 0, duration, caster, skill.Id);

			if (caster is Character character)
			{
				Send.ZC_OBJECT_PROPERTY(character);
				Send.ZC_MOVE_SPEED(character);
			}
		}
	}
}
