using System;
using System.Linq;
using Melia.Shared.Packages;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Skills.Handlers.Wizards.Necromancer
{
	/// <summary>
	/// Handler for the Necromancer skill Raise Skullwizard.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Necromancer_RaiseSkullwizard)]
	public class Necromancer_RaiseSkullwizardOverride : IGroundSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (caster is not Character character)
				return;

			var mageLimit = character.IsAbilityActive(AbilityId.Necromancer26) ? 1 : 2;
			if (NecromancerSkillHelper.CountSkeletons(character, NecromancerSkeletonKind.Mage) >= mageLimit)
				return;

			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);

			var skillHandle = ZoneServer.Instance.World.CreateSkillHandle();

			Send.ZC_SKILL_READY(caster, skill, skillHandle, caster.Position, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, caster.Position, caster.Direction, Position.Zero);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos);

			if (character.IsAbilityActive(AbilityId.Necromancer26))
			{
				foreach (var mage in NecromancerSkillHelper.GetSkeletons(character).Where(s => s.Id == MonsterId.SkeletonMage && !s.IsDead))
				{
					NecromancerSkillHelper.RestoreSummonResource(character, mage);
					mage.TakeDamage(Math.Max(1, mage.Hp), character);
				}
			}

			skillHandle = ZoneServer.Instance.World.CreateSkillHandle();
			Send.ZC_SYNC_START(caster, skillHandle, 1);
			NecromancerSkillHelper.SpawnSkeleton(character, skill, NecromancerSkeletonKind.Mage, farPos);

			if (character.IsAbilityActive(AbilityId.Necromancer24))
			{
				foreach (var skeleton in NecromancerSkillHelper.GetSkeletons(character))
					skeleton.StartBuff(BuffId.SkullFollowPainBarrier_Buff, 1, 0, TimeSpan.FromSeconds(45), caster, skill.Id);
			}
			Send.ZC_SYNC_END(caster, skillHandle, 0);
			Send.ZC_SYNC_EXEC_BY_SKILL_TIME(caster, skillHandle, skill.Data.DefaultHitDelay);
		}
	}
}
