using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Melia.Shared.Game.Const;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Scripting.AI;
using Melia.Zone.Skills.Helpers;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.CombatEntities.Components;
using Melia.Zone.World.Actors.Monsters;
using Yggdrasil.Util;
using static Melia.Zone.Skills.Helpers.SkillDamageHelper;

namespace Melia.Zone.Skills.Handlers.Wizards.Necromancer
{
	public enum NecromancerSkeletonKind
	{
		Soldier,
		Archer,
		Mage,
		EliteSoldier,
	}

	public static class NecromancerSkillHelper
	{
		public const int EliteSkeletonSoldierId = 300022;
		public const int ShoggothId = 103018;
		public const int FleshAmalgamId = 103017;
		public const int CreateShoggothCorpsePartsCost = 100;
		public const int UntilDeathCorpsePartsCost = 200;
		public const int FleshAmalgamCorpsePartsCost = 200;

		private const string NecromancerSummonKey = "Melia.Necromancer.Summon";
		private const string SkeletonKey = "Melia.Necromancer.Skeleton";
		private const string SkeletonKindKey = "Melia.Necromancer.SkeletonKind";
		private const string ResourceTypeKey = "Melia.Necromancer.ResourceType";
		private const string ResourceAmountKey = "Melia.Necromancer.ResourceAmount";
		private const string ResourceRestoredKey = "Melia.Necromancer.ResourceRestored";
		private const string UntilDeathBonusKey = "Melia.Necromancer.UntilDeathBonus";
		private const string UntilDeathBuffTokenKey = "Melia.Necromancer.UntilDeathBuffToken";
		private const string SupportDamageBonusKey = "Melia.Necromancer.SupportDamageBonus";
		private const string SupportDamageTokenKey = "Melia.Necromancer.SupportDamageToken";
		private const string OrderCancellationKey = "Melia.Necromancer.OrderCancellation";
		private const string OrderScriptKey = "Melia.Necromancer.OrderScript";
		private const string ManualOrderOnlyKey = "Melia.Necromancer.ManualOrderOnly";
		private const int DefaultMaxCorpseParts = 500;
		private static readonly TimeSpan UntilDeathDuration = TimeSpan.FromSeconds(15);
		private const float SummonControlStatCap = 5000f;
		private const float SummonControlExponent = 0.85f;

		private static readonly HashSet<int> SkeletonIds = new()
		{
			MonsterId.SkeletonSoldier,
			MonsterId.SkeletonArcher,
			MonsterId.SkeletonMage,
			EliteSkeletonSoldierId,
		};

		private static readonly JobId[] SummonControlJobIds =
		[
			JobId.Bokor,
			JobId.Sorcerer,
			JobId.Necromancer,
			// Future summon classes can be added here.
		];

		public static List<Summon> GetNecromancerSummons(Character character)
			=> character.Summons.GetSummons(s => s.Vars.GetBool(NecromancerSummonKey, false) || SkeletonIds.Contains(s.Id) || s.Id == ShoggothId);

		public static List<Summon> GetSkeletons(Character character)
			=> character.Summons.GetSummons(IsSkeleton);

		public static int CountSkeletons(Character character)
			=> GetSkeletons(character).Count(s => !s.IsDead);

		public static int CountSkeletons(Character character, NecromancerSkeletonKind kind)
			=> GetSkeletons(character).Count(s => !s.IsDead && GetSkeletonKind(s) == kind);

		public static bool IsSkeleton(Summon summon)
			=> summon.Vars.GetBool(SkeletonKey, false) || SkeletonIds.Contains(summon.Id);

		public static NecromancerSkeletonKind GetSkeletonKind(Summon summon)
		{
			if (summon.Vars.TryGetString(SkeletonKindKey, out var kindName) && Enum.TryParse<NecromancerSkeletonKind>(kindName, out var kind))
				return kind;
			if (summon.Id == MonsterId.SkeletonArcher)
				return NecromancerSkeletonKind.Archer;
			if (summon.Id == MonsterId.SkeletonMage)
				return NecromancerSkeletonKind.Mage;
			if (summon.Id == EliteSkeletonSoldierId)
				return NecromancerSkeletonKind.EliteSoldier;
			return NecromancerSkeletonKind.Soldier;
		}

		public static bool IsNecromancerSummon(Summon summon)
			=> summon.Vars.GetBool(NecromancerSummonKey, false) || IsSkeleton(summon) || summon.Id == ShoggothId;

