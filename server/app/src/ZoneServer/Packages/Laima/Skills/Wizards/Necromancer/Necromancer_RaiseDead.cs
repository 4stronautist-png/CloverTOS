using System;
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
	/// Handler for the Necromancer skill Raise Dead.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Necromancer_RaiseDead)]
	public class Necromancer_RaiseDeadOverride : IGroundSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (caster is not Character character)
				return;

			var soldierCount = NecromancerSkillHelper.CountSkeletons(character, NecromancerSkeletonKind.Soldier)
				+ NecromancerSkillHelper.CountSkeletons(character, NecromancerSkeletonKind.EliteSoldier);
			if (soldierCount >= 6)
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

			var hasElite = NecromancerSkillHelper.GetSkeletons(character)
				.Exists(s => s.Id == NecromancerSkillHelper.EliteSkeletonSoldierId && !s.IsDead);
			var kind = character.IsAbilityActive(AbilityId.Necromancer35) && !hasElite
				? NecromancerSkeletonKind.EliteSoldier
				: NecromancerSkeletonKind.Soldier;

			skillHandle = ZoneServer.Instance.World.CreateSkillHandle();
			Send.ZC_SYNC_START(caster, skillHandle, 1);
			NecromancerSkillHelper.SpawnSkeleton(character, skill, kind, farPos);
			Send.ZC_SYNC_END(caster, skillHandle, 0);
			Send.ZC_SYNC_EXEC_BY_SKILL_TIME(caster, skillHandle, skill.Data.DefaultHitDelay);
		}
	}
}
