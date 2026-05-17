using System;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Swordsmen.Eskrimer;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Buffs.Handlers.Swordsmen.Eskrimer
{
	[Package("laima")]
	[BuffHandler(BuffId.Touche_Buff)]
	public class Touche_BuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			var stacks = buff.OverbuffCounter;

			UpdatePropertyModifier(buff, buff.Target, PropertyName.HR_RATE_BM, EskrimerSkillHelper.ToucherAccuracyPerStack * stacks);
			Send.ZC_NORMAL.PlayTextEffect(buff.Target, buff.Caster, "SHOW_BUFF_TEXT", (float)BuffId.Touche_Buff, null);
			buff.NotifyUpdate();
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.HR_RATE_BM);
		}

		[CombatCalcModifier(CombatCalcPhase.BeforeCalc_Attack, BuffId.Touche_Buff)]
		public void OnAttackBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!attacker.TryGetBuff(BuffId.Touche_Buff, out var buff))
				return;

			var minCritChance = EskrimerSkillHelper.ToucherMinCritPerStack * buff.OverbuffCounter;
			modifier.MinCritChance = MathF.Max(modifier.MinCritChance, minCritChance);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Touche_max_Buff)]
	public class Touche_max_BuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			UpdatePropertyModifier(buff, buff.Target, PropertyName.HR_RATE_BM, EskrimerSkillHelper.ToucherAccuracyPerStack * EskrimerSkillHelper.ToucherMaxStacks);
			Send.ZC_NORMAL.PlayTextEffect(buff.Target, buff.Caster, "SHOW_BUFF_TEXT", (float)BuffId.Touche_max_Buff, null);
			buff.NotifyUpdate();
			EskrimerSkillHelper.EnablePasataSotoWindow(buff.Target, TimeSpan.FromSeconds(10));
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.HR_RATE_BM);
			EskrimerSkillHelper.SetPasataSotoAvailability(buff.Target, false);
		}

		[CombatCalcModifier(CombatCalcPhase.BeforeCalc_Attack, BuffId.Touche_max_Buff)]
		public void OnAttackBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!attacker.IsBuffActive(BuffId.Touche_max_Buff))
				return;

			var minCritChance = EskrimerSkillHelper.ToucherMinCritPerStack * EskrimerSkillHelper.ToucherMaxStacks;
			modifier.MinCritChance = MathF.Max(modifier.MinCritChance, minCritChance);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Ouvert_Buff)]
	public class Ouvert_BuffOverride : BuffHandler
	{
	}

	[Package("laima")]
	[BuffHandler(BuffId.Pret_Buff)]
	public class Pret_BuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			Send.ZC_NORMAL.PlayTextEffect(buff.Target, buff.Caster, "SHOW_BUFF_TEXT", (float)BuffId.Pret_Buff, null);
			buff.NotifyUpdate();
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Invitation_Buff)]
	public class Invitation_BuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			Send.ZC_NORMAL.PlayTextEffect(buff.Target, buff.Caster, "SHOW_BUFF_TEXT", (float)BuffId.Invitation_Buff, null);
			buff.NotifyUpdate();
		}

		[CombatCalcModifier(CombatCalcPhase.AfterCalc_Defense, BuffId.Invitation_Buff)]
		public void OnDefenseAfterCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!target.IsBuffActive(BuffId.Invitation_Buff))
				return;

			skillHitResult.Damage = 0;
			skillHitResult.Result = HitResultType.Block;
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.AdvantGarde_Buff)]
	public class AdvantGarde_BuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			Send.ZC_NORMAL.PlayTextEffect(buff.Target, buff.Caster, "SHOW_BUFF_TEXT", (float)BuffId.AdvantGarde_Buff, null);
			buff.NotifyUpdate();

			if (buff.Target is Character character)
			{
				Send.ZC_NORMAL.SkillChangeAnimation(character, SkillId.Normal_Attack, "SKL_EPEEGARDE_ATK");
				Send.ZC_NORMAL.SkillChangeAnimation(character, SkillId.Normal_Attack_TH, "SKL_EPEEGARDE_ATK");
			}
		}

		public override void OnEnd(Buff buff)
		{
			if (buff.Target is Character character)
			{
				Send.ZC_NORMAL.SkillChangeAnimation(character, SkillId.Normal_Attack);
				Send.ZC_NORMAL.SkillChangeAnimation(character, SkillId.Normal_Attack_TH);
			}
		}

		[CombatCalcModifier(CombatCalcPhase.AfterBonuses, BuffId.AdvantGarde_Buff)]
		public void OnAfterBonuses(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!EskrimerSkillHelper.IsEskrimerSkill(skill))
				return;

			if (!attacker.TryGetBuff(BuffId.AdvantGarde_Buff, out var buff))
				return;

			if (skillHitResult.Result != HitResultType.Crit)
				return;

			modifier.FinalDamageMultiplier += buff.NumArg2;
		}
	}
}
