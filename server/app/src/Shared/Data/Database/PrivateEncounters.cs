using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Yggdrasil.Data.JSON;

namespace Melia.Shared.Data.Database
{
	[Serializable]
	public class PrivateEncounterData
	{
		public int Id { get; set; }
		public string QuestName { get; set; }
		public string MapName { get; set; }
		public string Target { get; set; }
		public int MinSpawnCount { get; set; }
		public List<string> MapPointGroup { get; set; }
	}

	public class PrivateEncounterDb : DatabaseJsonIndexed<int, PrivateEncounterData>
	{
		public IEnumerable<PrivateEncounterData> FindByQuest(string questName)
		{
			if (string.IsNullOrWhiteSpace(questName))
				return Enumerable.Empty<PrivateEncounterData>();

			return this.Entries.Values.Where(entry => string.Equals(entry.QuestName, questName, StringComparison.OrdinalIgnoreCase));
		}

		public IEnumerable<PrivateEncounterData> FindByQuestAndMap(string questName, string mapName)
		{
			return this.FindByQuest(questName)
				.Where(entry => string.IsNullOrWhiteSpace(entry.MapName) || string.Equals(entry.MapName, mapName, StringComparison.OrdinalIgnoreCase));
		}

		protected override void ReadEntry(JObject entry)
		{
			entry.AssertNotMissing("id", "questName", "target", "mapPointGroup");

			var data = new PrivateEncounterData();
			data.Id = entry.ReadInt("id");
			data.QuestName = entry.ReadString("questName");
			data.MapName = entry.ReadString("mapName");
			data.Target = entry.ReadString("target");
			data.MinSpawnCount = entry.ReadInt("minSpawnCount", 1);
			data.MapPointGroup = entry.ReadList<string>("mapPointGroup");

			this.AddOrReplace(data.Id, data);
		}
	}
}