		public static int GetCorpseParts(Character owner)
			=> Math.Max(0, Math.Max((int)owner.Etc.Properties.GetFloat(PropertyName.Necro_DeadPartsCnt), CountCorpsePartSlots(owner)));

		public static void SyncCorpseParts(Character owner)
		{
			if (owner == null)
				return;

			var counter = Math.Max(0, (int)owner.Etc.Properties.GetFloat(PropertyName.Necro_DeadPartsCnt));
			var slots = CountCorpsePartSlots(owner);
			var synced = Math.Max(counter, slots);

			if (counter != synced)
				owner.Etc.Properties.SetFloat(PropertyName.Necro_DeadPartsCnt, synced);

			Send.ZC_OBJECT_PROPERTY(owner, owner.Etc, PropertyName.Necro_DeadPartsCnt);
			owner.AddonMessage(AddonMessage.UPDATE_NECRONOMICON_UI);
		}

		public static int GetMaxCorpseParts(Character owner)
			=> DefaultMaxCorpseParts;

		public static bool HasCorpseParts(Character owner, int amount)
			=> owner != null && (amount <= 0 || GetCorpseParts(owner) >= amount);

		public static bool TrySpendCorpseParts(Character owner, int amount)
		{
			if (owner == null || amount <= 0)
				return false;

			if (GetCorpseParts(owner) < amount)
				return false;

			owner.ModifyEtcProperty(PropertyName.Necro_DeadPartsCnt, -amount);
			ClearCorpsePartSlots(owner, amount);
			owner.AddonMessage(AddonMessage.UPDATE_NECRONOMICON_UI);
			return true;
		}

		public static bool AddCorpseParts(Character owner, Mob corpseSource, int amount = 1)
		{
			if (owner == null || amount <= 0)
				return false;

			var current = GetCorpseParts(owner);
			var max = GetMaxCorpseParts(owner);
			var added = Math.Min(amount, Math.Max(0, max - current));
			if (added <= 0)
				return false;

			for (var i = 0; i < added; i++)
				SetFirstEmptyCorpsePartSlot(owner, corpseSource);

			owner.ModifyEtcProperty(PropertyName.Necro_DeadPartsCnt, added);
			owner.AddonMessage(AddonMessage.UPDATE_NECRONOMICON_UI);
			return true;
		}

		public static bool HasSummonControlJob(Character owner)
			=> owner?.Jobs != null && SummonControlJobIds.Any(jobId => owner.Jobs.Has(jobId));

		public static void ApplySummonAttackStats(Summon summon, float attack)
		{
			attack = Math.Max(1, attack);
			summon.Properties.SetFloat(PropertyName.FixedAttack, attack);
			summon.Properties.SetFloat(PropertyName.ATK_BM, attack);
			summon.Properties.SetFloat(PropertyName.PATK_BM, attack);
			summon.Properties.SetFloat(PropertyName.MATK_BM, attack);
			summon.Properties.SetFloat(PropertyName.MINMATK_BM, attack);
			summon.Properties.SetFloat(PropertyName.MAXMATK_BM, attack);
			summon.Properties.SetFloat(PropertyName.Stat_ATK_BM, attack);
		}

		public static float GetOwnerMagicAttack(Character owner)
		{
			if (owner == null)
				return 1f;

			var minMagic = owner.Properties.GetFloat(PropertyName.MINMATK);
			var maxMagic = owner.Properties.GetFloat(PropertyName.MAXMATK);
			var magicAttack = (minMagic + maxMagic) * 0.5f;
			if (magicAttack <= 0)
				magicAttack = owner.Properties.GetFloat(PropertyName.MATK);
			if (magicAttack <= 0)
				magicAttack = owner.Properties.GetFloat(PropertyName.ATK);

			return Math.Max(1, magicAttack);
		}

