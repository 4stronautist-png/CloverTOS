using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Skills.Handlers.Scouts.Bulletmarker
{
	[Package("laima")]
	[SkillHandler(SkillId.Bulletmarker_TracerBullet)]
	public class Bulletmarker_TracerBullet : ISelfSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (!BulletmarkerSkillHelper.TryStartSelf(skill, caster, originPos, dir))
				return;

			BulletmarkerSkillHelper.PlaySelfSkill(caster, skill);
			caster.StartBuff(BuffId.TracerBullet_Buff, skill.Level, 0, BulletmarkerSkillHelper.GetDefaultBuffDuration(), caster, skill.Id);

			if (caster is Character character)
				Send.ZC_OBJECT_PROPERTY(character);
		}
	}
}
