using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Buffs;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.Components;
using Melia.Zone.World.Actors.CombatEntities.Components;
using Melia.Zone.World.Actors.Monsters;
using Yggdrasil.Util;

namespace Melia.Zone.Skills.Handlers.Archers.PiedPiper
{
	internal static class PiedPiperSkillHelper
	{
		public const int MouseId = 300004;
		public const int RareMouseId = 300005;
		public const float SongRange = 120f;
		public const int MaxMice = 5;
		private const int BestFriendMaxStacks = 3;
		private const float BestFriendAttackRange = 170f;
		private static readonly TimeSpan BestFriendStackDuration = TimeSpan.FromMinutes(10);
		private static readonly TimeSpan BestFriendDuration = TimeSpan.FromSeconds(35);
		private static readonly TimeSpan BestFriendRareDuration = TimeSpan.FromSeconds(50);
		private static readonly TimeSpan BestFriendAttackInterval = TimeSpan.FromSeconds(3);
		private static readonly TimeSpan ARatDuration = TimeSpan.FromSeconds(10);

		public static bool TryStart(Skill skill, ICombatEntity caster, Position originPos, Position farPos)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Melia.Shared.L10N.Localization.Get("Not enough SP."));
				return false;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);
			Send.ZC_SKILL_READY(caster, skill, ZoneServer.Instance.World.CreateSkillHandle(), originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos, ForceId.GetNew(), null);
			return true;
		}

		public static List<ICombatEntity> GetEnemiesInSongRange(ICombatEntity caster, float radius = SongRange)
			=> caster.SelectObjects(caster.Position, radius, RelationType.Enemy)
				.Concat(caster.Map.GetAttackableEnemiesInPosition(caster, caster.Position, radius))
				.Where(enemy => enemy != null && !enemy.IsDead && !enemy.IsHighFlying())
				.GroupBy(enemy => enemy.Handle)
				.Select(group => group.First())
				.OrderBy(enemy => caster.Position.Get2DDistance(enemy.Position))
				.ToList();

		public static List<ICombatEntity> GetEnemiesFromHitListOrRange(ICombatEntity caster, IEnumerable<ICombatEntity> hitList, float radius = SongRange)
			=> (hitList ?? [])
				.Where(enemy => enemy != null && !enemy.IsDead && !enemy.IsHighFlying())
				.Concat(GetEnemiesInSongRange(caster, radius))
				.GroupBy(enemy => enemy.Handle)
				.Select(group => group.First())
				.OrderBy(enemy => caster.Position.Get2DDistance(enemy.Position))
				.ToList();

		public static Buff StartVisibleBuff(ICombatEntity target, BuffId buffId, float numArg1, float numArg2, TimeSpan duration, ICombatEntity caster, SkillId skillId)
		{
			var buff = target.StartBuff(buffId, numArg1, numArg2, duration, caster, skillId);
			if (buff != null)
			{
				buff.NotifyUpdate();
				if (target is Character character)
					Send.ZC_BUFF_LIST(character);
				SafePlayTextEffect(target, caster, "SHOW_BUFF_TEXT", (float)buffId);
			}

			return buff;
		}

		public static void SafePlaySound(IActor actor, string soundName, bool loop = false)
		{
			try { actor.PlaySound(soundName, loop); }
			catch (ArgumentException) { }
		}

		public static void SafeStopSound(IActor actor, string soundName)
		{
			try { actor.StopSound(soundName); }
			catch (ArgumentException) { }
		}

		public static void SafePlayEffect(IActor actor, string effectName, float scale = 1f)
		{
			try { actor.PlayEffect(effectName, scale); }
			catch (ArgumentException) { }
		}

		public static void SafePlayEffectToGround(IActor actor, string effectName, Position position, float scale = 1f, int lifeTime = 0)
		{
			try { _ = actor.PlayEffectToGround(effectName, position, scale, lifeTime); }
			catch (ArgumentException) { }
		}

		public static void SafePlayTextEffect(ICombatEntity target, IActor caster, string effectName, float value)
		{
			try { Send.ZC_NORMAL.PlayTextEffect(target, caster, effectName, value, null); }
			catch (ArgumentException) { }
		}

		public static List<ICombatEntity> GetAttackableEnemiesAt(ICombatEntity caster, Skill skill, Position position, float radius)
		{
			var param = skill.GetSplashParameters(caster, position, position, length: 0, width: radius, angle: 0);
			var area = skill.GetSplashArea(SplashType.Circle, param);
			return caster.Map.GetAttackableEnemiesIn(caster, area);
		}

		public static List<ICombatEntity> GetPartyTargets(ICombatEntity caster, float radius = SongRange)
		{
			var result = new List<ICombatEntity> { caster };

			if (caster is Character character && character.Connection?.Party != null)
				result.AddRange(caster.Map.GetPartyMembersInRange(character, radius, true).Where(member => member.Handle != caster.Handle));

			return result;
		}

		public static void SummonMouseFromSong(ICombatEntity caster, Skill skill)
		{
			// Best Friend is now linked only to Symphony of Fate results.
			return;
		}

		public static void AddBestFriendStack(Character character, Skill skill)
		{
			if (GetMice(character).Count > 0)
				return;

			var current = character.TryGetBuff(BuffId.HamelnNagetier_Buff, out var existing)
				? existing.OverbuffCounter
				: 0;
			var next = Math.Min(BestFriendMaxStacks, current + 1);
			var buff = character.StartBuff(BuffId.HamelnNagetier_Buff, 1, 0, BestFriendStackDuration, character, skill.Id);
			if (buff != null)
			{
				buff.OverbuffCounter = next;
				buff.NotifyUpdate();
			}

			if (next >= BestFriendMaxStacks)
			{
				ClearBestFriendStacks(character);
				SummonBestFriend(character, skill);
			}
		}

		public static void ClearBestFriendStacks(Character character)
		{
			if (GetMice(character).Count > 0)
				return;

			if (character.IsBuffActive(BuffId.HamelnNagetier_Buff))
				character.RemoveBuff(BuffId.HamelnNagetier_Buff);
		}

		private static void SummonBestFriend(Character character, Skill triggerSkill)
		{
			if (GetMice(character).Count > 0 || !character.TryGetSkill(SkillId.PiedPiper_HamelnNagetier, out var bestFriendSkill))
				return;

			var rare = character.IsAbilityActive(AbilityId.PiedPiper13) && RandomProvider.Get().Next(100) < 25;
			var monsterId = rare ? RareMouseId : MouseId;
			var duration = rare ? BestFriendRareDuration : BestFriendDuration;
			var summon = new Summon(character, monsterId, RelationType.Friendly)
			{
				Position = character.Position.GetRandomInRange2D(20, RandomProvider.Get()),
				Direction = character.Direction,
				Map = character.Map,
				OwnerHandle = character.Handle,
				Faction = character.Faction,
			};

			summon.Properties.SetFloat(PropertyName.FIXMSPD_BM, 80f);
			summon.Properties.SetFloat(PropertyName.HR_BM, 999999f);
			summon.Components.Add(new LifeTimeComponent(summon, duration));
			summon.SetState(true);
			summon.Vars.SetBool("EnableAIOutOfPC", true);
			summon.Vars.SetFloat("Melia.PiedPiper.BestFriend.Rare", rare ? 1f : 0f);

			character.Summons.AddSummon(summon);
			character.Map.AddMonster(summon);
			if (rare)
				summon.ChangeScale(2f, 0);

			var durationBuff = StartVisibleBuff(character, BuffId.HamelnNagetier_Buff, rare ? 2 : 1, 0, duration, character, bestFriendSkill.Id);
			if (durationBuff != null)
			{
				durationBuff.OverbuffCounter = 1;
				durationBuff.NotifyUpdate();
				Send.ZC_BUFF_LIST(character);
			}
			summon.StartBuff(BuffId.Ability_buff_PC_Summon, TimeSpan.Zero, summon);
			SafePlayEffect(summon, "E_archer_HamelnNagetier##2.0", 1f);

			bestFriendSkill.Run(RunBestFriend(character, summon, bestFriendSkill, duration));
		}

		private static async System.Threading.Tasks.Task RunBestFriend(Character owner, Summon summon, Skill skill, TimeSpan duration)
		{
			var endAt = DateTimeOffset.UtcNow + duration;
			while (!owner.IsDead && !summon.IsDead && summon.Map != null && DateTimeOffset.UtcNow < endAt)
			{
				var enemy = GetBestFriendTarget(owner, summon);

				if (enemy != null)
					BestFriendAttack(owner, summon, skill, enemy);
				else
					MoveSummonNearOwner(owner, summon);

				await skill.Wait(BestFriendAttackInterval);
			}

			owner.Summons.RemoveSummon(summon);
			summon.Map?.RemoveMonster(summon);
			owner.RemoveBuff(BuffId.HamelnNagetier_Buff);
			owner.RemoveBuff(BuffId.BestFriend_Duration_Buff);
		}

		private static ICombatEntity GetBestFriendTarget(Character owner, Summon summon)
		{
			if (owner.Variables.Temp.TryGetInt(PiedPiperHamelnNagetier.LastTargetVar, out var targetHandle) && targetHandle > 0)
			{
				var preferred = owner.Map.GetAttackableEnemiesInPosition(owner, owner.Position, BestFriendAttackRange * 2f)
					.Concat(owner.Map.GetAttackableEnemiesInPosition(owner, summon.Position, BestFriendAttackRange * 2f))
					.FirstOrDefault(target => target.Handle == targetHandle && !target.IsDead && !target.IsHighFlying());

				if (preferred != null)
					return preferred;
			}

			return owner.Map.GetAttackableEnemiesInPosition(owner, summon.Position, BestFriendAttackRange)
					.Concat(owner.Map.GetAttackableEnemiesInPosition(owner, owner.Position, BestFriendAttackRange))
					.Where(target => target != null && !target.IsDead)
					.GroupBy(target => target.Handle)
					.Select(group => group.First())
					.OrderBy(target => summon.Position.Get2DDistance(target.Position))
					.FirstOrDefault();
		}

		private static void MoveSummonNearOwner(Character owner, Summon summon)
		{
			var startPos = summon.Position;
			var endPos = owner.Position.GetRandomInRange2D(25, RandomProvider.Get());
			summon.Direction = summon.Position.GetDirection(endPos);
			Send.ZC_MOVE_POS(summon, startPos, endPos, 80, 0.3f);
			summon.Position = endPos;
		}

		private static void BestFriendAttack(Character owner, Summon summon, Skill skill, ICombatEntity enemy)
		{
			SafePlayEffect(enemy, "F_hit003_slash##0.4", 0.7f);
			ApplyBestFriendMark(owner, skill, enemy, summon.Vars.GetFloat("Melia.PiedPiper.BestFriend.Rare") > 0);
		}

		public static void ApplyBestFriendMark(Character owner, Skill skill, ICombatEntity enemy, bool rare)
		{
			if (owner == null || enemy == null || enemy.IsDead)
				return;

			var bestFriendSkill = owner.TryGetSkill(SkillId.PiedPiper_HamelnNagetier, out var learnedSkill)
				? learnedSkill
				: skill;
			var rareMarkMultiplier = rare ? 1.015f : 1f;
			enemy.StartBuff(BuffId.HamelnNagetier_Debuff, bestFriendSkill.Level, rareMarkMultiplier, ARatDuration, owner, bestFriendSkill.Id);
		}

		public static List<Summon> GetMice(Character character)
			=> character.Summons.GetSummons(MouseId, RareMouseId);

		public static void RemoveMice(Character character)
		{
			foreach (var summon in GetMice(character))
			{
				character.Summons.RemoveSummon(summon);
				summon.Map?.RemoveMonster(summon);
			}

			character.RemoveBuff(BuffId.HamelnNagetier_Buff);
			character.RemoveBuff(BuffId.BestFriend_Duration_Buff);
		}

		private static int GetMouseMonsterId(Character character)
		{
			if (!character.TryGetActiveAbilityLevel(AbilityId.PiedPiper13, out var level))
				return MouseId;

			return RandomProvider.Get().Next(100) < level * 2 ? RareMouseId : MouseId;
		}

		private static void RefreshMouseBuff(Character character, Skill skill)
		{
			var count = Math.Min(MaxMice, GetMice(character).Count);
			character.StartBuff(BuffId.HamelnNagetier_Buff, 1, 0, TimeSpan.FromSeconds(60), character, skill.Id);

			if (character.TryGetBuff(BuffId.HamelnNagetier_Buff, out var buff))
				buff.OverbuffCounter = Math.Max(1, count);
		}
	}
}

