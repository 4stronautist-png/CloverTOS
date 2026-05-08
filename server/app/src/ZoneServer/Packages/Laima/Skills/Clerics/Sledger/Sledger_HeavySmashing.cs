using System;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Clerics.Sledger
{
	/// <summary>
	/// Handles Heavy Smashing.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Sledger_HeavySmashing_Cleric)]
	public class Sledger_HeavySmashingOverride : IGroundSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!SledgerSkillHelper.TryStart(skill, caster, originPos, farPos, visualPos: caster.Position))
				return;

			var duration = SledgerSkillHelper.GetSkillActionDuration(skill, 1500);
			SledgerSkillHelper.RunCancellable(skill, () => SledgerSkillHelper.AttackAreaOverTime(
				skill,
				caster,
				() => SledgerSkillHelper.CreateImpactSquare(skill, caster, SledgerSkillHelper.GetImpactPosition(caster, farPos, 12), length: 24, width: 24),
				skill.Data.MultiHitCount,
				duration,
				appliesBigBangReduction: true,
				bigBangReductionSeconds: 1
			));
		}
	}
}
