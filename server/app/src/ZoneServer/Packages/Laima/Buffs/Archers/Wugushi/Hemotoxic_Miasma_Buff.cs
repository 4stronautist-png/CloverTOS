using System;
using Melia.Shared.Packages;
using Melia.Shared.Game.Const;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Archers.Wugushi;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Buffs.Handlers.Archers.Wugushi
{
	/// <summary>
	/// Short Wide Miasma window that makes Wugushi attacks punish bleeding targets.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.Hemotoxic_Miasma_Buff)]
	public class Hemotoxic_Miasma_BuffOverride : BuffHandler
	{
		private static readonly TimeSpan HealingReductionDuration = TimeSpan.FromSeconds(8);
		private const float HealingReduction = WugushiSkillHelper.HemotoxicMiasmaHealingReductionPercent * 1000f;

		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			Send.ZC_NORMAL.PlayTextEffect(buff.Target, buff.Caster, "SHOW_BUFF_TEXT", (float)BuffId.Hemotoxic_Miasma_Buff, null);
			buff.NotifyUpdate();
		}

		[CombatCalcModifier(CombatCalcPhase.AfterCalc_Attack, BuffId.Hemotoxic_Miasma_Buff)]
		public void OnAttackAfterCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!attacker.IsBuffActive(BuffId.Hemotoxic_Miasma_Buff))
				return;

			if (!WugushiSkillHelper.IsWugushiSkill(skill))
				return;

			if (skillHitResult.Damage <= 0)
				return;

			if (!WugushiSkillHelper.IsBleedingEffectActive(target))
				return;

			target.StartBuff(BuffId.WideMiasma_Debuff, skill.Level, HealingReduction, HealingReductionDuration, attacker, skill.Id);
		}
	}
}
