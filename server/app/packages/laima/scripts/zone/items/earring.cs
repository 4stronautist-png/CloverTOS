//--- Melia Script ----------------------------------------------------------
// Fire Flame Earring items
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.Network;
using Melia.Shared.Network.Inter.Messages;
using Melia.Zone;
using Melia.Zone.Network;
using Melia.Zone.Scripting;
using Melia.Zone.Skills;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.Characters.Components;
using Melia.Zone.World.Items;
using Yggdrasil.Logging;
using Yggdrasil.Network.Communication;
using Yggdrasil.Util;

public class EarringItemScripts : GeneralScript
{
	private const int NormalEarringId = ItemId.EP13_GabijaEarring;
	private const int BoundEarringId = ItemId.NoTrade_EP13_GabijaEarring;
	private const int RareHighTotalChancePerMillion = 17;
	private const int AdvancedThirdLineChancePerMillion = 17;
	private const int RollRetryLimit = 100;

	private static readonly HashSet<JobId> ExcludedJobs = new()
	{
		JobId.Swordsman,
		JobId.Wizard,
		JobId.Archer,
		JobId.Cleric,
		JobId.Scout,
		JobId.Centurion,
		JobId.GM,
	};

	private static readonly HashSet<string> AllowedUseMaps = new(StringComparer.OrdinalIgnoreCase)
	{
		"c_Klaipe",
		"c_orsha",
		"c_fedimian",
	};

	[ScriptableFunction]
	public ItemUseResult SCR_USE_EARRING_MISC(Character character, Item item, string strArg, float numArg1, float numArg2)
	{
		if (!CanUseHere(character))
		{
			character.AddonMessage(AddonMessage.NOTICE_Dm_Clear, "Fire Flame Earring Fragments can only be used in Klaipeda, Orsha or Fedimian.", 3);
			return ItemUseResult.Fail;
		}

		var sourceItemId = item.Id;
		var requiredAmount = GetRequiredAmount(sourceItemId, numArg1);
		if (character.Inventory.CountItem(sourceItemId) < requiredAmount)
		{
			character.AddonMessage(AddonMessage.NOTICE_Dm_Clear, $"You need {requiredAmount}x {item.Data.Name} to use this item. ({character.Inventory.CountItem(sourceItemId)}/{requiredAmount})", 3);
			return ItemUseResult.Fail;
		}

		var earring = new Item(GetRewardEarringId(sourceItemId), 1);
		var isMystic = IsMysticFireFlameFragment(sourceItemId);
		var isMysticII = IsMysticFireFlameFragmentII(sourceItemId);
		var isAdvanced = IsAdvancedFireFlameFragment(sourceItemId);

		AddBaseOptions(earring);
		var selectedLines = isMystic
			? AddMysticOptions(character, earring, isMysticII)
			: AddRandomClassOptions(character, earring, isAdvanced);

		if (!ConsumeFragmentStack(character, item, requiredAmount))
			return ItemUseResult.Fail;

		character.Inventory.Add(earring, InventoryAddType.New, reason: "FireFlameEarring");
		Send.ZC_ITEM_USE(character, sourceItemId);
		PlayOpenEffect(character);
		BroadcastRareEarring(selectedLines);
		return ItemUseResult.OkayNotConsumed;
	}

	[ScriptableFunction]
	public ItemTxResult SCR_EARRING_SELECT_JOB(Character character, Item item, int[] numArgs)
	{
		if (item == null)
			return ItemTxResult.Fail;

		if (!CanUseHere(character))
		{
			character.AddonMessage(AddonMessage.NOTICE_Dm_Clear, "Mystic Fire Flame Earring Fragments can only be used in Klaipeda, Orsha or Fedimian.", 3);
			return ItemTxResult.Fail;
		}

		if (!TryGetSelectedMysticJob(numArgs, out var selectedJob))
		{
			Log.Warning("SCR_EARRING_SELECT_JOB: Invalid or unsupported selected job. Item: {0}, Args: {1}", item.Id, string.Join(",", numArgs ?? Array.Empty<int>()));
			return ItemTxResult.Fail;
		}

		var sourceItemId = item.Id;
		var earring = new Item(GetRewardEarringId(sourceItemId), 1);
		AddBaseOptions(earring);
		var selectedLines = AddMysticOptions(earring, selectedJob, IsMysticFireFlameFragmentII(sourceItemId));

		character.Inventory.Add(earring, InventoryAddType.New, reason: "MysticFireFlameEarring");
		Send.ZC_ITEM_USE(character, sourceItemId);
		PlayOpenEffect(character);
		BroadcastRareEarring(selectedLines);
		return ItemTxResult.Okay;
	}