		public static Summon SpawnSkeleton(Character owner, Skill skill, NecromancerSkeletonKind kind, Position targetPos)
		{
			var monsterId = kind switch
			{
				NecromancerSkeletonKind.Archer => MonsterId.SkeletonArcher,
				NecromancerSkeletonKind.Mage => MonsterId.SkeletonMage,
				NecromancerSkeletonKind.EliteSoldier => EliteSkeletonSoldierId,
				_ => MonsterId.SkeletonSoldier,
			};

			var hpCostRate = kind == NecromancerSkeletonKind.Mage ? 0f : 0.20f;
			var spCostRate = kind == NecromancerSkeletonKind.Mage ? 0.20f : 0f;
			var resourceAmount = kind == NecromancerSkeletonKind.Mage
				? LockOwnerResource(owner, "SP", spCostRate)
				: LockOwnerResource(owner, "HP", hpCostRate);

			var className = kind switch
			{
				NecromancerSkeletonKind.Archer => "pcskill_skullarcher",
				NecromancerSkeletonKind.Mage => "pcskill_skullwizard",
				NecromancerSkeletonKind.EliteSoldier => "pcskill_skullelitesoldier",
				_ => "pcskill_skullsoldier",
			};
			if (MonsterSkillCreateMob(skill, owner, className, targetPos, 0f, "", "PC_Summon", 0, 1800f, "None", "$NECRO_MON#1") is not Summon summon)
				return null;

			summon.Name = "!@#${Auto_1}_of_{Auto_2}$*$Auto_1$*$" + owner.Name + "$*$Auto_2$*$@dicID_^*$ETC_20150317_000235$*^#@!";
			summon.OwnerHandle = owner.Handle;
			summon.AssociatedHandle = owner.Handle;
			summon.Faction = owner.Faction;
			summon.Tendency = TendencyType.Peaceful;
			summon.FromGround = true;
			summon.Layer = owner.Layer;
			summon.Position = owner.Map.Ground.GetLastValidPosition(owner.Position, targetPos);
			summon.Direction = owner.Direction;
			summon.Properties.SetFloat(PropertyName.Level, owner.Level);
			summon.Properties.SetFloat(PropertyName.Lv, owner.Level);

			var fixedLife = resourceAmount * (1f + (0.05f * Math.Max(1, skill.Level)));
			var fixedAttack = CalculateSkeletonAttack(owner, skill, kind);
			var fixedDefense = (owner.Properties.GetFloat(PropertyName.DEF) + owner.Properties.GetFloat(PropertyName.MDEF)) * 0.5f * (1.2f + (0.1f * skill.Level));

			summon.Properties.SetFloat(PropertyName.FixedLife, Math.Max(1, fixedLife));
			ApplySummonAttackStats(summon, fixedAttack);
			summon.Properties.SetFloat(PropertyName.FixedDefence, Math.Max(1, fixedDefense));
			summon.Properties.SetFloat(PropertyName.FIXMSPD_BM, Math.Max(1f, owner.Properties.GetFloat(PropertyName.MSPD)));
			summon.Properties.SetFloat(PropertyName.HR_BM, owner.Properties.GetFloat(PropertyName.HR) * GetSkeletonAccuracyMultiplier(kind));
			summon.Properties.SetFloat(PropertyName.DR_BM, owner.Properties.GetFloat(PropertyName.DR));
			summon.Properties.SetFloat(PropertyName.CRTHR_BM, owner.Properties.GetFloat(PropertyName.CRTHR));
			summon.Properties.SetFloat(PropertyName.CRTATK_BM, owner.Properties.GetFloat(PropertyName.CRTATK) * (kind == NecromancerSkeletonKind.Soldier || kind == NecromancerSkeletonKind.EliteSoldier ? 1.4f : 1f));
			if (kind == NecromancerSkeletonKind.Archer)
			{
				summon.Properties.Modify(PropertyName.CRTHR_BM, owner.Properties.GetFloat(PropertyName.CRTHR) * 0.3f);
			}
			if (kind == NecromancerSkeletonKind.EliteSoldier)
				summon.Properties.Modify(PropertyName.CRTHR_BM, 25f);

			summon.Properties.InvalidateAll();
			summon.Properties.SetFloat(PropertyName.HP, summon.Properties.GetFloat(PropertyName.MHP));
			summon.Properties.SetFloat(PropertyName.SP, summon.Properties.GetFloat(PropertyName.MSP));
			summon.Components.Add(new LifeTimeComponent(summon, TimeSpan.FromMinutes(kind == NecromancerSkeletonKind.Mage && owner.IsAbilityActive(AbilityId.Necromancer26) ? 30 : 30)));
			summon.SetState(true);
			if (kind == NecromancerSkeletonKind.Mage && owner.IsAbilityActive(AbilityId.Necromancer26))
				summon.ChangeScale(2f, 0f);

			MarkSkeleton(owner, summon, kind, resourceAmount, kind == NecromancerSkeletonKind.Mage ? "SP" : "HP");
			summon.StartBuff(BuffId.Ability_buff_PC_Summon, TimeSpan.Zero, summon);
			ApplySkeletonCountIndicator(owner);

			if (kind == NecromancerSkeletonKind.Mage && owner.IsAbilityActive(AbilityId.Necromancer26))
				StartClericHealingLoop(skill, owner, summon);

			return summon;
		}

