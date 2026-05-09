using System;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Clerics.Kneller
{
	[Package("laima")]
	[SkillHandler(SkillId.Kneller_MourningChime_Cleric)]
	public class Kneller_MourningChime_ClericOverride : IGroundSkillHandler, IDynamicCasted
	{
		private const int MaxTargets = 5;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			originPos = caster.Position;
			farPos = caster.Position.GetRelative(caster.Direction, 120f);

			if (!KnellerSkillHelper.TryStart(skill, caster, originPos, farPos))
				return;

			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, length: 120, width: 18, angle: 0);
			var area = skill.GetSplashArea(SplashType.Square, splashParam);
			KnellerSkillHelper.PlayProjectile(caster, farPos, "F_smoke054", "None", 1.2f, 0f, 120f, 350);

			KnellerSkillHelper.RunAndReset(skill, caster, () => KnellerSkillHelper.AttackArea(skill, caster, area,
				configureHit: (hitTarget, modifier) =>
				{
					if (caster.IsAbilityActive(AbilityId.Kneller8) && KnellerSkillHelper.ConsumeFrostGrave(caster, hitTarget))
						modifier.DamageMultiplier += 0.30f;
				},
				afterHit: (hitTarget, result) =>
				{
					if (result.Damage > 0)
						KnellerSkillHelper.ApplyMourning(caster, hitTarget, skill);
				},
				delayMs: 300,
				hitCount: Math.Max(1, skill.Data.MultiHitCount),
				maxTargets: MaxTargets));
		}
	}
}
