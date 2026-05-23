using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Yggdrasil.Data.JSON;

namespace Melia.Shared.Data.Database
{
	[Serializable]
	public class QuestAutoData
	{
		public int Id { get; set; }
		public string QuestName { get; set; }
		public string Track { get; set; }
		public bool TrackAutoComplete { get; set; }
		public List<string> SuccessNextQuestNames { get; set; }
	}

	public class QuestAutoDb : DatabaseJsonIndexed<int, QuestAutoData>
	{
		public QuestAutoData Find(string questName)
		{
			if (string.IsNullOrWhiteSpace(questName))
				return null;

			return this.Entries.Values.FirstOrDefault(entry => string.Equals(entry.QuestName, questName, StringComparison.OrdinalIgnoreCase));
		}

		protected override void ReadEntry(JObject entry)
		{
			entry.AssertNotMissing("id", "questName");

			var data = new QuestAutoData();
			data.Id = entry.ReadInt("id");
			data.QuestName = entry.ReadString("questName");
			data.Track = entry.ReadString("track");
			data.TrackAutoComplete = entry.ReadBool("trackAutoComplete", false);
			data.SuccessNextQuestNames = entry.ReadList<string>("successNextQuestNames", new List<string>());

			this.AddOrReplace(data.Id, data);
		}
	}
}