		public static void MarkNecromancerSummon(Summon summon)
		{
			summon.Vars.SetBool(NecromancerSummonKey, true);
			summon.Vars.SetInt("$NECRO_MON", 1);
		}

		public static void MarkSkeleton(Character owner, Summon summon, NecromancerSkeletonKind kind, float resourceAmount, string resourceType)
		{
			MarkNecromancerSummon(summon);
			summon.Vars.SetBool(SkeletonKey, true);
			summon.Vars.SetString(SkeletonKindKey, kind.ToString());
			summon.Vars.SetString(ResourceTypeKey, resourceType);
			summon.Vars.SetFloat(ResourceAmountKey, resourceAmount);
			summon.Vars.SetBool(ResourceRestoredKey, false);

			Action<Mob, ICombatEntity> onDied = null;
			onDied = (mob, killer) =>
			{
				if (mob is Summon deadSummon)
				{
					deadSummon.Died -= onDied;
					RestoreSummonResource(owner, deadSummon);
					ApplySkeletonCountIndicator(owner);
				}
			};
			summon.Died += onDied;
		}

		private static float GetSkeletonAccuracyMultiplier(NecromancerSkeletonKind kind)
			=> kind switch
			{
				NecromancerSkeletonKind.Archer => 2f,
				NecromancerSkeletonKind.Mage => 4f,
				_ => 1f,
			};

		public static void ReleaseNecromancerSummons(Character owner)
		{
			foreach (var summon in GetNecromancerSummons(owner).ToList())
			{
				CancelCurrentOrder(summon);
				RestoreSummonResource(owner, summon);
				if (!summon.IsDead)
					summon.TakeDamage(Math.Max(1, summon.Hp), owner);
			}

			ApplySkeletonCountIndicator(owner);
		}

		public static int SacrificeRandomSkeleton(Character owner, int count)
		{
			var skeletons = GetSkeletons(owner).Where(s => !s.IsDead).ToList();
			var sacrificed = 0;
			for (var i = 0; i < count && skeletons.Count != 0; i++)
			{
				var skeleton = skeletons[RandomProvider.Get().Next(skeletons.Count)];
				skeletons.Remove(skeleton);
				RestoreSummonResource(owner, skeleton);
				skeleton.TakeDamage(Math.Max(1, skeleton.Hp), owner);
				sacrificed++;
			}

			ApplySkeletonCountIndicator(owner);
			return sacrificed;
		}

		public static void OrderAttack(Skill skill, Character owner, ICombatEntity target)
		{
			if (target == null)
				return;

			foreach (var summon in GetNecromancerSummons(owner).Where(s => !s.IsDead))
			{
				CancelCurrentOrder(summon);
				summon.Tendency = TendencyType.Peaceful;
				summon.Vars.SetBool(ManualOrderOnlyKey, true);
				summon.Components.Get<AiComponent>()?.Script.QueueEventAlert(new HateResetAlert());
				if (summon.Vars.GetBool("Melia.AI.Stationary", false) && !summon.Position.InRange2D(target.Position, 320f + summon.AgentRadius))
					continue;
				summon.InsertHate(target, 5000);
			}
		}

		public static void OrderCancelAttack(Character owner)
		{
			foreach (var summon in GetNecromancerSummons(owner).Where(s => !s.IsDead))
			{
				CancelCurrentOrder(summon);
				summon.Tendency = TendencyType.Peaceful;
				summon.Vars.SetBool(ManualOrderOnlyKey, false);
				summon.Components.Get<AiComponent>()?.Script.QueueEventAlert(new HateResetAlert());
				summon.StopMove();
			}
		}

		public static void OrderCancelAttackAllSummons(Character owner)
		{
			foreach (var summon in owner.Summons.GetSummons(s => !s.IsDead))
			{
				CancelCurrentOrder(summon);
				summon.Tendency = TendencyType.Peaceful;
				summon.Vars.SetBool(ManualOrderOnlyKey, false);
				summon.Components.Get<AiComponent>()?.Script.QueueEventAlert(new HateResetAlert());
				summon.StopMove();
			}
		}

