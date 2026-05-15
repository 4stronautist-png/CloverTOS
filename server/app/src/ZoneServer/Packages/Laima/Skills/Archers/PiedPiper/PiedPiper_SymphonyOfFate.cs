using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Buffs;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.Util;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.Components;
using Melia.Zone.World.Actors.CombatEntities.Components;
using Melia.Zone.World.Actors.Effects;
using Yggdrasil.Util;

namespace Melia.Zone.Skills.Handlers.Archers.PiedPiper
{
	[Package("laima")]
	[SkillHandler(SkillId.PiedPiper_Improvisation)]
	public class PiedPiperSymphonyOfFate : ISelfSkillHandler
	{
		private const float PartyRange = 240f;
		private const string DivineSymphonyEffectKey = "Melia.PiedPiper.DivineSymphony.Visual";
		private const string CooldownReductionVar = "Melia.Skill.CooldownReduction";
		private static readonly TimeSpan PositiveDuration = TimeSpan.FromSeconds(20);
		private static readonly TimeSpan NegativeDuration = TimeSpan.FromSeconds(12);

		private readonly WeightedRandom<SymphonyEffect> _table = new();

		public PiedPiperSymphonyOfFate()
		{
			_table.Add(SymphonyEffect.MarchOfTriumph, 11);
			_table.Add(SymphonyEffect.BalladOfSanctuary, 11);
			_table.Add(SymphonyEffect.DanceOfSwiftness, 10);
			_table.Add(SymphonyEffect.GoldenResonance, 10);
			_table.Add(SymphonyEffect.HerosCrescendo, 10);
			_table.Add(SymphonyEffect.IronWaltz, 10);
			_table.Add(SymphonyEffect.EchoOfReversal, 10);
			_table.Add(SymphonyEffect.FestivalOverture, 10);
			_table.Add(SymphonyEffect.FinaleOfResurrection, 10);
			_table.Add(SymphonyEffect.DivineSymphony, 1);
			_table.Add(SymphonyEffect.BrokenTempo, 2);
			_table.Add(SymphonyEffect.CursedChorus, 2);
			_table.Add(SymphonyEffect.DiscordantMelody, 1);
			_table.Add(SymphonyEffect.DanceOfMadness, 1);
			_table.Add(SymphonyEffect.LastWaltz, 1);
		}

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);

			var effect = _table.GetRandomItem();
			this.PlaySkillAnimation(skill, caster, originPos, effect);

			var party = PiedPiperSkillHelper.GetPartyTargets(caster, PartyRange)
				.OfType<Character>()
				.Where(member => member != null && !member.IsDead)
				.GroupBy(member => member.Handle)
				.Select(group => group.First())
				.ToList();

			if (party.Count == 0 && caster is Character self)
				party.Add(self);

			this.PlayEffectCue(caster, effect);

			switch (effect)
			{
				case SymphonyEffect.DivineSymphony:
					this.ApplyAllPositive(skill, caster, party, 1.5f);
					this.ApplyBestFriendResult(skill, caster, true);
					this.ApplyDivineSymphonyVisual(party);
					break;
				case SymphonyEffect.LastWaltz:
					this.ApplyLastWaltz(caster, party);
					this.ApplyBestFriendResult(skill, caster, false);
					break;
				default:
					this.ApplyEffect(skill, caster, party, effect, 1f);
					this.ApplyBestFriendResult(skill, caster, IsPositive(effect));
					break;
			}

