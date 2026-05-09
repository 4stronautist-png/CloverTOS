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
	[SkillHandler(SkillId.Kneller_DeathKnell_Cleric)]
	public class Kneller_DeathKnell_ClericOverride : IGroundSkillHandler, IDynamicCasted
	{
		private const float Radius = 90f;
		private const int MaxTargets = 15;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!KnellerSkillHelper.TryStart(skill, caster, originPos, farPos))
				return;

			var area = new CircleF(caster.Position, Radius);
			KnellerSkillHelper.PlayGroundEffect(caster, "F_ground131_dark_red", caster.Position, 2.2f, 1300f);
			KnellerSkillHelper.RunAndReset(skill, caster, () => KnellerSkillHelper.AttackArea(skill, caster, area,
				configureHit: (hitTarget, modifier) => KnellerSkillHelper.ApplyDeathKnellBonus(hitTarget, modifier),
				afterHit: (hitTarget, result) =>
				{
					if (result.Damage > 0)
						KnellerSkillHelper.ConsumeMourningStacks(hitTarget);
				},
				delayMs: 900,
				hitCount: Math.Max(1, skill.Data.MultiHitCount),
				maxTargets: MaxTargets));
		}
	}
}
