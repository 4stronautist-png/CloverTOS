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
	/// Handles Charge Hammer.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Sledger_ChargeHammer_Cleric)]
	public class Sledger_ChargeHammerOverride : IGroundSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			var impactPos = SledgerSkillHelper.GetImpactPosition(caster, farPos, 20);

			if (!SledgerSkillHelper.TryStart(skill, caster, originPos, farPos, allowMovement: true, visualPos: impactPos))
				return;
			
			var area = SledgerSkillHelper.CreateImpactSquare(skill, caster, impactPos, length: 32, width: 30);
			SledgerSkillHelper.RunCancellable(skill, () => SledgerSkillHelper.DelayedAttackArea(skill, caster, area, SledgerSkillHelper.GetSkillActionDuration(skill, 1150), appliesBigBangReduction: true, bigBangReductionSeconds: 5));
		}
	}
}
