using System;
using System.Collections.Generic;
using Melia.Shared.Game.Const;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Archers.Wugushi
{
	public static class WugushiSkillHelper
	{
		private const float IntScalingExponent = 1.173f;

		public const float MaxIntScaledRadius = 211.2f;
		public const float WugongGuMaxInt = 1900f;
		public const float WideMiasmaMaxInt = 1900f;
		public const float WideMiasmaRadius = 120f;
		public const float CrescendoBaneMaxInt = 1000f;
		public const float WugongGuMaxRadius = MaxIntScaledRadius / 2f;
		public const float WideMiasmaMaxStealthSeconds = 35f;
		public const float WideMiasmaMaxMoveSpeed = 15f;
		public const float HemotoxicMiasmaHealingReductionPercent = 60f;

		private static readonly HashSet<SkillId> WugushiSkillIds =
		[
			SkillId.Wugushi_JincanGu,
			SkillId.Wugushi_JincanGuBug,
			SkillId.Wugushi_LatentVenom,
			SkillId.Wugushi_NeedleBlow,
			SkillId.Wugushi_ThrowGuPot,
			SkillId.Wugushi_WideMiasma,
			SkillId.Wugushi_WugongGu,
			SkillId.Wugushi_CrescendoBane,
			SkillId.Wugushi_Zhendu
		];

		private static readonly BuffId[] BleedingEffectBuffIds =
		[
			BuffId.CriticalWound,
			BuffId.HeavyBleeding,
			BuffId.Archer_BleedingToxin_Debuff,
			BuffId.BleedingPierce_Debuff,
			BuffId.BleedingPierce_Abil_Debuff,
			BuffId.Ngadhundi_Wound_Debuff,
			BuffId.CriticalWound_Mon,
			BuffId.Wound_mon,
			BuffId.Weekly_Bramble_Bleed_Debuff,
			BuffId.Field_Bramble_Bleed_Debuff,
			BuffId.Mythic_Bleeding_Buff,
			BuffId.Mythic_Bleeding_Debuff,
			BuffId.Mythic_Bleed_Debuff,
			BuffId.CARD_Wound,
			BuffId.Common_Wound,
			BuffId.Ancient_Baby_Hauberk_Bleed_Debuff,
			BuffId.GLACIER_UNIQUE_ICE_SPEAR_BLEEDING_DEBUFF,
			BuffId.GLACIER_LEGEND_ICE_SPEAR_BLEEDING_DEBUFF,
			BuffId.SWORD_DANCE_BLEEDING_DEBUFF,
			BuffId.RE_HELGASERCLE_BLEEDING_DEBUFF,
			BuffId.UC_bleed,
			BuffId.UC_hemorrhage,
		];

		public static float GetIntScalingRatio(ICombatEntity caster, float maxInt)
		{
			if (caster == null)
				return 1f;

			var intelligence = Math.Clamp(caster.Properties.GetFloat(PropertyName.INT), 0f, maxInt);
			return (float)Math.Pow(intelligence / maxInt, IntScalingExponent);
		}

		public static int GetPoisonMasteryPercent(ICombatEntity caster)
		{
			if (caster == null)
				return 100;

			var intelligence = Math.Clamp(caster.Properties.GetFloat(PropertyName.INT), 0f, WugongGuMaxInt);
			return Math.Clamp((int)MathF.Round((intelligence / WugongGuMaxInt) * 100f), 0, 100);
		}

		public static float GetIntScaledRadius(ICombatEntity caster, float maxInt, float maxRadius = MaxIntScaledRadius)
			=> maxRadius * GetIntScalingRatio(caster, maxInt);

		public static float GetWugongGuSpreadRange(ICombatEntity caster)
			=> GetIntScaledRadius(caster, WugongGuMaxInt, WugongGuMaxRadius);

		public static float GetWideMiasmaRadius(ICombatEntity caster)
			=> WideMiasmaRadius;

		public static float GetCrescendoBaneRadius(ICombatEntity caster, int skillLevel)
		{
			var levelRatio = Math.Clamp(skillLevel / 15f, 0.1f, 1f);
			return GetIntScaledRadius(caster, CrescendoBaneMaxInt) * levelRatio;
		}

		public static TimeSpan GetWideMiasmaStealthDuration(ICombatEntity caster)
			=> TimeSpan.FromSeconds(Math.Max(1f, WideMiasmaMaxStealthSeconds * GetIntScalingRatio(caster, WideMiasmaMaxInt)));

		public static int GetWideMiasmaStealthDurationCaption(ICombatEntity caster)
			=> (int)MathF.Round((float)GetWideMiasmaStealthDuration(caster).TotalSeconds);

		public static float GetWideMiasmaMoveSpeedBonus(ICombatEntity caster)
		{
			var bonus = WideMiasmaMaxMoveSpeed * GetIntScalingRatio(caster, WideMiasmaMaxInt);
			return GetPoisonMasteryPercent(caster) > 0 ? Math.Max(1f, bonus) : 0f;
		}

		public static int GetWideMiasmaMoveSpeedCaption(ICombatEntity caster)
			=> (int)MathF.Round(GetWideMiasmaMoveSpeedBonus(caster));

		public static bool IsWugushiSkill(Skill skill)
			=> skill != null && WugushiSkillIds.Contains(skill.Id);

		public static bool IsBleedingEffectActive(ICombatEntity target)
			=> target != null && (target.IsBuffActiveByKeyword(BuffTag.Wound, BuffTag.HardWound) || target.IsAnyBuffActive(BleedingEffectBuffIds));

		public static void ApplyPoisonMasteryIndicator(ICombatEntity caster)
		{
			var percent = GetPoisonMasteryPercent(caster);
			var buff = caster.StartBuff(BuffId.Poison_Mastery_Buff, percent, 0f, TimeSpan.FromMinutes(30), caster, SkillId.None, mastery =>
			{
				mastery.OverbuffCounter = percent;
			});
			buff?.NotifyUpdate();
		}
	}
}
