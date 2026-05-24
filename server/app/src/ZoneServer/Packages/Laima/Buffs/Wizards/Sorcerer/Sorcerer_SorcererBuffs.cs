using System;
using Melia.Shared.Packages;
using Melia.Shared.Game.Const;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;
using Melia.Zone.Scripting;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Wizards.Necromancer;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.CombatEntities.Components;
using Melia.Zone.World.Actors.Monsters;
using Yggdrasil.Util;

namespace Melia.Zone.Buffs.Handlers.Wizards.Sorcerer
{
	/// <summary>
	/// Handler for the Sorcerer_Obey_Status_Buff applied to controlled summons.
	/// </summary>
	/// <remarks>
	/// Applied when the player takes direct control of their summon.
	/// </remarks>
	[Package("laima")]
	[BuffHandler(BuffId.Sorcerer_Obey_Status_Buff)]
	public class Sorcerer_Obey_Status_BuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			// The summon is now under direct player control
			// Disable AI and allow player input
			if (buff.Target is Summon summon)
			{
				summon.Vars.SetBool("UnderPlayerControl", true);

				// Remove AI component while under control
				var aiComponent = summon.Components.Get<AiComponent>();
				if (aiComponent != null)
				{
					summon.Vars.Set("StoredAiComponent", aiComponent);
					summon.Components.Remove<AiComponent>();
				}
			}
		}

		public override void OnEnd(Buff buff)
		{
			if (buff.Target is Summon summon)
			{
				summon.Vars.SetBool("UnderPlayerControl", false);

				// Restore AI component
				if (summon.Vars.TryGet<AiComponent>("StoredAiComponent", out var aiComponent))
				{
					summon.Components.Add(aiComponent);
					summon.Vars.Remove("StoredAiComponent");
				}

				// Also stop control on the owner side
				if (summon.Owner is Character owner)
				{
					owner.StopBuff(BuffId.Sorcerer_Obey_PC_DEF_Buff);
					//Send.ZC_CONTROL_OBJECT(owner, summon, false, "None");
				}
			}
		}
	}

	/// <summary>
	/// Handler for the Sorcerer_Obey_PC_DEF_Buff applied to the sorcerer during Obey.
	/// </summary>
	/// <remarks>
	/// Provides defensive bonuses while controlling a summon.
	/// </remarks>
	[Package("laima")]
	[BuffHandler(BuffId.Sorcerer_Obey_PC_DEF_Buff)]
	public class Sorcerer_Obey_PC_DEF_BuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			// Apply defensive bonuses while controlling summon
			var defBonus = 50f * buff.NumArg1;
			var mdefBonus = 50f * buff.NumArg1;

			AddPropertyModifier(buff, buff.Target, PropertyName.DEF_BM, defBonus);
			AddPropertyModifier(buff, buff.Target, PropertyName.MDEF_BM, mdefBonus);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.DEF_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.MDEF_BM);
		}
	}

	/// <summary>
	/// Handler for the Summoning_Overwork_Buff applied to summons.
	/// </summary>
	/// <remarks>
	/// Provides bonuses but may have drawbacks.
	/// </remarks>
	[Package("laima")]
	[BuffHandler(BuffId.Summoning_Overwork_Buff)]
	public class Summoning_Overwork_BuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			// Overwork provides attack bonuses based on ability level
			var abilityLevel = buff.NumArg1;
			var atkBonus = 100f * abilityLevel;

			AddPropertyModifier(buff, buff.Target, PropertyName.PATK_BM, atkBonus);
			AddPropertyModifier(buff, buff.Target, PropertyName.MATK_BM, atkBonus);
		}

		public override void WhileActive(Buff buff)
		{
			// Overwork may drain HP over time as a drawback
			if (buff.Target is Summon summon)
			{
				var hpDrain = summon.Properties.GetFloat(PropertyName.MHP) * 0.01f;
				summon.TakeDamage(hpDrain, summon);
			}
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.PATK_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.MATK_BM);
		}
	}

	/// <summary>
	/// Handler for the Ability_buff_PC_Summon applied to all player summons.
	/// </summary>
	/// <remarks>
	/// This is a baseline buff for all PC summons.
	/// </remarks>
	[Package("laima")]
	[BuffHandler(BuffId.Ability_buff_PC_Summon)]
	public class Ability_buff_PC_SummonOverride : BuffHandler
	{
		private static readonly TimeSpan RustyBladePoisonDuration = TimeSpan.FromSeconds(4);
		private static readonly TimeSpan FleshAmalgamDebuffDuration = TimeSpan.FromSeconds(5);

		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			// Mark entity as a PC summon
			if (buff.Target is Summon summon)
			{
				summon.Vars.SetBool("IsPCSummon", true);
			}
		}

		public override void OnEnd(Buff buff)
		{
			if (buff.Target is Summon summon)
			{
				summon.Vars.SetBool("IsPCSummon", false);
			}
		}

		[CombatCalcModifier(CombatCalcPhase.BeforeCalc, BuffId.Ability_buff_PC_Summon)]
		public void OnBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult result)
		{
			if (attacker is not Summon summon || summon.Owner is not Character owner)
				return;

			if (NecromancerSkillHelper.HasSummonControlJob(owner))
				modifier.FinalDamageMultiplier *= NecromancerSkillHelper.GetSummonControlMultiplier(owner);

			var isNecromancerSummon = NecromancerSkillHelper.IsNecromancerSummon(summon);
			if (!isNecromancerSummon)
				return;

			var untilDeathBonus = NecromancerSkillHelper.GetUntilDeathBonus(summon);
			if (untilDeathBonus > 0)
				modifier.FinalDamageMultiplier *= 1f + untilDeathBonus;

			var supportDamageBonus = NecromancerSkillHelper.GetSupportDamageBonus(summon);
			if (supportDamageBonus > 0)
				modifier.FinalDamageMultiplier *= 1f + supportDamageBonus;

			if (owner.TryGetSkill(SkillId.Necromancer_FleshCannon, out var martyrSkill) && owner.Hp <= owner.MaxHp * 0.5f)
			{
				var martyrBonus = Math.Max(1, martyrSkill.Level) * 0.01f;
				var reinforceRate = ScriptableFunctions.Skill.Get("SCR_Get_AbilityReinforceRate");
				martyrBonus *= 1f + reinforceRate(martyrSkill);
				modifier.FinalDamageMultiplier *= 1f + martyrBonus;
			}
		}

		[CombatCalcModifier(CombatCalcPhase.AfterCalc, BuffId.Ability_buff_PC_Summon)]
		public void OnAfterCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult result)
		{
			if (result.Damage <= 0 || attacker is not Summon summon || summon.Owner is not Character owner)
				return;

			if (!NecromancerSkillHelper.IsNecromancerSummon(summon))
				return;

			if ((summon.Id == MonsterId.SkeletonSoldier || summon.Id == NecromancerSkillHelper.EliteSkeletonSoldierId)
				&& owner.IsAbilityActive(AbilityId.Necromancer22)
				&& RandomProvider.Get().Next(100) < 20)
			{
				target.StartBuff(BuffId.Poison, 1, Math.Max(1, result.Damage * 0.25f), RustyBladePoisonDuration, owner, SkillId.Necromancer_RaiseDead);
			}

			if (summon.Id == NecromancerSkillHelper.FleshAmalgamId)
			{
				var rnd = RandomProvider.Get().Next(100);
				if (rnd < 30)
					target.StartBuff(BuffId.Blind, 1, 0, FleshAmalgamDebuffDuration, owner, SkillId.Necromancer_CorpseTower);
				if (rnd < 5)
					target.StartBuff(BuffId.Confuse, 1, 0, FleshAmalgamDebuffDuration, owner, SkillId.Necromancer_CorpseTower);
			}

			if (summon.Id == NecromancerSkillHelper.ShoggothId && target is Mob mob && !target.IsDead)
				TryDevourShoggothTarget(summon, owner, mob);
		}

		private static void TryDevourShoggothTarget(Summon summon, Character owner, Mob target)
		{
			if (target.Rank == MonsterRank.Boss)
				return;

			var enlarged = summon.Vars.GetBool("Melia.Necromancer.Shoggoth.Enlarged", false);
			var canDevourBySize = target.EffectiveSize == SizeType.S || (enlarged && target.EffectiveSize == SizeType.M);
			if (!canDevourBySize)
				return;

			if (Math.Abs(target.Level - owner.Level) > 30)
				return;

			var chance = summon.Vars.GetFloat("Melia.Necromancer.DevourChance", 0.02f);
			if (RandomProvider.Get().NextDouble() >= chance)
				return;

			target.TakeDamage(Math.Max(1, target.Hp), summon);
		}
	}
}