		public static void StartRangedSpacingLoop(Skill skill, Summon summon, float preferredRange)
		{
			var cts = new CancellationTokenSource();
			summon.Vars.Set(OrderCancellationKey, cts);
			summon.Vars.SetString(OrderScriptKey, "RANGED_SPACING");
			_ = MaintainRangedSpacing(skill, summon, preferredRange, cts.Token);
		}

		public static float GetUntilDeathBonus(Summon summon)
		{
			if (summon == null || !IsNecromancerSummon(summon))
				return 0f;

			var summonBonus = summon.Vars.GetFloat(UntilDeathBonusKey, 0f);
			if (summonBonus > 0)
				return summonBonus;

			if (summon.Owner is Character owner && owner.Variables.Temp.TryGetFloat(UntilDeathBonusKey, out var ownerBonus))
				return ownerBonus;

			return 0f;
		}

		public static float GetSupportDamageBonus(Summon summon)
			=> summon.Vars.GetFloat(SupportDamageBonusKey, 0f);

		public static void SetUntilDeathBonus(Summon summon, float bonus)
		{
			summon.Vars.SetFloat(UntilDeathBonusKey, bonus);
		}

		public static bool ApplyUntilDeath(Character owner, Skill skill)
		{
			SyncCorpseParts(owner);

			var corpseParts = GetCorpseParts(owner);
			if (!TrySpendCorpseParts(owner, corpseParts))
				return false;

			var bonus = Math.Max(1, skill.Level) * 0.01f + corpseParts * 0.001f;
			var token = Guid.NewGuid().ToString("N");

			owner.Variables.Temp.SetFloat(UntilDeathBonusKey, bonus);
			owner.Variables.Temp.SetString(UntilDeathBuffTokenKey, token);
			owner.StartBuff(BuffId.UntilDeath_Buff, skill.Level, bonus, UntilDeathDuration, owner, skill.Id);

			foreach (var summon in GetNecromancerSummons(owner).Where(s => !s.IsDead))
				SetUntilDeathBonus(summon, bonus);

			_ = ClearUntilDeathBonusWhenBuffEnds(skill, owner, token);
			return true;
		}

		public static void GrantCorpsePartFromSkeletonKill(Summon summon, Mob defeated)
		{
			if (summon == null || defeated == null || !IsSkeleton(summon))
				return;
			if (summon.Owner is not Character owner || !owner.IsAbilityActive(AbilityId.Necromancer17))
				return;

			AddCorpseParts(owner, defeated, 1);
		}

		public static void ApplySupportDamageBonus(Skill skill, Character owner, float bonus, TimeSpan duration)
		{
			var token = Guid.NewGuid().ToString("N");
			foreach (var summon in GetNecromancerSummons(owner).Where(s => !s.IsDead))
			{
				summon.Vars.SetFloat(SupportDamageBonusKey, bonus);
				summon.Vars.SetString(SupportDamageTokenKey, token);
				summon.PlayEffect("F_light034_green", 0.9f);
			}

			_ = ClearSupportDamageBonusWhenExpired(skill, owner, token, duration);
		}

		private static float LockOwnerResource(Character owner, string type, float rate)
		{
			var property = type == "SP" ? PropertyName.MSP : PropertyName.MHP;
			var modifier = type == "SP" ? PropertyName.MSP_BM : PropertyName.MHP_BM;
			var amount = MathF.Floor(owner.Properties.GetFloat(property) * rate);
			owner.Properties.Modify(modifier, -amount);
			owner.Properties.Invalidate(type == "SP" ? PropertyName.MSP : PropertyName.MHP);

			if (type == "SP" && owner.Sp > owner.MaxSp)
				owner.Properties.SetFloat(PropertyName.SP, owner.MaxSp);
			if (type == "HP" && owner.Hp > owner.MaxHp)
				owner.Properties.SetFloat(PropertyName.HP, owner.MaxHp);

			Send.ZC_UPDATE_ALL_STATUS(owner, 0);
			return amount;
		}