			skill.Run(skill.Wait(TimeSpan.FromMilliseconds(250)).ContinueWith(_ =>
			{
				caster.SetAttackState(false);
			}));
			skill.Run(skill.Wait(TimeSpan.FromMilliseconds(900)).ContinueWith(_ =>
			{
				Send.ZC_SKILL_DISABLE(caster);
			}));
		}

		private void PlaySkillAnimation(Skill skill, ICombatEntity caster, Position originPos, SymphonyEffect effect)
		{
			if (effect == SymphonyEffect.DivineSymphony)
				this.PlayDissonanzAnimation(skill, caster, originPos);
			else
				this.PlayHamelnAnimation(skill, caster, originPos);
		}

		private void PlayHamelnAnimation(Skill skill, ICombatEntity caster, Position originPos)
		{
			var visualSkill = caster.TryGetSkill(SkillId.PiedPiper_HamelnNagetier, out var hamelnSkill)
				? hamelnSkill
				: new Skill(caster, SkillId.PiedPiper_HamelnNagetier, 1);

			var farPos = caster.Position;
			Send.ZC_SKILL_READY(caster, visualSkill, ZoneServer.Instance.World.CreateSkillHandle(), originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_GROUND(caster, visualSkill, farPos, ForceId.GetNew(), null);
		}

		private void PlayDissonanzAnimation(Skill skill, ICombatEntity caster, Position originPos)
		{
			var visualSkill = caster.TryGetSkill(SkillId.PiedPiper_Dissonanz, out var dissonanzSkill)
				? dissonanzSkill
				: new Skill(caster, SkillId.PiedPiper_Dissonanz, 1);

			var farPos = caster.Position;
			Send.ZC_SKILL_READY(caster, visualSkill, ZoneServer.Instance.World.CreateSkillHandle(), originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_GROUND(caster, visualSkill, farPos, ForceId.GetNew(), null);
		}

		private void ApplyEffect(Skill skill, ICombatEntity caster, List<Character> party, SymphonyEffect effect, float multiplier)
		{
			foreach (var member in party)
			{
				switch (effect)
				{
					case SymphonyEffect.MarchOfTriumph:
						Start(member, BuffId.Symphony_MarchOfTriumph_Buff, skill, PositiveDuration, caster, multiplier);
						break;
					case SymphonyEffect.BalladOfSanctuary:
						Start(member, BuffId.Symphony_BalladOfSanctuary_Buff, skill, PositiveDuration, caster, multiplier);
						break;
					case SymphonyEffect.DanceOfSwiftness:
						Start(member, BuffId.Symphony_DanceOfSwiftness_Buff, skill, PositiveDuration, caster, multiplier);
						break;
					case SymphonyEffect.GoldenResonance:
						Start(member, BuffId.Symphony_GoldenResonance_Buff, skill, NegativeDuration, caster, multiplier);
						break;
					case SymphonyEffect.HerosCrescendo:
						Start(member, BuffId.Symphony_HerosCrescendo_Buff, skill, PositiveDuration, caster, multiplier);
						break;
					case SymphonyEffect.IronWaltz:
						Start(member, BuffId.Symphony_IronWaltz_Buff, skill, PositiveDuration, caster, multiplier);
						break;
					case SymphonyEffect.EchoOfReversal:
						Start(member, BuffId.Symphony_EchoOfReversal_Buff, skill, PositiveDuration, caster, multiplier);
						break;
					case SymphonyEffect.FestivalOverture:
						Start(member, BuffId.Symphony_FestivalOverture_Buff, skill, PositiveDuration, caster, multiplier);
						break;
					case SymphonyEffect.FinaleOfResurrection:
						var buff = Start(member, BuffId.Symphony_FinaleOfResurrection_Buff, skill, PositiveDuration, caster, multiplier);
						if (buff != null)
							buff.OverbuffCounter = 1;
						break;
					case SymphonyEffect.BrokenTempo:
						Start(member, BuffId.Symphony_BrokenTempo_Debuff, skill, NegativeDuration, caster, multiplier);
						ForceRandomCooldown(member, TimeSpan.FromSeconds(15));
						break;
					case SymphonyEffect.CursedChorus:
						Start(member, BuffId.Symphony_CursedChorus_Debuff, skill, NegativeDuration, caster, multiplier);
						this.ScheduleCursedChorus(skill, member);
						break;
					case SymphonyEffect.DiscordantMelody:
						Start(member, BuffId.Symphony_DiscordantMelody_Debuff, skill, TimeSpan.FromSeconds(2), caster, multiplier);
						this.ApplyTemporaryState(member, skill, StateType.Stunned, TimeSpan.FromSeconds(2));
						break;
					case SymphonyEffect.DanceOfMadness:
						if (member.Handle == caster.Handle)
							break;

						Start(member, BuffId.Symphony_DanceOfMadness_Debuff, skill, TimeSpan.FromSeconds(4), caster, multiplier);
						member.StartBuff(BuffId.Blind, 1, 0, TimeSpan.FromSeconds(4), caster, skill.Id);
						break;
				}
			}
		}

		private void ApplyAllPositive(Skill skill, ICombatEntity caster, List<Character> party, float multiplier)
		{
			this.ApplyEffect(skill, caster, party, SymphonyEffect.MarchOfTriumph, multiplier);
			this.ApplyEffect(skill, caster, party, SymphonyEffect.BalladOfSanctuary, multiplier);
			this.ApplyEffect(skill, caster, party, SymphonyEffect.DanceOfSwiftness, multiplier);
			this.ApplyEffect(skill, caster, party, SymphonyEffect.GoldenResonance, multiplier);
			this.ApplyEffect(skill, caster, party, SymphonyEffect.HerosCrescendo, multiplier);
			this.ApplyEffect(skill, caster, party, SymphonyEffect.IronWaltz, multiplier);
			this.ApplyEffect(skill, caster, party, SymphonyEffect.EchoOfReversal, multiplier);
			this.ApplyEffect(skill, caster, party, SymphonyEffect.FestivalOverture, multiplier);
			this.ApplyEffect(skill, caster, party, SymphonyEffect.FinaleOfResurrection, multiplier);
		}

		private void ApplyLastWaltz(ICombatEntity caster, List<Character> party)
		{
			foreach (var member in party.Where(member => !member.IsDead))
				this.ApplyLastWaltzDrain(caster, member);
		}

		private void ApplyLastWaltzDrain(ICombatEntity caster, Character member)
		{
			var total = MathF.Min(member.Hp - 1, member.MaxHp * 0.96f);
			if (total <= 0)
				return;

			var removed = 0f;
			var tickDamage = member.MaxHp * 0.10f;
			var runner = caster.TryGetSkill(SkillId.PiedPiper_Improvisation, out var skill) ? skill : null;
			if (runner == null)
				return;

			for (var i = 1; i <= 11; ++i)
			{
				var delay = TimeSpan.FromMilliseconds(8000f / 11f * i);
				runner.Run(runner.Wait(delay).ContinueWith(_ =>
				{
					if (member.IsDead)
						return;

					var damage = MathF.Min(tickDamage, total - removed);
					removed += damage;
					if (damage > 0)
						member.TakeDamage(damage, caster);
				}));
			}
		}

		private void ScheduleCursedChorus(Skill skill, Character member)
		{
			skill.Run(skill.Wait(NegativeDuration).ContinueWith(_ =>
			{
				if (!member.IsDead)
					ApplyCursedChorus(member);
			}));
		}

		internal static void ForceRandomCooldown(Character member)
			=> ForceRandomCooldown(member, TimeSpan.FromSeconds(15));

		internal static void ForceRandomCooldown(Character member, TimeSpan duration)
		{
			var hotbarSkillIds = GetHotbarSkillIds(member).ToHashSet();
			var skills = member.Skills.GetList(skill =>
				skill.Level > 0 &&
				skill.Id != SkillId.PiedPiper_Improvisation &&
				skill.Data.CooldownTime > TimeSpan.Zero &&
				(hotbarSkillIds.Count == 0 || hotbarSkillIds.Contains(skill.Id)))
				.ToList();

			if (skills.Count == 0)
				return;

			var selected = skills[RandomProvider.Get().Next(skills.Count)];
			selected.StartCooldown(duration);
		}

		private static IEnumerable<SkillId> GetHotbarSkillIds(Character member)
		{
			var serialized = member.Variables.Perm.Get<string>("Melia.QuickSlotList", "");
			foreach (var entry in serialized.Split('#', StringSplitOptions.RemoveEmptyEntries).Take(50))
			{
				var split = entry.Split(',', StringSplitOptions.RemoveEmptyEntries);
				if (split.Length < 2 || !Enum.TryParse<QuickSlotType>(split[0], out var type) || type != QuickSlotType.Skill || !int.TryParse(split[1], out var skillId))
					continue;

				yield return (SkillId)skillId;
			}
		}

		private void ReduceCurrentCooldowns(Character member, float multiplier)
		{
			if (!member.Components.TryGet<CooldownComponent>(out var cooldowns))
				return;

			var changed = false;
			foreach (var cooldown in cooldowns.GetAll().Where(cd => cd.Remaining > TimeSpan.Zero))
			{
				cooldowns.ReduceCooldown(cooldown.Id, TimeSpan.FromMilliseconds(cooldown.Remaining.TotalMilliseconds * 0.2f * multiplier));
				changed = true;
			}

			if (changed)
				Send.ZC_COOLDOWN_LIST(member, cooldowns.GetAll());
		}

		private void ApplyBestFriendResult(Skill skill, ICombatEntity caster, bool positive)
		{
			if (caster is not Character character || !character.TryGetSkillLevel(SkillId.PiedPiper_HamelnNagetier, out var level) || level <= 0)
				return;

			if (positive)
				PiedPiperSkillHelper.AddBestFriendStack(character, skill);
			else
				PiedPiperSkillHelper.ClearBestFriendStacks(character);
		}

		private void ApplyDivineSymphonyVisual(List<Character> party)
		{
			foreach (var member in party)
			{
				member.AddEffect(DivineSymphonyEffectKey, new AttachEffect("F_explosion052_green", 1f, EffectLocation.Middle));
				member.AddEffect($"{DivineSymphonyEffectKey}.Fire", new AttachEffect("F_burstup005_fire", 1f, EffectLocation.Middle));
				member.AddEffect($"{DivineSymphonyEffectKey}.Poison", new AttachEffect("F_spread_out010_pink", 1f, EffectLocation.Middle));

				if (member.TryGetSkill(SkillId.PiedPiper_Improvisation, out var symphony))
				{
					symphony.Run(symphony.Wait(PositiveDuration).ContinueWith(_ =>
					{
						member.RemoveEffect(DivineSymphonyEffectKey);
						member.RemoveEffect($"{DivineSymphonyEffectKey}.Fire");
						member.RemoveEffect($"{DivineSymphonyEffectKey}.Poison");
					}));
				}
			}
		}

		private void ApplyTemporaryState(Character member, Skill skill, string stateType, TimeSpan duration)
		{
			member.AddState(stateType);
			skill.Run(skill.Wait(duration).ContinueWith(_ => member.RemoveState(stateType)));
		}

		internal static void ApplyCursedChorus(Character member)
		{
			if (RandomProvider.Get().Next(2) == 0)
			{
				var targetHp = MathF.Max(1, member.MaxHp * 0.0666f);
				if (member.Hp > targetHp)
					member.TakeDamage(member.Hp - targetHp, member);
			}
			else
			{
				var targetSp = MathF.Max(0, member.MaxSp * 0.0666f);
				member.ModifySp(targetSp - member.Sp);
			}
		}

		internal static void RemoveDebuffs(Character member, float multiplier)
		{
			var count = RandomProvider.Get().Next(1, 3);
			if (multiplier > 1f)
				count = Math.Max(count, 3);

			for (var i = 0; i < count; ++i)
				member.RemoveRandomDebuff();
		}

		private void PlayEffectCue(ICombatEntity caster, SymphonyEffect effect)
		{
			switch (effect)
			{
				case SymphonyEffect.MarchOfTriumph:
					this.PlayTimedEffect(caster, "E_archer_HamelnNagetier", 1f);
					this.PlayTimedEffect(caster, "F_warrior_PainBarrier_buff2_swordman01_3", 1f);
					PiedPiperSkillHelper.SafePlaySound(caster, "skl_eff_hamelnnagetier_melody");
					break;
				case SymphonyEffect.BalladOfSanctuary:
					this.PlayTimedEffect(caster, "F_pc_pose_music_wedding01", 1f);
					PiedPiperSkillHelper.SafePlaySound(caster, "skl_eff_hamelnnagetier_melody");
					break;
				case SymphonyEffect.DanceOfSwiftness:
					this.PlayTimedEffect(caster, "F_pc_pose_music_wedding02", 1f);
					PiedPiperSkillHelper.SafePlaySound(caster, "skl_eff_hamelnnagetier_melody");
					break;
				case SymphonyEffect.GoldenResonance:
					this.PlayTimedEffect(caster, "F_pc_pose_music_wedding02", 1f);
					PiedPiperSkillHelper.SafePlaySound(caster, "skl_eff_hamelnnagetier_melody");
					break;
				case SymphonyEffect.HerosCrescendo:
					this.PlayTimedEffect(caster, "F_pc_pose_music_wedding01", 1f);
					PiedPiperSkillHelper.SafePlaySound(caster, "skl_eff_hamelnnagetier_melody");
					break;
				case SymphonyEffect.IronWaltz:
					this.PlayTimedEffect(caster, "F_pc_pose_music_wedding02", 1f);
					PiedPiperSkillHelper.SafePlaySound(caster, "skl_eff_hamelnnagetier_melody");
					break;
				case SymphonyEffect.EchoOfReversal:
					this.PlayTimedEffect(caster, "F_pc_pose_music_wedding02", 1f);
					PiedPiperSkillHelper.SafePlaySound(caster, "skl_eff_hamelnnagetier_melody");
					break;
				case SymphonyEffect.FestivalOverture:
					this.PlayTimedEffect(caster, "F_pc_pose_music_wedding02", 1f);
					PiedPiperSkillHelper.SafePlaySound(caster, "skl_eff_hamelnnagetier_melody");
					break;
				case SymphonyEffect.FinaleOfResurrection:
					this.PlayTimedEffect(caster, "F_pc_pose_music_wedding01", 1f);
					PiedPiperSkillHelper.SafePlaySound(caster, "skl_eff_hamelnnagetier_melody");
					break;
				case SymphonyEffect.BrokenTempo:
					this.PlayTimedEffect(caster, "E_buff_Stun", 1f);
					PiedPiperSkillHelper.SafePlaySound(caster, "skl_eff_piedpiper_dissonanz_shot");
					break;
				case SymphonyEffect.CursedChorus:
					this.PlayTimedEffect(caster, "F_pc_pose_music_wedding01", 1f);
					PiedPiperSkillHelper.SafePlaySound(caster, "skl_eff_piedpiper_dissonanz_shot");
					break;
				case SymphonyEffect.DiscordantMelody:
					this.PlayTimedEffect(caster, "E_buff_Stun", 1f);
					PiedPiperSkillHelper.SafePlaySound(caster, "skl_eff_piedpiper_dissonanz_shot");
					break;
				case SymphonyEffect.DanceOfMadness:
					this.PlayTimedEffect(caster, "[mon]Confuse Debuff", 1f);
					PiedPiperSkillHelper.SafePlaySound(caster, "skl_eff_piedpiper_dissonanz_shot");
					break;
				case SymphonyEffect.DivineSymphony:
					this.PlayPacketEffect(caster, 5);
					PiedPiperSkillHelper.SafePlaySound(caster, "skl_eff_hamelnnagetier_melody");
					break;
				case SymphonyEffect.LastWaltz:
					this.PlayRawPacketEffect(caster, 159);
					PiedPiperSkillHelper.SafePlaySound(caster, "skl_eff_piedpiper_dissonanz_shot");
					break;
			}
		}

		private void PlayPacketEffect(ICombatEntity caster, int id)
		{
			if (ZoneServer.Instance.Data.PacketStringDb.TryFind(id, out var effectString))
				this.PlayTimedEffect(caster, effectString.Name, 1f);
		}

		private void PlayRawPacketEffect(ICombatEntity caster, int id)
		{
			if (ZoneServer.Instance.Data.PacketStringDb.TryFind(id, out var effectString))
				PiedPiperSkillHelper.SafePlayEffect(caster, effectString.Name, 1f);
		}

		private void PlayTimedEffect(ICombatEntity caster, string effectName, float scale)
		{
			var timedName = effectName.Contains("##", StringComparison.Ordinal)
				? effectName
				: $"{effectName}##20.0";

			PiedPiperSkillHelper.SafePlayEffect(caster, timedName, scale);
		}

		private static Buff Start(Character target, BuffId buffId, Skill skill, TimeSpan duration, ICombatEntity caster, float multiplier)
		{
			var buff = target.StartBuff(buffId, MathF.Min(skill.Level, 3), multiplier, duration, caster, skill.Id);
			if (buff != null)
			{
				buff.NotifyUpdate();
				Send.ZC_BUFF_LIST(target);
			}

			return buff;
		}

		private static bool IsPositive(SymphonyEffect effect)
			=> effect is SymphonyEffect.MarchOfTriumph
				or SymphonyEffect.BalladOfSanctuary
				or SymphonyEffect.DanceOfSwiftness
				or SymphonyEffect.GoldenResonance
				or SymphonyEffect.HerosCrescendo
				or SymphonyEffect.IronWaltz
				or SymphonyEffect.EchoOfReversal
				or SymphonyEffect.FestivalOverture
				or SymphonyEffect.FinaleOfResurrection;

		private enum SymphonyEffect
		{
			MarchOfTriumph,
			BalladOfSanctuary,
			DanceOfSwiftness,
			GoldenResonance,
			HerosCrescendo,
			IronWaltz,
			EchoOfReversal,
			FestivalOverture,
			FinaleOfResurrection,
			BrokenTempo,
			CursedChorus,
			DiscordantMelody,
			DanceOfMadness,
			DivineSymphony,
			LastWaltz,
		}
	}
}

