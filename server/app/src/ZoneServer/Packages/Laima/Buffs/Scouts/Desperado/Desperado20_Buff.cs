using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Scouts.Desperado;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Buffs.Handlers.Scouts.Desperado
{
	/// <summary>
	/// Bad Guy: Death Approaching final damage bonus.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.Desperado20_Buff)]
	public class Desperado20_BuffOverride : BuffHandler
	{
		[CombatCalcModifier(CombatCalcPhase.BeforeBonuses, BuffId.Desperado20_Buff)]
		public void OnBeforeBonuses(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!DesperadoSkillHelper.IsDesperadoDamageSkill(skill.Id))
				return;

			if (!attacker.TryGetBuff(BuffId.Desperado20_Buff, out var buff))
				return;

			modifier.FinalDamageMultiplier *= 1f + 0.03f * buff.OverbuffCounter;
		}
	}
}
