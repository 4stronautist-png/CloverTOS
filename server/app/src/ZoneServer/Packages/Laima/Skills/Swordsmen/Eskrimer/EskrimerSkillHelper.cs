using System;
using System.Threading.Tasks;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using static Melia.Zone.Skills.Helpers.SkillDamageHelper;

namespace Melia.Zone.Skills.Handlers.Swordsmen.Eskrimer
{
	public static class EskrimerSkillHelper
	{
		public const float OuvertDamageBonus = 0.20f;
		public const float OuvertAttaqueFactorPenalty = 0.20f;
		public const float SetEclairDefensePenetration = 0.15f;
		public const float PretPasataFinalDamageCap = 0.20f;
		public const float AvantGardeBaseFinalDamageBonus = 0.10f;
		public const float AvantGardeFinalDamageBonusPerLevel = 0.02f;
		public const float ToucherMinCritPerStack = 1f;
		public const float ToucherAccuracyPerStack = 0.02f;
		public const int ToucherMaxStacks = 15;
		public const string StoredPasataSotoLevelVar = "Melia.Eskrimer.StoredPasataSotoLevel";
		public const string PasataSotoAvailableUntilVar = "Melia.Eskrimer.PasataSotoAvailableUntil";

		private static readonly SkillId[] EskrimerSkills =
		[
			SkillId.Escrimeur_AttaqueEnchainee,
			SkillId.Escrimeur_SeptEclairs,
			SkillId.Escrimeur_GrandFente,
			SkillId.Escrimeur_Invitation,
			SkillId.Escrimeur_AvantGarde,
			SkillId.Escrimeur_Rafale,
			SkillId.Escrimeur_PassataSotto,
		];

		public static bool IsEskrimerSkill(Skill skill)
		{
			if (skill == null)
				return false;

			foreach (var skillId in EskrimerSkills)
			{
				if (skill.Id == skillId)
					return true;
			}

			return false;
		}

		public static bool TryBeginGroundSkill(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return false;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);

			var targetHandle = target?.Handle ?? 0;
			Send.ZC_SKILL_READY(caster, skill, 1, originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, targetHandle, originPos, originPos.GetDirection(farPos), Position.Zero);
			QueueSkillMeleeGround(caster, skill, farPos);
			QueueAttackStateRelease(caster, skill);
			return true;
		}