	private static int GetRewardEarringId(int sourceItemId)
	{
		return sourceItemId is ItemId.Piece_GabijaEarring_Select_Job_NoTrade_Belonging
			or ItemId.Piece_GabijaEarring_Select_Job2_NoTrade_Belonging
			or ItemId.Piece_GabijaEarring_NoTrade
			or ItemId.Piece_GabijaEarring_Grade2_NoTrade
			or ItemId.Piece_GabijaEarring_Grade3_NoTrade
			? BoundEarringId
			: NormalEarringId;
	}

	private static bool IsAdvancedFireFlameFragment(int sourceItemId)
	{
		return sourceItemId is 11201161 or 11201162;
	}

	private static bool IsMysticFireFlameFragment(int sourceItemId)
	{
		return sourceItemId is ItemId.Piece_GabijaEarring_Select_Job
			or ItemId.Piece_GabijaEarring_Select_Job_NoTrade
			or ItemId.Piece_GabijaEarring_Select_Job_NoTrade_Belonging
			or ItemId.Piece_GabijaEarring_Select_Job2
			or ItemId.Piece_GabijaEarring_Select_Job2_NoTrade
			or ItemId.Piece_GabijaEarring_Select_Job2_NoTrade_Belonging;
	}

	private static bool IsMysticFireFlameFragmentII(int sourceItemId)
	{
		return sourceItemId is ItemId.Piece_GabijaEarring_Select_Job2
			or ItemId.Piece_GabijaEarring_Select_Job2_NoTrade
			or ItemId.Piece_GabijaEarring_Select_Job2_NoTrade_Belonging;
	}

	private static int GetRequiredAmount(int sourceItemId, float scriptAmount)
	{
		return sourceItemId is ItemId.Piece_GabijaEarring
			or ItemId.Piece_GabijaEarring_NoTrade
			or 11201161
			or 11201162
			? 6
			: Math.Max(1, (int)scriptAmount);
	}

	private static void AddBaseOptions(Item earring)
	{
		var random = RandomProvider.Get();
		var statOptions = new[] { "STR", "DEX", "INT", "MNA", "CON" };
		var optionCount = RollStatCount(random);

		foreach (var stat in statOptions.OrderBy(_ => random.Next()).Take(optionCount).Select((stat, index) => new { stat, index }))
			SetRandomOption(earring, stat.index + 1, "STAT", stat.stat, random.Next(50, 151));
	}

	private static List<EarringSkillLine> AddMysticOptions(Character character, Item earring, bool isMysticII)
	{
		var currentJob = ZoneServer.Instance.Data.JobDb.Find(character.JobId);
		var jobs = currentJob != null && IsEligibleEarringJob(currentJob)
			? new[] { currentJob }
			: GetEligibleEarringJobs().ToArray();

		return AddSpecialOptions(earring, jobs, EarringFragmentMode.Mystic, isMysticII);
	}

	private static List<EarringSkillLine> AddMysticOptions(Item earring, JobData selectedJob, bool isMysticII)
	{
		return AddSpecialOptions(earring, new[] { selectedJob }, EarringFragmentMode.Mystic, isMysticII);
	}

	private static List<EarringSkillLine> AddRandomClassOptions(Character character, Item earring, bool isAdvanced)
	{
		return AddSpecialOptions(earring, GetEligibleEarringJobs(), EarringFragmentMode.Regular, isAdvanced);
	}

