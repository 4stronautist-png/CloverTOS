using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Yggdrasil.Data.JSON;

namespace Melia.Shared.Data.Database
{
	[Serializable]
	public class QuestStaticData
	{
		public int Id { get; set; }
		public string ClassName { get; set; }
		public string Name { get; set; }
		public int Level { get; internal set; }
		public string QuestSSN { get; internal set; }
		public string QuestProperty { get; internal set; }
		public string QuestMode { get; internal set; }
		public string QuestStartMode { get; internal set; }
		public string QuestEndMode { get; internal set; }
		public string QStartZone { get; internal set; }
		public string StartMap { get; internal set; }
		public string StartLocation { get; internal set; }
		public string StartNPC { get; internal set; }
		public string ProgMap { get; internal set; }
		public string ProgLocation { get; internal set; }
		public string ProgNPC { get; internal set; }
		public string EndMap { get; internal set; }
		public string EndLocation { get; internal set; }
		public string EndNPC { get; internal set; }
		public string RequiredQuestItem { get; internal set; }
		public List<string> RequiredQuests { get; internal set; }
		public List<string> CheckScripts { get; internal set; }
		public List<QuestObjectiveStaticData> Objectives { get; internal set; }
		public List<QuestRewardItemStaticData> RewardItems { get; internal set; }
	}

	[Serializable]
	public class QuestObjectiveStaticData
	{
		public string Ident { get; set; }
		public string Type { get; set; }
		public string Text { get; set; }
		public string Target { get; set; }
		public string Item { get; set; }
		public string DropTarget { get; set; }
		public float DropChance { get; set; }
		public int Count { get; set; }
	}

	[Serializable]
	public class QuestRewardItemStaticData
	{
		public string Item { get; set; }
		public int Amount { get; set; }
	}

	/// <summary>
	/// Quest database, indexed by quest id.
	/// </summary>
	public class QuestDb : DatabaseJsonIndexed<int, QuestStaticData>
	{
		/// <summary>
		/// Returns all quest entries.
		/// </summary>
		public IEnumerable<QuestStaticData> GetList()
			=> this.Entries.Values;

		/// <summary>
		/// Returns first Quest data entry with given class name, or null
		/// if it wasn't found.
		/// </summary>
		/// <param name="className"></param>
		/// <returns></returns>
		public QuestStaticData Find(string className)
		{
			return this.Entries.Values.FirstOrDefault(a => a.ClassName.ToLower() == className.ToLower());
		}

		public bool TryFind(string className, out QuestStaticData quest)
		{
			quest = this.Find(className);
			return quest != null;
		}

		protected override void ReadEntry(JObject entry)
		{
			entry.AssertNotMissing("id", "className", "name");

			var info = new QuestStaticData();

			info.Id = entry.ReadInt("id");
			info.ClassName = entry.ReadString("className");
			info.Name = entry.ReadString("name");

			info.Level = entry.ReadInt("level", 0);
			info.QuestSSN = entry.ReadString("questSSN");
			info.QuestProperty = entry.ReadString("questPropertyName");
			info.QuestMode = entry.ReadString("questMode");
			info.QuestStartMode = entry.ReadString("questStartMode");
			info.QuestEndMode = entry.ReadString("questEndMode");
			info.QStartZone = entry.ReadString("questStartZone");
			info.StartMap = entry.ReadString("startMap");
			info.StartLocation = entry.ReadString("startLocation");
			info.StartNPC = entry.ReadString("startNPC");
			info.ProgMap = entry.ReadString("progressMap");
			info.ProgLocation = entry.ReadString("progressLocation");
			info.ProgNPC = entry.ReadString("progressNPC");
			info.EndMap = entry.ReadString("endMap");
			info.EndLocation = entry.ReadString("endLocation");
			info.EndNPC = entry.ReadString("endNPC");
			info.RequiredQuestItem = entry.ReadString("requiredQuestItem");
			info.RequiredQuests = entry.ReadList<string>("requiredQuestName");
			info.CheckScripts = entry.ReadList<string>("checkScripts");
			info.Objectives = ReadObjectives(entry);
			info.RewardItems = ReadRewardItems(entry);

			this.Entries[info.Id] = info;
		}

		private static List<QuestObjectiveStaticData> ReadObjectives(JObject entry)
		{
			if (!entry.ContainsKey("objectives"))
				return null;

			var result = new List<QuestObjectiveStaticData>();
			foreach (var token in (JArray)entry["objectives"])
			{
				var obj = (JObject)token;
				result.Add(new QuestObjectiveStaticData
				{
					Ident = obj.ReadString("ident"),
					Type = obj.ReadString("type"),
					Text = obj.ReadString("text"),
					Target = obj.ReadString("target"),
					Item = obj.ReadString("item"),
					DropTarget = obj.ReadString("dropTarget"),
					DropChance = obj.ReadFloat("dropChance", 1),
					Count = obj.ReadInt("count", 1),
				});
			}
			return result;
		}

		private static List<QuestRewardItemStaticData> ReadRewardItems(JObject entry)
		{
			if (!entry.ContainsKey("rewardItems"))
				return null;

			var result = new List<QuestRewardItemStaticData>();
			foreach (var token in (JArray)entry["rewardItems"])
			{
				var obj = (JObject)token;
				result.Add(new QuestRewardItemStaticData
				{
					Item = obj.ReadString("item"),
					Amount = obj.ReadInt("amount", 1),
				});
			}
			return result;
		}
	}
}
