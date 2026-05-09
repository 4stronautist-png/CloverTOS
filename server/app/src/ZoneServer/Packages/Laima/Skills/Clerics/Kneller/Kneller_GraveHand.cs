using System;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Yggdrasil.Geometry.Shapes;

namespace Melia.Zone.Skills.Handlers.Clerics.Kneller
{
	[Package("laima")]
	[SkillHandler(SkillId.Kneller_GraveHand_Cleric)]
	public class Kneller_GraveHand_ClericOverride : ITargetGroundSkillHandler, ITargetSkillHandler, IDynamicCasted
	{
		private const float Radius = 35f;
		private const float FallbackRange = 80f;
		private const int MaxTargets = 5;

		public void Handle(Skill skill, ICombatEntity caster, ICombatEntity target)
		{
			var originPos = caster.Position;
			var farPos = target?.Position ?? caster.Position.GetRelative(caster.Direction, FallbackRange);
			this.Handle(skill, caster, originPos, farPos, target);
		}

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!KnellerSkillHelper.TryStart(skill, caster, originPos, farPos))
				return;

			var area = new CircleF(farPos, Radius);
			KnellerSkillHelper.PlayGroundEffect(caster, "I_wizard_DemonScratch_mesh2", farPos, 1.7f, Math.Max(1500f, (float)skill.Data.ShootTime.TotalMilliseconds));
			KnellerSkillHelper.RunAndReset(skill, caster, () => KnellerSkillHelper.AttackArea(skill, caster, area,
				afterHit: (hitTarget, result) =>
				{
					if (result.Damage <= 0)
						return;

					KnellerSkillHelper.ApplyMourning(caster, hitTarget, skill);

					if (caster.IsAbilityActive(AbilityId.Kneller7))
						KnellerSkillHelper.ApplyFrostGrave(caster, hitTarget);
				},
				delayMs: 300,
				hitCount: Math.Max(1, skill.Data.MultiHitCount),
				maxTargets: MaxTargets));
		}
	}
}