	private static List<EarringSkillLine> AddSpecialOptions(Item earring, IEnumerable<JobData> jobs, EarringFragmentMode mode, bool isAdvanced)
	{
		var random = RandomProvider.Get();
		var jobList = jobs.Where(IsEligibleEarringJob).DistinctBy(job => job.Id).ToList();
		if (jobList.Count == 0)
			return new List<EarringSkillLine>();

		var selectedLines = RollSpecialLines(jobList, mode, isAdvanced, random);
		for (var i = 0; i < selectedLines.Count; i++)
		{
			var optionIndex = i + 1;
			earring.Properties.SetString($"EarringSpecialOption_{optionIndex}", selectedLines[i].Job.ClassName);
			earring.Properties.SetFloat($"EarringSpecialOptionLevelValue_{optionIndex}", selectedLines[i].Level);
			earring.Properties.SetFloat($"EarringSpecialOptionRankValue_{optionIndex}", selectedLines[i].Line);
		}

		return selectedLines;
	}

	private static List<EarringSkillLine> RollSpecialLines(List<JobData> jobs, EarringFragmentMode mode, bool isAdvanced, Random random)
	{
		if (mode == EarringFragmentMode.Mystic)
			return RollMysticSpecialLines(jobs, isAdvanced, random);

		if (!isAdvanced)
			return RollRegularSpecialLine(jobs, random);

		return RollAdvancedSpecialLines(jobs, random);
	}

	private static List<EarringSkillLine> RollMysticSpecialLines(List<JobData> jobs, bool isMysticII, Random random)
	{
		var job = jobs[random.Next(jobs.Count)];
		var lineCount = Math.Min(RollMysticLineCount(isMysticII, random), GetAvailableSkillLines(job.Id).Count());
		var skillLines = GetAvailableSkillLines(job.Id).OrderBy(_ => random.Next()).Take(lineCount);

		return skillLines
			.Select(line => new EarringSkillLine(job, line, RollMysticSkillLevel(random)))
			.ToList();
	}

	private static List<EarringSkillLine> RollRegularSpecialLine(List<JobData> jobs, Random random)
	{
		var job = jobs[random.Next(jobs.Count)];
		return new List<EarringSkillLine>
		{
			new(job, RollSkillLine(job.Id, random), RollWeightedSkillLevel(random)),
		};
	}

	private static List<EarringSkillLine> RollAdvancedSpecialLines(List<JobData> jobs, Random random)
	{
		var lineCount = random.Next(1_000_000) < AdvancedThirdLineChancePerMillion ? 3 : 2;
		var selectedLines = new List<EarringSkillLine>();
		var usedLines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var baseClass = jobs[random.Next(jobs.Count)].JobClassId;
		var treeJobs = jobs.Where(job => job.JobClassId == baseClass).ToList();

		for (var attempt = 0; selectedLines.Count < lineCount && attempt < RollRetryLimit * lineCount; attempt++)
		{
			var job = treeJobs[random.Next(treeJobs.Count)];
			var line = RollSkillLine(job.Id, random);
			var key = $"{job.ClassName}:{line}";
			if (!usedLines.Add(key))
				continue;

			selectedLines.Add(new EarringSkillLine(job, line, RollWeightedSkillLevel(random)));
		}

		if (selectedLines.Count == lineCount)
			return selectedLines;

		foreach (var job in treeJobs.OrderBy(_ => random.Next()))
		{
			foreach (var line in GetAvailableSkillLines(job.Id).OrderBy(_ => random.Next()))
			{
				var key = $"{job.ClassName}:{line}";
				if (!usedLines.Add(key))
					continue;

				selectedLines.Add(new EarringSkillLine(job, line, RollWeightedSkillLevel(random)));
				if (selectedLines.Count == lineCount)
					return selectedLines;
			}
		}

		return selectedLines;
	}

	private static void PlayOpenEffect(Character character)
	{
		Send.ZC_NORMAL.PlayEffect(character.Connection, character, animationName: "F_spread_in012_blue", scale: 0.5f);
	}

	private static bool ConsumeFragmentStack(Character character, Item item, int requiredAmount)
	{
		if (item.Amount >= requiredAmount)
		{
			if (item.Amount == requiredAmount)
				return character.Inventory.Remove(item, requiredAmount, InventoryItemRemoveMsg.Used) == InventoryResult.Success;

			var inventoryEntry = character.Inventory.GetItems(candidate => candidate.ObjectId == item.ObjectId).FirstOrDefault();
			var previousAmount = item.Amount;
			var categoryIndex = item.GetInventoryIndex(inventoryEntry.Key);

			if (character.Inventory.Remove(item, requiredAmount, InventoryItemRemoveMsg.Used, InventoryType.Inventory, silently: true) != InventoryResult.Success)
				return false;

			Send.ZC_ITEM_REMOVE(character, item.ObjectId, previousAmount, InventoryItemRemoveMsg.Used, InventoryType.Inventory);
			Send.ZC_ITEM_ADD(character, item, categoryIndex, item.Amount, InventoryAddType.NotNew);
			Send.ZC_EQUIP_GEM_INFO(character);
			return true;
		}

		return character.Inventory.Remove(item.Id, requiredAmount, InventoryItemRemoveMsg.Used) == requiredAmount;
	}