		public static void RestoreSummonResource(Character owner, Summon summon)
		{
			if (summon.Vars.GetBool(ResourceRestoredKey, false))
				return;

			if (!summon.Vars.TryGetFloat(ResourceAmountKey, out var amount) || amount <= 0)
				return;

			var type = summon.Vars.GetString(ResourceTypeKey, "HP");
			var modifier = type == "SP" ? PropertyName.MSP_BM : PropertyName.MHP_BM;
			owner.Properties.Modify(modifier, amount);
			owner.Properties.Invalidate(type == "SP" ? PropertyName.MSP : PropertyName.MHP);
			summon.Vars.SetBool(ResourceRestoredKey, true);

			Send.ZC_UPDATE_ALL_STATUS(owner, 0);
		}

		private static float CalculateSkeletonAttack(Character owner, Skill skill, NecromancerSkeletonKind kind)
		{
			var magicAttack = GetOwnerMagicAttack(owner);
			var skillLevel = Math.Max(1, skill.Level);
			var levelMultiplier = 0.40f + (skillLevel * 0.05f);
			var kindMultiplier = kind switch
			{
				NecromancerSkeletonKind.Archer => 0.8f,
				NecromancerSkeletonKind.EliteSoldier => 1.4f,
				_ => 1f,
			};

			return magicAttack * levelMultiplier * kindMultiplier;
		}

		public static float GetSummonControlMultiplier(Character owner)
		{
			return 1f + (GetSummonControlPercent(owner) / 100f);
		}

		public static float GetSummonControlPercent(Character owner)
		{
			if (owner == null)
				return 0f;

			var intContribution = GetSummonControlStatContribution(owner.Properties.GetFloat(PropertyName.INT));
			var conContribution = GetSummonControlStatContribution(owner.Properties.GetFloat(PropertyName.CON));
			var sprContribution = GetSummonControlStatContribution(owner.Properties.GetFloat(PropertyName.MNA));
			return Math.Clamp((intContribution + conContribution + sprContribution) / 3f * 100f, 0f, 100f);
		}

		private static float GetSummonControlStatContribution(float stat)
		{
			var normalized = Math.Clamp(stat / SummonControlStatCap, 0f, 1f);
			return MathF.Pow(normalized, SummonControlExponent);
		}

		private static void ApplySkeletonCountIndicator(Character owner)
		{
			var count = CountSkeletons(owner);
			if (count <= 0)
			{
				owner.RemoveBuff(BuffId.Disinter_PC_Buff);
				return;
			}

			var buff = owner.StartBuff(BuffId.Disinter_PC_Buff, count, 0, TimeSpan.Zero, owner, SkillId.Necromancer_RaiseDead);
			if (buff != null)
			{
				buff.OverbuffCounter = count;
				buff.NotifyUpdate();
			}
		}

		private static void SetFirstEmptyCorpsePartSlot(Character owner, Mob corpseSource)
		{
			var max = GetMaxCorpseParts(owner);
			for (var i = 1; i <= max; i++)
			{
				var key = "NecroDParts_" + i;
				if (owner.Etc.Properties.Has(key) && owner.Etc.Properties.GetFloat(key) > 0)
					continue;

				owner.Etc.Properties.SetFloat(key, corpseSource?.Id ?? 0);
				return;
			}
		}

		private static int CountCorpsePartSlots(Character owner)
		{
			if (owner == null)
				return 0;

			var count = 0;
			var max = GetMaxCorpseParts(owner);
			for (var i = 1; i <= max; i++)
			{
				var key = "NecroDParts_" + i;
				if (owner.Etc.Properties.Has(key) && owner.Etc.Properties.GetFloat(key) > 0)
					count++;
			}

			return count;
		}

		private static void ClearCorpsePartSlots(Character owner, int amount)
		{
			var remaining = amount;
			var max = GetMaxCorpseParts(owner);
			for (var i = max; i >= 1 && remaining > 0; i--)
			{
				var key = "NecroDParts_" + i;
				if (!owner.Etc.Properties.Has(key) || owner.Etc.Properties.GetFloat(key) <= 0)
					continue;

				owner.Etc.Properties.SetFloat(key, 0);
				remaining--;
			}
		}

		private static void ClearUntilDeathBonus(Character owner)
		{
			owner.Variables.Temp.Remove(UntilDeathBonusKey);
			foreach (var summon in GetNecromancerSummons(owner).Where(s => !s.IsDead))
				SetUntilDeathBonus(summon, 0f);
		}