namespace Melia.Zone.Buffs.Handlers.Archers.PiedPiper
{
	[Package("laima")]
	[BuffHandler(BuffId.Fluting_Buff)]
	public class FlutingBuffHandler : BuffHandler, IBuffBeforeKnockbackHandler, IBuffBeforeKnockdownHandler
	{
		public KnockResult OnBeforeKnockback(Buff buff, ICombatEntity attacker, ICombatEntity target) => KnockResult.Prevent;
		public KnockResult OnBeforeKnockdown(Buff buff, ICombatEntity attacker, ICombatEntity target) => KnockResult.Prevent;
	}

	[Package("laima")]
	[BuffHandler(BuffId.Dissonanz_Stun_Debuff)]
	public class DissonanzStunDebuffHandler : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			buff.Target.AddState(StateType.Stunned);
		}

		public override void OnEnd(Buff buff)
		{
			buff.Target.RemoveState(StateType.Stunned);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.HamelnNagetier_Buff)]
	public class HamelnNagetierBuffHandler : BuffHandler
	{
		public override void OnEnd(Buff buff)
		{
			if (buff.Target is Character character)
				Melia.Zone.Skills.Handlers.Archers.PiedPiper.PiedPiperSkillHelper.RemoveMice(character);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.HamelnNagetier_Debuff)]
	public class ARatDebuffHandler : BuffHandler, IBuffCombatDefenseAfterCalcHandler
	{
		public void OnDefenseAfterCalc(Buff buff, ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (buff.Caster == null || attacker == null || attacker.Handle != buff.Caster.Handle || skillHitResult.Damage <= 0)
				return;

			if (skill == null || skill.Data.ClassName.Contains("DOT", StringComparison.OrdinalIgnoreCase) || skill.Data.ClassName.Contains("Dot", StringComparison.OrdinalIgnoreCase))
				return;

			var skillLevel = 1;
			Skill bestFriendSkill = null;
			if (attacker.TryGetSkill(SkillId.PiedPiper_HamelnNagetier, out bestFriendSkill))
				skillLevel = Math.Clamp(bestFriendSkill.Level, 1, 10);

			var markMultiplier = MathF.Max(1f, buff.NumArg2);
			var factor = 300f + ((skillLevel - 1) * (3500f - 300f) / 9f);
			var physicalAtk = (attacker.Properties.GetFloat(PropertyName.MINPATK) + attacker.Properties.GetFloat(PropertyName.MAXPATK)) / 2f;
			var magicAtk = (attacker.Properties.GetFloat(PropertyName.MINMATK) + attacker.Properties.GetFloat(PropertyName.MAXMATK)) / 2f;
			var baseAtk = MathF.Max(physicalAtk, magicAtk);
			var damage = baseAtk * (factor / 100f) * markMultiplier;
			if (damage <= 0)
				return;

			target.RemoveBuff(BuffId.HamelnNagetier_Debuff);
			target.TakeDamage(damage, attacker);
			Send.ZC_HIT_INFO(attacker, target, new HitInfo(attacker, target, bestFriendSkill ?? skill, new SkillHitResult
			{
				Damage = damage,
				Result = HitResultType.Hit,
				Effect = HitEffect.Impact,
			}));
			Melia.Zone.Skills.Handlers.Archers.PiedPiper.PiedPiperSkillHelper.SafePlayEffect(target, "F_hit003_slash##0.4", 0.8f);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Lullaby_Debuff)]
	public class LullabyDebuffHandler : BuffHandler, IBuffCombatDefenseAfterCalcHandler
	{
		private const int DrowsyDurationSeconds = 15;

		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			buff.Target.AddState(StateType.Sleep);
			Melia.Zone.Skills.Handlers.Archers.PiedPiper.PiedPiperSkillHelper.SafePlayTextEffect(buff.Target, buff.Caster, "SHOW_BUFF_TEXT", (float)BuffId.Lullaby_Debuff);
		}

		[CombatCalcModifier(CombatCalcPhase.BeforeCalc, BuffId.Lullaby_Debuff)]
		public void OnDefenseBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult result)
		{
			if (!target.IsBuffActive(BuffId.Lullaby_Debuff))
				return;

			modifier.ForcedHit = true;
			modifier.Unblockable = true;
			modifier.ForcedCritical = true;
		}

		public void OnDefenseAfterCalc(Buff buff, ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (skillHitResult.Damage <= 0)
				return;

			if (buff.Caster is ICombatEntity caster && caster.IsAbilityActive(AbilityId.PiedPiper3))
				target.RemoveRandomBuff(33);

			target.RemoveBuff(BuffId.Lullaby_Debuff);
		}

		public override void OnEnd(Buff buff)
		{
			buff.Target.RemoveState(StateType.Sleep);

			if (buff.Target is ICombatEntity target)
				target.StartBuff(BuffId.Wiegenlied_Debuff, buff.NumArg1, 0, TimeSpan.FromSeconds(DrowsyDurationSeconds), buff.Caster);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Wiegenlied_Debuff)]
	public class WiegenliedDebuffHandler : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			var reduceHr = buff.Target.Properties.GetFloat(PropertyName.HR) * 0.5f;
			AddPropertyModifier(buff, buff.Target, PropertyName.HR_BM, -reduceHr);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.HR_BM);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Fluting_DeBuff)]
	public class FlutingDebuffHandler : BuffHandler
	{
		private const float FixedMovementSpeed = 20f;
		private const float FirstFollowerDistance = 55f;
		private const float FollowerSpacing = 35f;
		private const float RetargetThreshold = 24f;

		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			AddPropertyModifier(buff, buff.Target, PropertyName.FIXMSPD_BM, FixedMovementSpeed);
			buff.Target.AddState(StateType.Fluting);
			Send.ZC_MOVE_SPEED(buff.Target);
			Melia.Zone.Skills.Handlers.Archers.PiedPiper.PiedPiperSkillHelper.SafePlayTextEffect(buff.Target, buff.Caster, "SHOW_BUFF_TEXT", (float)BuffId.Fluting_DeBuff);
		}

		public override void WhileActive(Buff buff)
		{
			if (buff.Target is not ICombatEntity target || buff.Caster is not ICombatEntity caster)
				return;

			var slotIndex = Math.Max(0, (int)buff.NumArg2);
			var destination = caster.Position.GetRelative(caster.Direction.Backwards, FirstFollowerDistance + slotIndex * FollowerSpacing);
			var distance = target.Position.Get2DDistance(destination);
			if (distance <= 18)
				return;

			var movement = target.Components.Get<MovementComponent>();
			if (movement == null)
				return;

			var lastCommanded = movement.IsMoving ? movement.FinalDestination : target.Position;
			if (lastCommanded.Get2DDistance(destination) < RetargetThreshold)
				return;

			movement.MoveTo(destination);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.FIXMSPD_BM);
			buff.Target.RemoveState(StateType.Fluting);
			Send.ZC_MOVE_SPEED(buff.Target);
		}

		[CombatCalcModifier(CombatCalcPhase.BeforeCalc, BuffId.Fluting_DeBuff)]
		public void OnDefenseBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult result)
		{
			if (!target.IsBuffActive(BuffId.Fluting_DeBuff))
				return;

			modifier.ForcedHit = true;
			modifier.Unblockable = true;
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Friedenslied_Buff)]
	public class FriedensliedBuffHandler : BuffHandler
	{
		private const float MovingArtSpeed = 20f;

		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			if (buff.NumArg2 > 0)
			{
				AddPropertyModifier(buff, buff.Target, PropertyName.FIXMSPD_BM, MovingArtSpeed);
				Send.ZC_MOVE_SPEED(buff.Target);
			}
			else
			{
				this.PlayHop(buff);
			}

			Melia.Zone.Skills.Handlers.Archers.PiedPiper.PiedPiperSkillHelper.SafePlayTextEffect(buff.Target, buff.Caster, "SHOW_BUFF_TEXT", (float)BuffId.Friedenslied_Buff);
		}

		public override void WhileActive(Buff buff)
		{
			this.PlayHop(buff);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.FIXMSPD_BM);
			Send.ZC_MOVE_SPEED(buff.Target);
		}

		private void PlayHop(Buff buff)
		{
			if (buff.Target is IActor actor)
				Send.ZC_NORMAL.LeapJump(actor, buff.Target.Position, 6f, 0.1f, 0.1f, 0.15f, 0.1f, 3f);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Friedenslied_Debuff, BuffId.Friedenslied_Abil_Debuff)]
	public class FriedensliedDebuffHandler : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			this.PlayHop(buff);
			Melia.Zone.Skills.Handlers.Archers.PiedPiper.PiedPiperSkillHelper.SafePlayTextEffect(buff.Target, buff.Caster, "SHOW_BUFF_TEXT", (float)buff.Id);
		}

		public override void WhileActive(Buff buff)
		{
			this.PlayHop(buff);
		}

		public override void OnEnd(Buff buff)
		{
		}

		private void PlayHop(Buff buff)
		{
			if (buff.Target is IActor actor)
				Send.ZC_NORMAL.LeapJump(actor, buff.Target.Position, 6f, 0.1f, 0.1f, 0.15f, 0.1f, 3f);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.LiedDerWeltbaum_Buff)]
	public class LiedDerWeltbaumBuffHandler : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			Melia.Zone.Skills.Handlers.Archers.PiedPiper.PiedPiperSkillHelper.SafePlayTextEffect(buff.Target, buff.Caster, "SHOW_BUFF_TEXT", (float)BuffId.LiedDerWeltbaum_Buff);
		}

		[CombatCalcModifier(CombatCalcPhase.BeforeCalc, BuffId.LiedDerWeltbaum_Buff)]
		public void OnAttackBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult result)
		{
			if (attacker.TryGetBuff(BuffId.LiedDerWeltbaum_Buff, out var buff))
				modifier.DamageMultiplier += buff.NumArg2;
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Marschierendeslied_Buff)]
	public class MarschierendesliedBuffHandler : BuffHandler, IBuffBeforeKnockbackHandler, IBuffBeforeKnockdownHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			Melia.Zone.Skills.Handlers.Archers.PiedPiper.PiedPiperSkillHelper.SafePlayTextEffect(buff.Target, buff.Caster, "SHOW_BUFF_TEXT", (float)BuffId.Marschierendeslied_Buff);
		}

		public KnockResult OnBeforeKnockback(Buff buff, ICombatEntity attacker, ICombatEntity target)
		{
			this.ConsumeCount(buff);
			return KnockResult.Prevent;
		}

		public KnockResult OnBeforeKnockdown(Buff buff, ICombatEntity attacker, ICombatEntity target)
		{
			this.ConsumeCount(buff);
			return KnockResult.Prevent;
		}

		private void ConsumeCount(Buff buff)
		{
			buff.OverbuffCounter--;
			buff.NotifyUpdate();

			if (buff.OverbuffCounter <= 0)
				buff.Target.RemoveBuff(BuffId.Marschierendeslied_Buff);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Allegro_Buff)]
	public class AllegroBuffHandler : BuffHandler
	{
		private const float MoveSpeedBonus = 15f;

		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			AddPropertyModifier(buff, buff.Target, PropertyName.MSPD_BM, MoveSpeedBonus);
			Send.ZC_MOVE_SPEED(buff.Target);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.MSPD_BM);
			Send.ZC_MOVE_SPEED(buff.Target);
		}
	}
}
