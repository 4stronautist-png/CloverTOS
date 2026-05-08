using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Buffs.Handlers.Scouts.Desperado
{
	/// <summary>
	/// Last Man Standing PvP incoming damage reduction.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.Desperado_Reduce)]
	public class Desperado_ReduceOverride : BuffHandler
	{
		[CombatCalcModifier(CombatCalcPhase.BeforeCalc, BuffId.Desperado_Reduce)]
		public void OnBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!target.IsBuffActive(BuffId.Desperado_Reduce))
				return;

			modifier.DamageMultiplier *= 0.5f;
		}
	}
}
