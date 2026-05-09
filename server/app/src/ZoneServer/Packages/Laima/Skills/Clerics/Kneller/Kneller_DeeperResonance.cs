using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Clerics.Kneller
{
	[Package("laima")]
	[SkillHandler(SkillId.Kneller_DeeperResonance_Cleric)]
	public class Kneller_DeeperResonance_ClericOverride : IPassiveSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster)
		{
		}
	}
}