	private static void BroadcastRareEarring(List<EarringSkillLine> lines)
	{
		var rank = lines.Sum(line => line.Level);
		if (rank < 10 || rank > 15)
			return;

		var commMessage = new NoticeTextMessage(NoticeTextType.GoldRed, $"Um brinco Rank {rank} apareceu.");
		ZoneServer.Instance.Communicator.Send("Coordinator", commMessage.BroadcastTo("AllZones"));
	}

	private static List<EarringSkillLine> ClampTotal(List<EarringSkillLine> lines, int maxTotal)
	{
		var total = lines.Sum(line => line.Level);
		for (var i = lines.Count - 1; i >= 0 && total > maxTotal; i--)
		{
			var reduction = Math.Min(lines[i].Level - 1, total - maxTotal);
			if (reduction <= 0)
				continue;

			lines[i] = new EarringSkillLine(lines[i].Job, lines[i].Line, lines[i].Level - reduction);
			total -= reduction;
		}

		return lines;
	}

	private static int RollStatCount(Random random)
	{
		var roll = random.Next(100);
		if (roll < 85)
			return 1;
		if (roll < 95)
			return 2;
		return 3;
	}

	private static int RollMysticLineCount(bool isMysticII, Random random)
	{
		var roll = random.Next(1_000_000);
		var twoLineChance = isMysticII ? 150_412 : 75_206;
		var threeLineChance = isMysticII ? 8_194 : 4_097;

		if (roll < 1_000_000 - twoLineChance - threeLineChance)
			return 1;
		if (roll < 1_000_000 - threeLineChance)
			return 2;
		return 3;
	}

	private static bool TryGetSelectedMysticJob(int[] numArgs, out JobData selectedJob)
	{
		selectedJob = null;

		if (numArgs == null)
			return false;

		foreach (var rawJobId in numArgs.Reverse())
		{
			var job = ZoneServer.Instance.Data.JobDb.Find((JobId)rawJobId);
			if (IsEligibleEarringJob(job))
			{
				selectedJob = job;
				return true;
			}
		}

		return false;
	}

	private static int RollSkillLine(JobId jobId, Random random)
	{
		var lines = GetAvailableSkillLines(jobId).ToArray();
		return lines.Length == 0 ? 1 : lines[random.Next(lines.Length)];
	}

	private static int RollMysticSkillLevel(Random random)
	{
		return random.Next(1, 6);
	}

	private static int RollWeightedSkillLevel(Random random)
	{
		var roll = random.Next(100);
		if (roll < 93)
			return random.Next(1, 4);
		if (roll < 98)
			return 4;
		return 5;
	}

	private static IEnumerable<JobData> GetEligibleEarringJobs()
	{
		return ZoneServer.Instance.Data.JobDb.Entries.Values
			.Where(IsEligibleEarringJob)
			.OrderBy(_ => RandomProvider.Get().Next());
	}

	private static bool IsEligibleEarringJob(JobData job)
	{
		return job != null
			&& !ExcludedJobs.Contains(job.Id)
			&& job.Rank > 1
			&& job.JobClassId != JobClass.GM;
	}

	private static IEnumerable<int> GetAvailableSkillLines(JobId jobId)
	{
		var lines = ZoneServer.Instance.Data.SkillTreeDb.Entries
			.Where(skillTree => skillTree.JobId == jobId)
			.Select(skillTree => GetSkillLine(skillTree.UnlockLevel))
			.Where(line => line >= 1 && line <= 3)
			.Distinct()
			.ToArray();

		return lines.Length == 0 ? new[] { 1, 2, 3 } : lines;
	}

	private static int GetSkillLine(int unlockLevel)
	{
		if (unlockLevel >= 31)
			return 3;
		if (unlockLevel >= 16)
			return 2;
		return 1;
	}