namespace Melia.Zone.Buffs.Handlers.Archers.PiedPiper
{
	[Package("laima")]
	[BuffHandler(BuffId.Symphony_MarchOfTriumph_Buff)]
	public class SymphonyMarchOfTriumphBuffHandler : BuffHandler, IBuffBeforeKnockbackHandler, IBuffBeforeKnockdownHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			var multiplier = MathF.Max(1f, buff.NumArg2);
			AddPropertyModifier(buff, buff.Target, PropertyName.PATK_RATE_BM, 0.25f * multiplier);
			AddPropertyModifier(buff, buff.Target, PropertyName.MATK_RATE_BM, 0.25f * multiplier);
			AddPropertyModifier(buff, buff.Target, PropertyName.MSPD_BM, 8f * multiplier);
			SymphonyBuffUi.UpdateProperties(buff.Target, PropertyName.MINPATK, PropertyName.MAXPATK, PropertyName.MINMATK, PropertyName.MAXMATK, PropertyName.MSPD);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.PATK_RATE_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.MATK_RATE_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.MSPD_BM);
			SymphonyBuffUi.UpdateProperties(buff.Target, PropertyName.MINPATK, PropertyName.MAXPATK, PropertyName.MINMATK, PropertyName.MAXMATK, PropertyName.MSPD);
		}

		public KnockResult OnBeforeKnockback(Buff buff, ICombatEntity attacker, ICombatEntity target) => KnockResult.Prevent;
		public KnockResult OnBeforeKnockdown(Buff buff, ICombatEntity attacker, ICombatEntity target) => KnockResult.Prevent;
	}

	[Package("laima")]
	[BuffHandler(BuffId.Symphony_BalladOfSanctuary_Buff)]
	public class SymphonyBalladOfSanctuaryBuffHandler : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			var multiplier = MathF.Max(1f, buff.NumArg2);
			AddPropertyModifier(buff, buff.Target, PropertyName.DEF_RATE_BM, 0.20f * multiplier);
			AddPropertyModifier(buff, buff.Target, PropertyName.MDEF_RATE_BM, 0.20f * multiplier);
			SymphonyBuffUi.UpdateProperties(buff.Target, PropertyName.DEF, PropertyName.MDEF);
		}

		public override void WhileActive(Buff buff)
		{
			if (buff.Target is not Character character)
				return;

			var multiplier = MathF.Max(1f, buff.NumArg2);
			character.Heal(character.MaxHp * 0.03f * multiplier, character.MaxSp * 0.03f * multiplier);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.DEF_RATE_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.MDEF_RATE_BM);
			SymphonyBuffUi.UpdateProperties(buff.Target, PropertyName.DEF, PropertyName.MDEF);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Symphony_DanceOfSwiftness_Buff)]
	public class SymphonyDanceOfSwiftnessBuffHandler : BuffHandler
	{
		private const string CooldownReductionVar = "Melia.Skill.CooldownReduction";

		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			var multiplier = MathF.Max(1f, buff.NumArg2);
			buff.Target.SetTempVar(CooldownReductionVar, MathF.Max(buff.Target.GetTempVar(CooldownReductionVar), 0.20f * multiplier));
			AddPropertyModifier(buff, buff.Target, PropertyName.SR_BM, 2f * multiplier);
			SymphonyBuffUi.UpdateProperties(buff.Target, PropertyName.SR);
		}

		public override void OnEnd(Buff buff)
		{
			buff.Target.RemoveTempVar(CooldownReductionVar);
			RemovePropertyModifier(buff, buff.Target, PropertyName.SR_BM);
			SymphonyBuffUi.UpdateProperties(buff.Target, PropertyName.SR);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Symphony_GoldenResonance_Buff)]
	public class SymphonyGoldenResonanceBuffHandler : BuffHandler
	{
		public override void OnEnd(Buff buff)
		{
			if (buff.Target is Character character)
				Melia.Zone.Skills.Handlers.Archers.PiedPiper.PiedPiperSymphonyOfFate.RemoveDebuffs(character, MathF.Max(1f, buff.NumArg2));
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Symphony_HerosCrescendo_Buff)]
	public class SymphonyHerosCrescendoBuffHandler : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			var multiplier = MathF.Max(1f, buff.NumArg2);
			AddPropertyModifier(buff, buff.Target, PropertyName.CRTHR_RATE_BM, 0.10f * multiplier);

			var critDamage = buff.Target.Properties.GetFloat(PropertyName.CRTATK) * 0.15f * multiplier;
			AddPropertyModifier(buff, buff.Target, PropertyName.CRTATK_BM, critDamage);
			SymphonyBuffUi.UpdateProperties(buff.Target, PropertyName.CRTHR, PropertyName.CRTATK);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.CRTHR_RATE_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.CRTATK_BM);
			SymphonyBuffUi.UpdateProperties(buff.Target, PropertyName.CRTHR, PropertyName.CRTATK);
		}

		[CombatCalcModifier(CombatCalcPhase.BeforeCalc, BuffId.Symphony_HerosCrescendo_Buff)]
		public void OnAttackBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult result)
		{
			if (attacker.TryGetBuff(BuffId.Symphony_HerosCrescendo_Buff, out var buff))
			{
				var multiplier = MathF.Max(1f, buff.NumArg2);
				modifier.MinCritChance = MathF.Max(modifier.MinCritChance, 10f * multiplier);
				modifier.CritDamageMultiplier += 0.15f * multiplier;
			}
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Symphony_IronWaltz_Buff)]
	public class SymphonyIronWaltzBuffHandler : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			var multiplier = MathF.Max(1f, buff.NumArg2);
			AddPropertyModifier(buff, buff.Target, PropertyName.BLK_RATE_BM, 0.25f * multiplier);
			AddPropertyModifier(buff, buff.Target, PropertyName.DR_RATE_BM, 0.25f * multiplier);
			SymphonyBuffUi.UpdateProperties(buff.Target, PropertyName.BLK, PropertyName.DR);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.BLK_RATE_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.DR_RATE_BM);
			SymphonyBuffUi.UpdateProperties(buff.Target, PropertyName.BLK, PropertyName.DR);
		}

		[CombatCalcModifier(CombatCalcPhase.BeforeCalc, BuffId.Symphony_IronWaltz_Buff)]
		public void OnDefenseBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult result)
		{
			if (target.TryGetBuff(BuffId.Symphony_IronWaltz_Buff, out var buff))
				modifier.DamageMultiplier *= 1f - 0.15f * MathF.Max(1f, buff.NumArg2);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Symphony_EchoOfReversal_Buff)]
	public class SymphonyEchoOfReversalBuffHandler : BuffHandler, IBuffCombatDefenseAfterCalcHandler
	{
		public void OnDefenseAfterCalc(Buff buff, ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (attacker == null || attacker == target || skillHitResult.Damage <= 0 || RandomProvider.Get().Next(100) >= 20)
				return;

			var multiplier = MathF.Max(1f, buff.NumArg2);
			attacker.TakeDamage(skillHitResult.Damage * 0.30f * multiplier, target);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Symphony_FestivalOverture_Buff)]
	public class SymphonyFestivalOvertureBuffHandler : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			AddPropertyModifier(buff, buff.Target, PropertyName.HR_RATE_BM, 0.60f * MathF.Max(1f, buff.NumArg2));
			SymphonyBuffUi.UpdateProperties(buff.Target, PropertyName.HR);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.HR_RATE_BM);
			SymphonyBuffUi.UpdateProperties(buff.Target, PropertyName.HR);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Symphony_FinaleOfResurrection_Buff)]
	public class SymphonyFinaleOfResurrectionBuffHandler : BuffHandler
	{
	}

	[Package("laima")]
	[BuffHandler(BuffId.Symphony_DiscordantMelody_Debuff)]
	public class SymphonyDiscordantMelodyDebuffHandler : BuffHandler
	{
	}

	[Package("laima")]
	[BuffHandler(BuffId.Symphony_BrokenTempo_Debuff)]
	public class SymphonyBrokenTempoDebuffHandler : BuffHandler
	{
	}

	[Package("laima")]
	[BuffHandler(BuffId.Symphony_CursedChorus_Debuff)]
	public class SymphonyCursedChorusDebuffHandler : BuffHandler
	{
	}

	[Package("laima")]
	[BuffHandler(BuffId.Symphony_DanceOfMadness_Debuff)]
	public class SymphonyDanceOfMadnessDebuffHandler : BuffHandler
	{
	}

	internal static class SymphonyBuffUi
	{
		public static void UpdateProperties(ICombatEntity target, params string[] propertyNames)
		{
			if (target is Character character)
				character.InvalidateProperties(propertyNames);
			else
				target.InvalidateProperties();
		}
	}
}
