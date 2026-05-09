using System;
using Melia.Shared.Game.Const;
using Melia.Shared.Game.Properties;
using Melia.Shared.Packages;
using Melia.Zone.Buffs;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Effects;

namespace Melia.Zone.Buffs.Handlers.Clerics.Kneller
{
	[Package("laima")]
	[BuffHandler(BuffId.PassingBell_Buff)]
	public class PassingBell_BuffOverride : BuffHandler
	{
		private const float FixedMovementSpeed = 18f;

		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			AddPropertyModifier(buff, buff.Target, PropertyName.FIXMSPD_BM, FixedMovementSpeed);
			Send.ZC_MOVE_SPEED(buff.Target);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.FIXMSPD_BM);
			Send.ZC_MOVE_SPEED(buff.Target);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Mourning_Dummy_Debuff)]
	public class Shame_DebuffOverride : BuffHandler
	{
		private const float RateReductionPerSkillLevelPerStack = 0.01f;
		private const float MoveSpeedReductionPerStack = 3f;
		private const string ShameTextEffect = "SHOW_BUFF_TEXT";
		private const string ShameVisualEffectKey = "Melia.Kneller.ShameBell.Visual";
		private const string ShameVisualEffect = "GroundAura_DarkSoul_Red_01";

		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			this.UpdateModifiers(buff);
			this.RefreshVisualEffect(buff);
			this.PlayVisibleDebuff(buff);
		}

		public override void OnExtend(Buff buff)
		{
			this.UpdateModifiers(buff);
			this.RefreshVisualEffect(buff);
			this.PlayVisibleDebuff(buff);
		}

		public override void WhileActive(Buff buff)
		{
			this.PlayVisibleDebuff(buff);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.DR_RATE_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.CRTDR_RATE_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.HR_RATE_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.DEF_RATE_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.MDEF_RATE_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.DR_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.CRTDR_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.HR_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.DEF_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.MDEF_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.MSPD_BM);
			buff.Target.RemoveEffect(ShameVisualEffectKey);
			Send.ZC_MOVE_SPEED(buff.Target);
		}

		private void UpdateModifiers(Buff buff)
		{
			var stacks = Math.Max(1, buff.OverbuffCounter);
			var skillLevel = Math.Min(5, Math.Max(1, (int)buff.NumArg1));
			var rateReduction = -RateReductionPerSkillLevelPerStack * skillLevel * stacks;
			var moveReduction = -MoveSpeedReductionPerStack * stacks;

			this.SetRateOrFlatModifier(buff, PropertyName.DR_RATE_BM, PropertyName.DR_BM, PropertyName.DR, rateReduction);
			this.SetRateOrFlatModifier(buff, PropertyName.CRTDR_RATE_BM, PropertyName.CRTDR_BM, PropertyName.CRTDR, rateReduction);
			this.SetRateOrFlatModifier(buff, PropertyName.HR_RATE_BM, PropertyName.HR_BM, PropertyName.HR, rateReduction);
			this.SetRateOrFlatModifier(buff, PropertyName.DEF_RATE_BM, PropertyName.DEF_BM, PropertyName.DEF, rateReduction);
			this.SetRateOrFlatModifier(buff, PropertyName.MDEF_RATE_BM, PropertyName.MDEF_BM, PropertyName.MDEF, rateReduction);
			SetPropertyModifier(buff, buff.Target, PropertyName.MSPD_BM, moveReduction);
			Send.ZC_MOVE_SPEED(buff.Target);
		}

		private void SetRateOrFlatModifier(Buff buff, string rateProperty, string flatProperty, string baseProperty, float rateReduction)
		{
			if (PropertyTable.Exists(buff.Target.Properties.Namespace, rateProperty))
			{
				RemovePropertyModifier(buff, buff.Target, flatProperty);
				SetPropertyModifier(buff, buff.Target, rateProperty, rateReduction);
				return;
			}

			if (!PropertyTable.Exists(buff.Target.Properties.Namespace, flatProperty))
				return;

			RemovePropertyModifier(buff, buff.Target, rateProperty);
			var baseValue = MathF.Max(0, buff.Target.Properties.GetFloat(baseProperty));
			SetPropertyModifier(buff, buff.Target, flatProperty, baseValue * rateReduction);
		}

		private void PlayVisibleDebuff(Buff buff)
		{
			Send.ZC_NORMAL.PlayTextEffect(buff.Target, buff.Caster, ShameTextEffect, (float)BuffId.Mourning_Dummy_Debuff, null, "Item");
		}

		private void RefreshVisualEffect(Buff buff)
		{
			buff.Target.AddEffect(ShameVisualEffectKey, new AttachEffect(ShameVisualEffect, 1f, EffectLocation.Bottom));
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.GraveChill_Debuff)]
	public class GraveChill_DebuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			Send.ZC_NORMAL.PlayTextEffect(buff.Target, buff.Caster, "SHOW_BUFF_TEXT", (float)BuffId.GraveChill_Debuff, null);
		}

		public override void OnEnd(Buff buff)
		{
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.RestingGround_Buff)]
	public class RestingGround_BuffOverride : BuffHandler, IBuffCombatDefenseAfterCalcHandler
	{
		private const string ShieldValueKey = "Melia.Kneller.RestingGround.Shield";
		private const float ShieldRate = 0.20f;

		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			var shield = MathF.Floor(buff.Target.Properties.GetFloat(PropertyName.MHP) * ShieldRate);
			buff.Vars.SetFloat(ShieldValueKey, shield);
			Send.ZC_UPDATE_SHIELD(buff.Target, (long)shield, 1);
		}

		public override void OnEnd(Buff buff)
		{
			buff.Vars.Remove(ShieldValueKey);
			Send.ZC_UPDATE_SHIELD(buff.Target, 0, 1);
		}

		public void OnDefenseAfterCalc(Buff buff, ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			var shield = buff.Vars.GetFloat(ShieldValueKey);
			if (shield <= 0)
			{
				target.RemoveBuff(BuffId.RestingGround_Buff);
				return;
			}

			var absorbed = MathF.Min(shield, skillHitResult.Damage);
			skillHitResult.Damage -= absorbed;
			shield -= absorbed;

			buff.Vars.SetFloat(ShieldValueKey, shield);
			Send.ZC_UPDATE_SHIELD(target, (long)shield, 1);

			if (skillHitResult.Damage <= 0)
			{
				skillHitResult.Effect = HitEffect.SAFETY;
				skillHitResult.Result = HitResultType.Miss;
			}

			if (shield <= 0)
				target.RemoveBuff(BuffId.RestingGround_Buff);
		}
	}
}