		private static async Task MaintainRangedSpacing(Skill skill, Summon summon, float preferredRange, CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested && !summon.IsDead)
			{
				var enemies = summon.Map?.GetAttackableEnemiesInPosition(summon, summon.Position, preferredRange * 0.8f).ToList();
				var nearest = enemies?.OrderBy(e => e.Position.Get2DDistance(summon.Position)).FirstOrDefault();
				if (nearest != null)
				{
					var angle = nearest.Position.GetDirection(summon.Position);
					var away = summon.Position.GetRelative(angle, 30f);
					if (summon.Map.Ground.TryGetNearestValidPosition(away, out var valid))
						summon.MoveTo(valid);
				}

				await skill.Wait(TimeSpan.FromMilliseconds(700));
			}
		}

		private static void CancelCurrentOrder(Summon summon)
		{
			if (summon.Vars.TryGet<CancellationTokenSource>(OrderCancellationKey, out var cts))
			{
				cts.Cancel();
				cts.Dispose();
			}

			summon.Vars.Remove(OrderCancellationKey);
			summon.Vars.SetString(OrderScriptKey, "None");
		}

		private static void StartClericHealingLoop(Skill skill, Character owner, Summon mage)
		{
			_ = HealRandomSummonLoop(skill, owner, mage);
		}

		private static async Task HealRandomSummonLoop(Skill skill, Character owner, Summon mage)
		{
			while (!mage.IsDead && owner.Map != null)
			{
				await skill.Wait(TimeSpan.FromSeconds(owner.IsAbilityActive(AbilityId.Necromancer26) ? 5 : 15));
				if (mage.IsDead)
					return;

				if (owner.IsAbilityActive(AbilityId.Necromancer26))
				{
					var rnd = RandomProvider.Get();
					if (rnd.Next(100) < 1)
						owner.RemoveRandomBuff(100);
					if (rnd.Next(100) >= 33)
						continue;

					if (rnd.Next(2) == 0)
					{
						ApplySupportDamageBonus(skill, owner, 0.10f, TimeSpan.FromSeconds(8));
					}
					else
					{
						var ownerHeal = owner.MaxHp * 0.05f;
						owner.Heal(ownerHeal, 0);
						Send.ZC_HEAL_INFO(owner, ownerHeal, owner.Hp, HealType.Hp);
					}
					continue;
				}

				var candidates = GetNecromancerSummons(owner).Where(s => !s.IsDead && s.Hp < s.MaxHp).ToList();
				var target = candidates.Count == 0 ? null : candidates[RandomProvider.Get().Next(candidates.Count)];
				if (target != null)
				{
					var heal = target.MaxHp * 0.20f;
					target.Heal(heal, 0);
					Send.ZC_HEAL_INFO(target, heal, target.Hp, HealType.Hp);
				}
			}
		}

		private static async Task StartMageMagicShieldLoop(Skill skill, Character owner, Summon mage)
		{
			while (!mage.IsDead && owner.Map != null)
			{
				await skill.Wait(TimeSpan.FromSeconds(12));
				if (!mage.IsDead)
				{
					mage.StartBuff(BuffId.Mon_joint_MagicShield, 4, 0, TimeSpan.FromSeconds(10), owner, SkillId.Necromancer_RaiseSkullwizard);
					mage.PlayEffect("F_wizard_reflectshield_shot", 1f);
				}
			}
		}

		private static async Task ClearSupportDamageBonusWhenExpired(Skill skill, Character owner, string token, TimeSpan duration)
		{
			await skill.Wait(duration);
			foreach (var summon in GetNecromancerSummons(owner).Where(s => !s.IsDead && s.Vars.GetString(SupportDamageTokenKey, "") == token))
			{
				summon.Vars.SetFloat(SupportDamageBonusKey, 0f);
				summon.Vars.Remove(SupportDamageTokenKey);
			}
		}

		private static async Task ClearUntilDeathBonusWhenBuffEnds(Skill skill, Character owner, string token)
		{
			await skill.Wait(UntilDeathDuration);

			if (owner.Map == null || owner.Variables.Temp.GetString(UntilDeathBuffTokenKey, "") != token)
				return;

			ClearUntilDeathBonus(owner);
			owner.RemoveBuff(BuffId.UntilDeath_Buff);
			owner.Variables.Temp.Remove(UntilDeathBuffTokenKey);
		}
	}
}
