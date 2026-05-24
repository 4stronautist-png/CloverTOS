using System;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Skills.Handlers.Wizards.Necromancer
{
	[Package("laima")]
	[SkillHandler(SkillId.Common_ForcedAttack)]
	public class Necromancer_SummonForceAttackOverride : IForceSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity designatedTarget)
		{
			if (caster is not Character character)
				return;

			skill.IncreaseOverheat();
			Send.ZC_NORMAL.SkillTargetAnimation(caster, skill, caster.Direction, 1);
			Send.ZC_SKILL_FORCE_TARGET(caster, designatedTarget, skill);

			if (designatedTarget == null)
			{
				caster.ServerMessage(Localization.Get("No target specified."));
				return;
			}

			NecromancerSkillHelper.OrderAttack(skill, character, designatedTarget);
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Common_ForcedAttackCancel)]
	public class Necromancer_SummonCancelAttackOverride : ISelfSkillHandler, IGroundSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			this.Execute(skill, caster);
		}

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			this.Execute(skill, caster);
		}

		private void Execute(Skill skill, ICombatEntity caster)
		{
			if (caster is not Character character)
				return;

			skill.IncreaseOverheat();
			NecromancerSkillHelper.OrderCancelAttackAllSummons(character);
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Common_SummonRemove)]
	public class Necromancer_SummonReleaseOverride : ISelfSkillHandler, IGroundSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			this.Execute(skill, caster);
		}

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			this.Execute(skill, caster);
		}

		private void Execute(Skill skill, ICombatEntity caster)
		{
			if (caster is not Character character)
				return;

			skill.IncreaseOverheat();
			Send.ZC_NORMAL.SkillTargetAnimation(caster, skill, caster.Direction, 1);
			NecromancerSkillHelper.ReleaseNecromancerSummons(character);
		}
	}
}
