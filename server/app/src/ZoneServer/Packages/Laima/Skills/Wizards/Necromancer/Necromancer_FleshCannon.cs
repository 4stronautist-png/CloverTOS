using Melia.Shared.Packages;
using Melia.Shared.Game.Const;
using Melia.Shared.World;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Wizards.Necromancer
{
	/// <summary>
	/// Handler for the Necromancer skill Flesh Cannon.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Necromancer_FleshCannon)]
	public class Necromancer_FleshCannonOverride : IPassiveSkillHandler, ISelfSkillHandler, IGroundSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster)
		{
		}

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
		}

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
		}
	}
}
