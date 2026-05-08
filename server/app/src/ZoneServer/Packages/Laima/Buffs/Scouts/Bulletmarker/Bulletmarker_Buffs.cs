using System;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Scouts.Bulletmarker;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Yggdrasil.Util;

namespace Melia.Zone.Buffs.Handlers.Scouts.Bulletmarker
{
	[Package("laima")]
	[BuffHandler(BuffId.Outrage_Buff)]
	public class Outrage_BuffOverride : BuffHandler
	{
		private static readonly TimeSpan AfterMoveDuration = TimeSpan.FromSeconds(4);
		private static readonly TimeSpan CooldownAfterEnd = TimeSpan.FromSeconds(25);
		private const float MovingShotBonus = 2.5f;
		private const float BaseAttackSpeedBonus = 105f;
		private const float AttackSpeedBonusPerLevel = 3f;
		private const float BaseDoubleGunFinalDamageBonus = 0.50f;
		private const float DoubleGunFinalDamageBonusPerLevel = 0.03f;
		private const float FixedMoveSpeed = 73f;
		private const float AfterFixedMoveSpeed = 18f;

		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			buff.Target.RemoveBuff(BuffId.FreezeBullet_Buff);
			buff.Target.RemoveBuff(BuffId.SilverBullet_Buff);

			AddPropertyModifier(buff, buff.Target, PropertyName.MovingShot_BM, MovingShotBonus);
			AddPropertyModifier(buff, buff.Target, PropertyName.NormalASPD_BM, -GetAttackSpeedBonus((int)buff.NumArg1));
			AddPropertyModifier(buff, buff.Target, PropertyName.FIXMSPD_BM, FixedMoveSpeed);

			if (buff.Target is Character character)
			{
				Send.ZC_OBJECT_PROPERTY(character);
				Send.ZC_MOVE_SPEED(character);
			}
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.MovingShot_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.NormalASPD_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.FIXMSPD_BM);

			BulletmarkerSkillHelper.StartOutrageEndCooldown(buff.Target, CooldownAfterEnd);

			buff.Target.StartBuff(BuffId.MoveSpeedFix, AfterFixedMoveSpeed, 0, AfterMoveDuration, buff.Target, SkillId.Bulletmarker_Outrage);

			if (buff.Target is Character character)
			{
				Send.ZC_OBJECT_PROPERTY(character);
				Send.ZC_MOVE_SPEED(character);
			}
		}

		[CombatCalcModifier(CombatCalcPhase.BeforeCalc, BuffId.Outrage_Buff)]
		public void OnBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult result)
		{
			if (skill.Id != SkillId.DoubleGun_Attack)
				return;

			var outrageLevel = attacker.TryGetSkill(SkillId.Bulletmarker_Outrage, out var outrageSkill)
				? outrageSkill.Level
				: 1;

			modifier.FinalDamageMultiplier *= 1f + GetDoubleGunFinalDamageBonus(outrageLevel);
		}

		private static float GetAttackSpeedBonus(int skillLevel)
			=> BaseAttackSpeedBonus + Math.Max(1, skillLevel) * AttackSpeedBonusPerLevel;

		private static float GetDoubleGunFinalDamageBonus(int skillLevel)
			=> BaseDoubleGunFinalDamageBonus + Math.Max(1, skillLevel) * DoubleGunFinalDamageBonusPerLevel;
	}

	[Package("laima")]
	[BuffHandler(BuffId.FreezeBullet_Buff)]
	public class FreezeBullet_BuffOverride : BuffHandler
	{
		private const int FreezeChance = 30;
		private const float FogRadius = 45f;
		private static readonly TimeSpan FreezeDuration = TimeSpan.FromSeconds(2);
		private static readonly TimeSpan ChillDuration = TimeSpan.FromSeconds(5);

		public override void WhileActive(Buff buff)
		{
			if (buff.Target.IsBuffActive(BuffId.Outrage_Buff))
				return;

			if (!buff.Target.IsAbilityActive(AbilityId.Bulletmarker16))
				return;

			var area = new Circle(buff.Target.Position, FogRadius);
			foreach (var target in buff.Target.Map.GetAttackableEnemiesIn(buff.Target, area))
				ApplyChill(buff, target);
		}

		[CombatCalcModifier(CombatCalcPhase.AfterCalc, BuffId.FreezeBullet_Buff)]
		public void OnAfterCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult result)
		{
			if (skill.Id != SkillId.DoubleGun_Attack && skill.Id != SkillId.Pistol_Attack && skill.Id != SkillId.Pistol_Attack2)
				return;

			if (attacker.IsBuffActive(BuffId.Outrage_Buff))
				return;

			if (result.Damage <= 0)
				return;

			if (RandomProvider.Get().Next(100) >= FreezeChance)
				return;

			target.StartBuff(BuffId.Freeze, 1, 0, FreezeDuration, attacker);
		}

		private static void ApplyChill(Buff buff, ICombatEntity target)
		{
			var chill = target.StartBuff(BuffId.FreezeBullet_Cold_Debuff, buff.NumArg1, 0, ChillDuration, buff.Target, buff.SkillId);

			if (chill.OverbuffCounter >= 4)
			{
				target.StartBuff(BuffId.Freeze, 1, 0, FreezeDuration, buff.Target, buff.SkillId);
				target.RemoveBuff(BuffId.FreezeBullet_Cold_Debuff);
			}
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.SilverBullet_Buff)]
	public class SilverBullet_BuffOverride : BuffHandler
	{
		[CombatCalcModifier(CombatCalcPhase.BeforeCalc, BuffId.SilverBullet_Buff)]
		public void OnBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult result)
		{
			if (skill.Data.AttackType == SkillAttackType.Gun || skill.Id == SkillId.DoubleGun_Attack)
				modifier.AttackAttribute = AttributeType.Holy;
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.TracerBullet_Buff)]
	public class TracerBullet_BuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			AddPropertyModifier(buff, buff.Target, PropertyName.HR_BM, 50 + buff.NumArg1 * 5);

			if (buff.Target is Character character)
				Send.ZC_OBJECT_PROPERTY(character);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.HR_BM);

			if (buff.Target is Character character)
				Send.ZC_OBJECT_PROPERTY(character);
		}
	}
}