	private readonly struct EarringSkillLine
	{
		public EarringSkillLine(JobData job, int line, int level)
		{
			this.Job = job;
			this.Line = line;
			this.Level = level;
		}

		public JobData Job { get; }
		public int Line { get; }
		public int Level { get; }
	}

	private enum EarringFragmentMode
	{
		Regular,
		Mystic,
	}

	private static bool CanUseHere(Character character)
		=> character?.Map != null && AllowedUseMaps.Contains(character.Map.ClassName);

	private static void SetRandomOption(Item item, int optionIndex, string group, string option, float value)
	{
		item.Properties.SetString($"RandomOptionGroup_{optionIndex}", group);
		item.Properties.SetString($"RandomOption_{optionIndex}", option);
		item.Properties.SetFloat($"RandomOptionValue_{optionIndex}", value);
		item.Properties.Modify(option, value);
	}
}

public static class EarringSkillLineEffects
{
	public static void Apply(Character character, Item item)
	{
		Modify(character, item, 1);
	}

	public static void Remove(Character character, Item item)
	{
		Modify(character, item, -1);
	}

	private static void Modify(Character character, Item item, int sign)
	{
		if (item.Id != ItemId.EP13_GabijaEarring && item.Id != ItemId.NoTrade_EP13_GabijaEarring)
			return;

		var changedSkills = false;
		for (var optionIndex = 1; optionIndex <= 3; optionIndex++)
		{
			var jobClassName = item.Properties.GetString($"EarringSpecialOption_{optionIndex}", "");
			if (string.IsNullOrWhiteSpace(jobClassName) || jobClassName == "None")
				continue;

			if (!ZoneServer.Instance.Data.JobDb.TryFind(jobClassName, out var job))
				continue;

			if (!character.Jobs.Has(job.Id))
				continue;

			var line = (int)item.Properties.GetFloat($"EarringSpecialOptionRankValue_{optionIndex}", 0);
			var levelBonus = (int)item.Properties.GetFloat($"EarringSpecialOptionLevelValue_{optionIndex}", 0);
			if (line < 1 || line > 3 || levelBonus == 0)
				continue;

			foreach (var skillTree in ZoneServer.Instance.Data.SkillTreeDb.Entries.Where(entry => entry.JobId == job.Id && GetSkillLine(entry.UnlockLevel) == line))
			{
				if (!TryGetOrCreateEarringSkill(character, skillTree.SkillId, out var skill))
					continue;

				var currentBonus = skill.Vars.GetFloat("EarringLevel_BM", 0);
				var newBonus = sign > 0
					? currentBonus + levelBonus
					: Math.Max(0, currentBonus - levelBonus);

				skill.Vars.SetFloat("EarringLevel_BM", newBonus);
				skill.Properties.InvalidateAll();
				skill.RecalculateDependentBuffs();

				if (character.Connection != null)
					Send.ZC_NORMAL.SkillProperties(character.Connection, 0, skill);

				if (sign < 0
					&& skill.IsEquipSkill
					&& skill.LevelByDB == 0
					&& skill.Vars.GetFloat("EarringLevel_BM", 0) <= 0
					&& skill.Properties.GetFloat(PropertyName.GemLevel_BM, 0) <= 0)
					character.Skills.Remove(skill.Id);

				changedSkills = true;
			}
		}

		if (!changedSkills || character.Connection == null)
			return;

		Send.ZC_SKILL_LIST(character);
		Send.ZC_COMMON_SKILL_LIST(character);
		Send.ZC_NORMAL.SetSkillsProperties(character.Connection);
		Send.ZC_NORMAL.UpdateSkillUI(character);
	}

	private static int GetSkillLine(int unlockLevel)
	{
		if (unlockLevel >= 31)
			return 3;
		if (unlockLevel >= 16)
			return 2;
		return 1;
	}

	private static bool TryGetOrCreateEarringSkill(Character character, SkillId skillId, out Skill skill)
	{
		if (character.Skills.TryGet(skillId, out skill))
			return true;

		if (ZoneServer.Instance.Data.SkillDb.Find(skillId) == null)
			return false;

		skill = new Skill(character, skillId, 0, isEquipSkill: true);
		character.Skills.AddSilent(skill);
		return true;
	}
}