		public static bool TryBeginSelfSkill(Skill skill, ICombatEntity caster, Position originPos)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return false;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);

			Send.ZC_SKILL_READY(caster, skill, 1, originPos, Position.Zero);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, originPos, caster.Direction, Position.Zero);
			QueueSkillMeleeTarget(caster, skill);
			QueueAttackStateRelease(caster, skill);
			return true;
		}

		public static ISplashArea GetForwardSplash(Skill skill, ICombatEntity caster, Position originPos, Position farPos, float length = 80f, float width = 20f, float angle = 10f)
		{
			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, length, width, angle);
			return skill.GetSplashArea(SplashType.Square, splashParam);
		}

		public static SkillModifier CreateModifier(Skill skill, ICombatEntity caster)
		{
			var modifier = new SkillModifier();

			if (skill.Id == SkillId.Escrimeur_SeptEclairs)
				modifier.DefensePenetrationRate = SetEclairDefensePenetration;

			if (skill.Id == SkillId.Escrimeur_Rafale)
				modifier.CritChanceMultiplier *= 2f;

			if (skill.Id == SkillId.Escrimeur_AttaqueEnchainee && caster.IsAbilityActive(AbilityId.Escrimeur106))
				modifier.DamageMultiplier *= 1f - OuvertAttaqueFactorPenalty;

			if (IsEskrimerSkill(skill) && caster.IsBuffActive(BuffId.Ouvert_Buff))
				modifier.DamageMultiplier *= 1f + OuvertDamageBonus;

			if (skill.Id == SkillId.Escrimeur_PassataSotto && caster.TryGetBuff(BuffId.Pret_Buff, out var pret))
				modifier.FinalDamageMultiplier += Math.Min(PretPasataFinalDamageCap, pret.NumArg2);

			return modifier;
		}

		public static async Task AttackForward(ICombatEntity caster, Skill skill, Position originPos, Position farPos, float length, float width, int firstHitDelay, int hitInterval)
		{
			var hitCount = Math.Max(1, skill.Data.MultiHitCount);

			try
			{
				for (var i = 0; i < hitCount; ++i)
				{
					var delay = i == 0 ? firstHitDelay : hitInterval;
					if (delay > 0)
						await skill.Wait(TimeSpan.FromMilliseconds(delay));

					var splashArea = GetForwardSplash(skill, caster, originPos, farPos, length, width);
					await SkillAttack(caster, skill, splashArea, 0, 0, skillModifier: CreateModifier(skill, caster));
				}
			}
			finally
			{
				caster.SetAttackState(false);
			}
		}

		public static void GrantToucher(ICombatEntity caster, Skill skill)
		{
			var duration = TimeSpan.FromSeconds(60);

			if (caster.IsBuffActive(BuffId.Touche_max_Buff))
				return;

			var buff = caster.StartBuff(BuffId.Touche_Buff, skill.Level, 0f, duration, caster, skill.Id);
			if (buff == null)
				return;

			if (buff.OverbuffCounter >= ToucherMaxStacks)
			{
				buff.OverbuffCounter = ToucherMaxStacks;
				buff.NotifyUpdate();
				caster.StopBuff(BuffId.Touche_Buff);
				EnablePasataSotoWindow(caster, TimeSpan.FromSeconds(10));
				caster.StartBuff(BuffId.Touche_max_Buff, skill.Level, 0f, TimeSpan.FromSeconds(10), caster, skill.Id)?.NotifyUpdate();
				return;
			}

			buff.IncreaseDuration(duration);
			caster.StopBuff(BuffId.Touche_max_Buff);
			ClearPasataSotoWindow(caster);
			SetPasataSotoAvailability(caster, false);
			buff.NotifyUpdate();
		}

		public static bool HasMaximumToucher(ICombatEntity caster)
		{
			if (caster.IsBuffActive(BuffId.Touche_max_Buff) || caster.IsBuffActive((BuffId)3325))
				return true;

			if (caster.TryGetBuff(BuffId.Touche_Buff, out var toucher) && toucher.OverbuffCounter >= ToucherMaxStacks)
				return true;

			if (caster is Character character
				&& character.Variables.Temp.TryGetInt(PasataSotoAvailableUntilVar, out var availableUntil)
				&& availableUntil >= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
				return true;

			return false;
		}

		public static bool IsPasataSotoTriggerSkill(SkillId skillId)
		{
			return skillId == SkillId.Escrimeur_PassataSotto;
		}

		public static void EnablePasataSotoWindow(ICombatEntity target, TimeSpan duration)
		{
			if (target is not Character character)
				return;

			var availableUntil = DateTimeOffset.UtcNow.Add(duration).ToUnixTimeSeconds();
			character.Variables.Temp.SetInt(PasataSotoAvailableUntilVar, (int)availableUntil);
			SetPasataSotoAvailability(character, true);
		}

		public static void ClearPasataSotoWindow(ICombatEntity target)
		{
			if (target is Character character)
				character.Variables.Temp.SetInt(PasataSotoAvailableUntilVar, 0);
		}

		public static bool TryUseAvailablePasataSoto(ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!HasMaximumToucher(caster) || caster is not Character character)
				return false;

			if (!TryEnsurePasataSotoSkill(character, out var skill) || skill.Level <= 0)
				return false;

			ClearPasataSotoWindow(caster);
			caster.StopBuff(BuffId.Touche_max_Buff);
			caster.StopBuff(BuffId.Touche_Buff);
			SetPasataSotoAvailability(caster, false);

			if (!TryBeginGroundSkill(skill, caster, originPos, farPos, target))
				return true;

			skill.Run(ExecutePasataSoto(caster, skill, originPos, farPos));
			return true;
		}

		public static bool TryUseAvailablePasataSoto(ICombatEntity caster, Position originPos, Direction direction, ICombatEntity target)
		{
			var farPos = target?.Position ?? originPos.GetRelative2D(direction, 100);
			return TryUseAvailablePasataSoto(caster, originPos, farPos, target);
		}

		public static async Task ExecutePasataSoto(ICombatEntity caster, Skill skill, Position originPos, Position farPos)
		{
			await AttackForward(caster, skill, originPos, farPos, 75, 22, 120, 55);
		}

		public static void SetPasataSotoAvailability(ICombatEntity target, bool enabled)
		{
			if (target is not Character character)
				return;

			if (!TryEnsurePasataSotoSkill(character, out var currentSkill) || currentSkill.Level <= 0)
				return;

			StorePasataSotoLevel(character, currentSkill);
			currentSkill.Properties.InvalidateAll();

			if (enabled)
			{
				Send.ZC_SKILL_ADD(character, currentSkill, false);
				Send.ZC_UPDATE_SKL_SPDRATE_LIST(character, currentSkill);
				Send.ZC_OBJECT_PROPERTY(character.Connection, currentSkill);
				Send.ZC_NORMAL.SetSkillsProperties(character.Connection);
				Send.ZC_NORMAL.UpdateSkillUI(character);
			}
		}

		public static void SuppressPasataSotoIfUnavailable(Character character)
		{
			if (character == null)
				return;

			EnsurePasataSotoSkill(character);

			if (character.Skills.TryGet(SkillId.Escrimeur_PassataSotto, out var skill) && skill.Level > 0)
				StorePasataSotoLevel(character, skill);
		}

		private static void EnsurePasataSotoSkill(Character character)
		{
			TryEnsurePasataSotoSkill(character, out _);
		}

		public static bool TryEnsurePasataSotoSkill(Character character, out Skill skill)
		{
			skill = null;
			if (character == null)
				return false;

			if (character.Skills.TryGet(SkillId.Escrimeur_PassataSotto, out skill) && skill.Level > 0)
			{
				StorePasataSotoLevel(character, skill);
				return true;
			}

			skill = new Skill(character, SkillId.Escrimeur_PassataSotto, GetStoredPasataSotoLevel(character));
			character.Skills.AddSilent(skill);
			StorePasataSotoLevel(character, skill);
			return true;
		}

		private static void StorePasataSotoLevel(Character character, Skill skill)
		{
			var level = Math.Max(1, skill.LevelByDB);
			character.Variables.Temp.SetInt(StoredPasataSotoLevelVar, level);
		}

		private static int GetStoredPasataSotoLevel(Character character)
		{
			if (character.Variables.Temp.TryGetInt(StoredPasataSotoLevelVar, out var level) && level > 0)
				return level;

			return 1;
		}

		public static float GetAvantGardeFinalDamageBonus(Skill skill)
		{
			return AvantGardeBaseFinalDamageBonus + AvantGardeFinalDamageBonusPerLevel * Math.Max(1, skill.Level);
		}

		public static void GrantOuvertIfEnabled(ICombatEntity caster, Skill skill)
		{
			if (caster.IsAbilityActive(AbilityId.Escrimeur106))
				caster.StartBuff(BuffId.Ouvert_Buff, skill.Level, OuvertDamageBonus, TimeSpan.FromSeconds(2), caster, skill.Id);
		}

		private static void QueueAttackStateRelease(ICombatEntity caster, Skill skill)
		{
			var releaseDelay = skill.Properties.ShootTime + TimeSpan.FromMilliseconds(150);
			if (releaseDelay < TimeSpan.FromMilliseconds(250))
				releaseDelay = TimeSpan.FromMilliseconds(250);
			if (releaseDelay > TimeSpan.FromMilliseconds(1500))
				releaseDelay = TimeSpan.FromMilliseconds(1500);

			_ = Task.Run(async () =>
			{
				await Task.Delay(releaseDelay);

				if (!caster.IsDead)
					caster.SetAttackState(false);
			});
		}

		private static void QueueSkillMeleeGround(ICombatEntity caster, Skill skill, Position farPos)
		{
			var forceId = ForceId.GetNew();

			_ = Task.Run(async () =>
			{
				await Task.Delay(150);

				if (!caster.IsDead)
					Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos, forceId, null);
			});
		}

		private static void QueueSkillMeleeTarget(ICombatEntity caster, Skill skill)
		{
			_ = Task.Run(async () =>
			{
				await Task.Delay(150);

				if (!caster.IsDead)
					Send.ZC_SKILL_MELEE_TARGET(caster, skill, caster);
			});
		}
	}
}
