using Melia.Shared.Packages;
using Melia.Shared.Game.Const;
using Melia.Zone.Buffs;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Archers.Wugushi;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Buffs.Handlers.Archers.Wugushi
{
	/// <summary>
	/// Handler for the Wide Miasma stealth buff.
	/// Hides caster from monsters. Breaks when dealing damage
	/// (handled in calc_combat.cs via Cloaking tag).
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.WideMiasma_Buff)]
	public class WideMiasma_BuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			AddPropertyModifier(buff, buff.Target, PropertyName.MSPD_BM, WugushiSkillHelper.GetWideMiasmaMoveSpeedBonus(buff.Target));
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.MSPD_BM);
		}

		[CombatCalcModifier(CombatCalcPhase.AfterCalc_Attack, BuffId.WideMiasma_Buff)]
		public void OnAttackAfterCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (skillHitResult.Damage <= 0)
				return;

			attacker.StopBuff(BuffId.WideMiasma_Buff);
		}
	}
}
