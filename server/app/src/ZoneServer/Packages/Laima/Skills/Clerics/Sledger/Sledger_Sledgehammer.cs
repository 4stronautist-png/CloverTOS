using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Clerics.Sledger
{
	/// <summary>
	/// Passive marker for Sledgehammer. Its defense ignore is applied by
	/// SledgerSkillHelper when Sledger attacks hit.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Sledger_Sledgehammer_Cleric)]
	public class Sledger_SledgehammerOverride : ISelfSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			caster.StartBuff(BuffId.Sledgehammer_Buff, skill.Level, 0, System.TimeSpan.Zero, caster, skill.Id);
		}
	}
}
