using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Melia.Shared.ObjectProperties;
using Melia.Shared.Scripting;
using Melia.Shared.Game.Const;
using Melia.Shared.Game.Properties;
using Melia.Shared.World;
using Melia.Zone.Events.Arguments;
using Melia.Zone.Network;
using Melia.Zone.Scripting;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.CombatEntities.Components;
using Melia.Zone.World.Actors.Monsters;
using Melia.Zone.World.Quests;
using Melia.Zone.World.Quests.Modifiers;
using Melia.Zone.World.Quests.Objectives;
using Melia.Zone.World.Quests.Rewards;
using Melia.Zone.World.Tracks;
using Yggdrasil.Scheduling;
using Yggdrasil.Util;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Yggdrasil.Logging;
using QuestAutoData = Melia.Shared.Data.Database.QuestAutoData;
using QuestStaticData = Melia.Shared.Data.Database.QuestStaticData;
using SessionQuestData = Melia.Shared.Data.Database.QuestData;

namespace Melia.Zone.World.Actors.Characters.Components
{
	/// <summary>
	/// A character's quest manager.
	/// </summary>
	/// <remarks>
	/// Our current quest system is custom-made, as the system the game
	/// comes with is not very flexible. Using our own allows us to freely
	/// create custom quests, add features that wouldn't be available
	/// otherwise, and generally be independent of the game's ideas of
	/// quests. The downside is that our system might require some
	/// rethinking when trying to replicate the game's quests.
	/// </remarks>
	public class QuestComponent : CharacterComponent, IUpdateable
	{
		private readonly static TimeSpan AutoReceiveDelay = TimeSpan.FromMinutes(1);
		private readonly static TimeSpan LocationCheckInterval = TimeSpan.FromSeconds(1);
		private const int MaxClientTrackedQuestSlots = 5;
		private const int MaxClientQuestCheckProperties = 10;
		private readonly static object StaticObjectiveLoadLock = new();
		private readonly static HashSet<Type> LoadedStaticObjectiveTypes = new();
		private readonly static object StaticModifierLoadLock = new();
		private readonly static HashSet<Type> LoadedStaticModifierTypes = new();
		private readonly static object MainPrerequisiteBridgeLock = new();
		private static HashSet<string> MainPrerequisiteBridgeQuestNames;

		private readonly object _syncLock = new();
		private readonly List<Quest> _quests = new();
		private readonly List<long> _disabledQuests = new();

		private TimeSpan _autoReceiveDelay = AutoReceiveDelay;
		private TimeSpan _timeSinceLastLocationCheck = TimeSpan.Zero;
		private int _lastWestSiauliaiTrackedQuestId = int.MinValue;
		private string _lastTrackedQuestSignature = "";
		private readonly Dictionary<string, DateTime> _staticQuestObjectiveSpawnPending = new(StringComparer.OrdinalIgnoreCase);
		private bool _papayaCrystalMineSkipInProgress;

		/// <summary>
		/// Creates new instance for character.
		/// </summary>
		/// <param name="character"></param>
		public QuestComponent(Character character)
			: base(character)
		{
		}

		/// <summary>
		/// Clears all quests to release references for GC.
		/// </summary>
		public void Clear()
		{
			lock (_syncLock)
			{
				_quests.Clear();
				_disabledQuests.Clear();
			}
		}

		/// <summary>
		/// Notes the given quest db id as disabled.
		/// </summary>
		/// <remarks>
		/// Used to remember quests to keep around that are not currently
		/// loaded by the server, but should still be available to the
		/// character once they are. See quest loading and saving.
		/// </remarks>
		/// <param name="questDbId"></param>
		internal void AddDisabledQuest(long questDbId)
		{
			lock (_syncLock)
				_disabledQuests.Add(questDbId);
		}

		/// <summary>
		/// Returns a list of all disabled quests.
		/// </summary>
		/// <returns></returns>
		internal IList<long> GetDisabledQuests()
		{
			lock (_syncLock)
				return _disabledQuests.ToArray();
		}

		/// <summary>
		/// Returns true if the quest with the given database id is
		/// disabled.
		/// </summary>
		/// <param name="questDbId"></param>
		/// <returns></returns>
		internal bool IsDisabled(long questDbId)
		{
			lock (_syncLock)
				return _disabledQuests.Contains(questDbId);
		}

		/// <summary>
		/// Adds quest without informing the client.
		/// </summary>
		/// <remarks>
		/// This is primarily used while the character and its quests are
		/// loaded from the database.
		/// </remarks>
		/// <param name="quest"></param>
		public void AddSilent(Quest quest)
		{
			lock (_syncLock)
			{
				var oldQuest = _quests.Where(q => q.Data.Id == quest.Data.Id).FirstOrDefault();
				if (oldQuest != null)
					_quests.Remove(oldQuest);
				_quests.Add(quest);
			}
		}

		/// <summary>
		/// Gets quest by id and returns it via out, returns false if the
		/// quest didn't exist.
		/// </summary>
		/// <param name="questObjectId"></param>
		/// <param name="quest"></param>
		/// <returns></returns>
		public bool TryGet(long questObjectId, out Quest quest)
		{
			lock (_syncLock)
			{
				quest = _quests.Find(a => a.ObjectId == questObjectId);
				return quest != null;
			}
		}

		/// <summary>
		/// Gets quest by id and returns it via out, returns false if the
		/// quest didn't exist.
		/// </summary>
		/// <param name="questId"></param>
		/// <param name="quest"></param>
		/// <returns></returns>
		public bool TryGetById(long questId, out Quest quest)
		{
			lock (_syncLock)
			{
				quest = _quests.Find(a => a.Data.Id.Value == questId);
				return quest != null;
			}
		}

		/// <summary>
		/// Gets quest by id and returns it via out, returns false if the
		/// quest didn't exist.
		/// </summary>
		/// <param name="questId"></param>
		/// <param name="quest"></param>
		/// <returns></returns>
		public bool TryGetById(QuestId questId, out Quest quest)
		{
			lock (_syncLock)
			{
				quest = _quests.Find(a => a.Data.Id == questId);
				return quest != null;
			}
		}

		/// <summary>
		/// Returns a list of all active quests.
		/// </summary>
		/// <returns></returns>
		public Quest[] GetInProgress()
		{
			lock (_syncLock)
				return _quests.Where(a => a.InProgress).ToArray();
		}

		/// <summary>
		/// Returns a list with all of the character's quests.
		/// </summary>
		/// <returns></returns>
		public Quest[] GetList()
		{
			lock (_syncLock)
				return _quests.ToArray();
		}

		public void SyncTrackedQuestSlots()
		{
			if (this.Character?.Connection == null)
				return;

			var currentMap = this.Character.Map?.Data.ClassName;
			var quests = this.GetList();

			if (this.IsWestSiauliaiMap(currentMap))
			{
				this.SyncWestSiauliaiTrackedQuestSlots(quests);
				return;
			}

			var activeQuests = quests
				.Where(quest => this.QuestShouldBeVisibleInClientList(quest, currentMap))
				.OrderByDescending(quest => quest.Tracked)
				.ThenByDescending(quest => quest.Data.Type == QuestType.Main)
				.ThenByDescending(quest => this.QuestTouchesMap(quest, currentMap))
				.ThenBy(quest => quest.Data.Level)
				.ThenBy(quest => quest.StartTime)
				.ToList();

			var papayaMainQuest = this.GetCurrentPapayaCapturedMainTrackedQuest(activeQuests);
			if (papayaMainQuest != null)
			{
				activeQuests.Remove(papayaMainQuest);
				activeQuests.Insert(0, papayaMainQuest);
			}

			var trackedQuests = activeQuests
				.Take(MaxClientTrackedQuestSlots)
				.ToList();

			foreach (var quest in activeQuests)
				quest.Tracked = trackedQuests.Contains(quest);

			foreach (var quest in quests.Except(activeQuests))
				quest.Tracked = false;

			for (var i = 0; i < MaxClientQuestCheckProperties; i++)
			{
				var questId = i < trackedQuests.Count ? (int)trackedQuests[i].Data.Id.Value : 0;
				this.SetQuestCheckSlot(i, questId);
			}

			var trackedQuestSignature = string.Join(",", trackedQuests.Select(quest => $"{quest.QuestStaticData?.ClassName ?? quest.Data.Name}:{quest.Data.Id.Value}"));
			if (!string.Equals(_lastTrackedQuestSignature, trackedQuestSignature, StringComparison.Ordinal))
			{
				Log.Info("Native quest tracker: tracking [{0}] for '{1}' on map '{2}'.", trackedQuestSignature, this.Character.Name, currentMap ?? "unknown");
				_lastTrackedQuestSignature = trackedQuestSignature;
			}
		}

		private bool QuestShouldBeVisibleInClientList(Quest quest, string currentMap = null)
		{
			if (quest == null)
				return false;

			var questData = quest.QuestStaticData;
			var visiblePossibleStaticQuest = quest.IsPossible &&
				questData != null &&
				this.StaticQuestShouldShowPossibleInClientList(questData, currentMap);

			if (!quest.InProgress && quest.Status != QuestStatus.Success && !visiblePossibleStaticQuest)
				return false;

			if (questData == null)
				return true;

			if (this.StaticQuestIsClientHiddenPapayaBridge(questData))
				return false;

			if (string.IsNullOrWhiteSpace(currentMap))
				currentMap = this.Character?.Map?.ClassName ?? this.Character?.Map?.Data?.ClassName;

			if (this.StaticQuestDisabledForCloverFlow(questData))
				return false;

			if (this.StaticQuestIsBlockedByPapayaMainProgression(questData))
				return false;

			if (this.IsPrematureKlaipedaHandoffQuest(quest))
				return false;

			if (this.IsSkippedWestRoadQuestAfterKlaipedaArrival(quest, currentMap))
				return false;

			if (this.IsParkedFromWestSiauliaiMainFlow(quest))
				return false;

			return true;
		}

		private bool StaticQuestShouldShowPossibleInClientList(QuestStaticData questData, string currentMap)
		{
			if (questData == null ||
				!string.Equals(questData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase) ||
				!string.Equals(questData.QuestStartMode, "NPCDIALOG", StringComparison.OrdinalIgnoreCase))
				return false;

			if (this.StaticQuestDisabledForCloverFlow(questData) ||
				this.StaticQuestIsBlockedByPapayaMainProgression(questData))
				return false;

			if (string.IsNullOrWhiteSpace(currentMap))
				currentMap = this.Character?.Map?.ClassName ?? this.Character?.Map?.Data?.ClassName;

			return this.StaticQuestReferencesMap(questData, currentMap);
		}

		private bool IsPrematureKlaipedaHandoffQuest(Quest quest)
		{
			if (this.HasCompleted(1019))
				return false;

			var className = quest?.QuestStaticData?.ClassName;
			return string.Equals(className, "KLAPEDA_GO_TO_EAST", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(className, "EAST_PREPARE", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(className, "EAST_PREPARE_1", StringComparison.OrdinalIgnoreCase);
		}

		private bool IsSkippedWestRoadQuestAfterKlaipedaArrival(Quest quest, string currentMap)
		{
			if (!string.Equals(currentMap, "c_Klaipe", StringComparison.OrdinalIgnoreCase) ||
				!this.HasCompleted(1015) ||
				this.HasCompleted(1019))
				return false;

			var className = quest?.QuestStaticData?.ClassName;
			return string.Equals(className, "SIAUL_WEST_LAIMONAS4", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(className, "SIAUL_WEST_WOOD_SPIRIT", StringComparison.OrdinalIgnoreCase);
		}

		private void SyncWestSiauliaiTrackedQuestSlots(Quest[] quests)
		{
			var trackedQuest = this.GetCurrentWestSiauliaiTrackedQuest(quests);
			var trackedQuestId = trackedQuest != null ? (int)trackedQuest.Data.Id.Value : 0;

			foreach (var quest in quests)
				quest.Tracked = quest == trackedQuest;

			for (var i = 0; i < MaxClientQuestCheckProperties; i++)
			{
				var questId = i == 0 ? trackedQuestId : 0;
				this.SetQuestCheckSlot(i, questId);
			}

			if (_lastWestSiauliaiTrackedQuestId != trackedQuestId)
			{
				var questName = trackedQuest?.QuestStaticData?.ClassName ?? trackedQuest?.Data.Name ?? "none";
				Log.Info("West Siauliai native tracker: tracking quest {0} ({1}) for '{2}' and clearing the remaining QuestCheck slots.", questName, trackedQuestId, this.Character.Name);
				_lastWestSiauliaiTrackedQuestId = trackedQuestId;

				if (trackedQuest != null)
					this.NotifyNativeQuestTracking(trackedQuest, false);
			}
		}

		private Quest GetCurrentWestSiauliaiTrackedQuest(Quest[] quests)
		{
			foreach (var questId in WestSiauliaiMainQuestOrder)
			{
				var quest = quests.FirstOrDefault(candidate =>
					candidate.Data.Id.Value == questId &&
					(candidate.InProgress || candidate.Status == QuestStatus.Success) &&
					!this.IsParkedFromWestSiauliaiMainFlow(candidate));

				if (quest != null)
					return quest;
			}

			return null;
		}

		private Quest GetCurrentPapayaCapturedMainTrackedQuest(List<Quest> quests)
		{
			foreach (var questName in PapayaCapturedMainQuestOrder)
			{
				if (!ZoneServer.Instance.Data.QuestDb.TryFind(questName, out var questData))
					continue;

				var quest = quests.FirstOrDefault(candidate =>
					candidate.Data.Id.Value == questData.Id &&
					(candidate.InProgress || candidate.Status == QuestStatus.Success || candidate.IsPossible) &&
					(candidate.QuestStaticData == null || !this.StaticQuestDisabledForCloverFlow(candidate.QuestStaticData)) &&
					(candidate.QuestStaticData == null || !this.StaticQuestIsBlockedByPapayaMainProgression(candidate.QuestStaticData)));

				if (quest != null)
					return quest;
			}

			return null;
		}

		public void TrackQuestInClientSlot(string questArg)
		{
			if (string.IsNullOrWhiteSpace(questArg))
			{
				this.SyncTrackedQuestSlots();
				return;
			}

			var normalizedArg = questArg.Trim().Trim('"', '\'');
			var quest = this.FindActiveQuestForClientSlot(normalizedArg);
			if (quest != null)
				quest.Tracked = true;

			this.SyncTrackedQuestSlots();
		}

		private Quest FindActiveQuestForClientSlot(string questArg)
		{
			if (string.IsNullOrWhiteSpace(questArg))
				return null;

			Quest quest = null;
			if (questArg.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
				long.TryParse(questArg.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out var hexQuestId))
			{
				this.TryGetById(hexQuestId, out quest);
			}
			else if (long.TryParse(questArg, out var questId))
			{
				this.TryGetById(questId, out quest);
			}

			if (quest != null)
			{
				return quest.InProgress || quest.Status == QuestStatus.Success || quest.IsPossible ? quest : null;
			}

			return this.GetList().FirstOrDefault(quest =>
				(quest.InProgress || quest.Status == QuestStatus.Success || quest.IsPossible) &&
				(string.Equals(quest.QuestStaticData?.ClassName, questArg, StringComparison.OrdinalIgnoreCase) ||
				 string.Equals(quest.Data.Name, questArg, StringComparison.OrdinalIgnoreCase)));
		}

		private bool QuestTouchesMap(Quest quest, string mapClassName)
		{
			if (quest == null || string.IsNullOrWhiteSpace(mapClassName))
				return false;

			if (string.Equals(quest.Data.Location, mapClassName, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(quest.Data.QuestGiverLocation, mapClassName, StringComparison.OrdinalIgnoreCase))
				return true;

			var questStaticData = quest.QuestStaticData;
			return questStaticData != null &&
				(string.Equals(questStaticData.StartMap, mapClassName, StringComparison.OrdinalIgnoreCase) ||
				 string.Equals(questStaticData.ProgMap, mapClassName, StringComparison.OrdinalIgnoreCase) ||
				 string.Equals(questStaticData.EndMap, mapClassName, StringComparison.OrdinalIgnoreCase));
		}

		private void SetQuestCheckSlot(int slot, int questId)
		{
			var propertyName = $"QuestCheck_{slot}";
			if (!PropertyTable.Exists(this.Character.Etc.Properties.Namespace, propertyName))
				return;

			this.Character.SetEtcProperty(propertyName, questId);
		}

		/// <summary>
		/// Repairs the official Papaya early main quest handoff when a
		/// character loads into a map with stale static quest state.
		/// </summary>
		public bool RepairPapayaMainQuestFlow()
		{
			if (this.Character?.Connection == null || this.Character.Map == null)
				return false;

			var mapClassName = this.Character.Map.ClassName;
			var changed = false;
			changed |= this.CompletePapayaKlaipedaHandoffRoadQuests(mapClassName);
			changed |= this.SuppressOutOfSequencePapayaCapturedMainQuestState();
			changed |= this.SuppressOutOfSequencePapayaAutoMainQuestState();
			changed |= this.RepairPapayaGelePlateauImminentInvasion(mapClassName);
			changed |= this.RepairPapayaCapturedMainQuestChain();
			changed |= this.RepairPapayaMapTransitionQuestHandoffs(mapClassName);
			changed |= this.RepairPapayaCrystalMineSkipAfterMinersVillage(mapClassName);

			if (string.Equals(mapClassName, "c_Klaipe", StringComparison.OrdinalIgnoreCase))
				changed |= this.RepairPapayaKlaipedaMainQuestFlow();

			if (string.Equals(mapClassName, "f_siauliai_2", StringComparison.OrdinalIgnoreCase))
			{
				changed |= this.RepairPapayaEastSiauliaiMainQuestFlow();
				changed |= this.RepairPapayaEastSiauliaiCompletedAutoTrackStalls();
			}

			if (changed)
			{
				this.SyncStaticQuestNpcStates();
				this.SyncTrackedQuestSlots();
				this.UpdateClient();
			}

			return changed;
		}

		public bool RepairPapayaPreLoginMapState()
		{
			if (this.Character == null)
				return false;

			const int tenetB1MapId = 2085;
			const int tenet1FMapId = 2086;
			const int beyondDarknessQuestId = 8527;
			const int churchGateQuestId = 8510;

			if (this.Character.MapId != tenetB1MapId)
				return false;

			var shouldRouteToTenet1F = false;
			if (this.TryGetById(beyondDarknessQuestId, out var beyondDarknessQuest))
			{
				if (beyondDarknessQuest.Status < QuestStatus.Completed)
				{
					beyondDarknessQuest.CompleteObjectives();
					beyondDarknessQuest.Status = QuestStatus.Completed;
					beyondDarknessQuest.CompleteTime = DateTime.Now;
					beyondDarknessQuest.Tracked = false;
					shouldRouteToTenet1F = true;
				}
				else if (!this.Has(new QuestId(churchGateQuestId)))
				{
					shouldRouteToTenet1F = true;
				}
			}

			if (!shouldRouteToTenet1F)
				return false;

			for (var i = 0; i < MaxClientQuestCheckProperties; i++)
				this.SetQuestCheckSlot(i, 0);

			this.Character.MapId = tenet1FMapId;
			this.Character.Position = new Position(746, -79, -251);
			Log.Info("Papaya main quest flow: pre-login routed '{0}' past CHAPLE575_MQ_09 to Tenet Church 1F to avoid the client-native d_chapel57_5_tp04 load crash.", this.Character.Name);
			return true;
		}

		private bool RepairPapayaGelePlateauImminentInvasion(string mapClassName)
		{
			if (!string.Equals(mapClassName, "f_gele_57_2", StringComparison.OrdinalIgnoreCase))
				return false;

			if (!ZoneServer.Instance.Data.QuestDb.TryFind("GELE572_MQ_01", out var questData) ||
				!this.TryGetById(new QuestId(questData.Id), out var quest))
				return false;

			return this.TryCompletePapayaGelePlateauImminentInvasion(quest, mapClassName, "map-entry repair");
		}

		private bool TryCompletePapayaGelePlateauImminentInvasion(Quest quest, string mapClassName, string reason)
		{
			if (quest?.QuestStaticData == null ||
				!string.Equals(quest.QuestStaticData.ClassName, "GELE572_MQ_01", StringComparison.OrdinalIgnoreCase) ||
				!string.Equals(mapClassName, "f_gele_57_2", StringComparison.OrdinalIgnoreCase))
				return false;

			if (!quest.InProgress && quest.Status != QuestStatus.Success)
				return false;

			if (this.Character.Tracks.ActiveTrack?.Id == "GELE572_MQ_01_TRACK")
				this.Character.Tracks.Cancel();

			Log.Info("Papaya main quest flow: completing GELE572_MQ_01 for '{0}' on Gele Plateau via {1}; the client-native track is not reliable here.", this.Character.Name, reason);
			quest.CompleteObjectives();
			this.Complete(quest);
			return true;
		}

		private bool RepairPapayaCapturedMainQuestChain()
		{
			var changed = false;
			changed |= this.CompleteSucceededClientHiddenPapayaBridgeQuests();

			for (var i = 0; i < PapayaCapturedMainQuestOrder.Length - 1; i++)
			{
				if (!ZoneServer.Instance.Data.QuestDb.TryFind(PapayaCapturedMainQuestOrder[i], out var completedQuestData) ||
					!ZoneServer.Instance.Data.QuestDb.TryFind(PapayaCapturedMainQuestOrder[i + 1], out var nextQuestData))
					continue;

				changed |= this.RepairPapayaCapturedMainQuestStep(
					completedQuestData.ClassName,
					nextQuestData.ClassName,
					this.PapayaCapturedFollowUpShouldStartImmediately(completedQuestData, nextQuestData));
			}

			return changed;
		}

		private bool CompleteSucceededClientHiddenPapayaBridgeQuests()
		{
			var quests = this.GetList()
				.Where(quest =>
					quest.Status == QuestStatus.Success &&
					this.StaticQuestIsClientHiddenPapayaBridge(quest.QuestStaticData))
				.ToList();

			if (quests.Count == 0)
				return false;

			foreach (var quest in quests)
			{
				Log.Info("Papaya main quest flow: auto-completing hidden bridge quest '{0}' for '{1}' to continue the visible chain.", quest.QuestStaticData.ClassName, this.Character.Name);
				this.Complete(quest);
			}

			return true;
		}

		private bool PapayaCapturedFollowUpShouldStartImmediately(QuestStaticData completedQuestData, QuestStaticData nextQuestData)
		{
			if (nextQuestData == null)
				return false;

			if (this.PapayaCapturedFollowUpMustStartImmediately(completedQuestData, nextQuestData))
				return true;

			if (!string.Equals(nextQuestData.QuestStartMode, "NPCDIALOG", StringComparison.OrdinalIgnoreCase))
				return true;

			return false;
		}

		private bool PapayaCapturedFollowUpMustStartImmediately(QuestStaticData completedQuestData, QuestStaticData nextQuestData)
		{
			return false;
		}

		private bool RepairPapayaCapturedMainQuestStep(string completedQuestName, string nextQuestName, bool startImmediately)
		{
			if (!ZoneServer.Instance.Data.QuestDb.TryFind(completedQuestName, out var completedQuestData) ||
				!ZoneServer.Instance.Data.QuestDb.TryFind(nextQuestName, out var nextQuestData))
				return false;

			if (!this.HasCompleted(completedQuestData.Id) || this.Has(new QuestId(nextQuestData.Id)))
				return false;

			if (!this.MeetsStaticPrerequisites(nextQuestData))
				return false;

			if (startImmediately ||
				string.Equals(nextQuestData.QuestStartMode, "SYSTEM", StringComparison.OrdinalIgnoreCase) ||
				this.StaticMainFollowUpShouldStartImmediately(completedQuestData, nextQuestData))
			{
				this.StartStaticQuest(nextQuestData, TimeSpan.Zero);
				this.TrackPapayaMainFollowUpIfVisible(nextQuestData);
				Log.Info("Papaya main quest flow: started captured follow-up '{0}' after '{1}' for '{2}'.", nextQuestName, completedQuestName, this.Character.Name);
				return true;
			}

			if (!this.StaticQuestReferencesMap(nextQuestData, this.Character.Map.ClassName))
				return false;

			var changed = this.ShouldKeepPapayaCapturedFollowUpPossible(completedQuestData, nextQuestData)
				? this.EnsureStaticQuestPossible(nextQuestData, true)
				: this.SetStaticQuestProperty(nextQuestData, QuestStatus.Possible);
			if (changed)
				Log.Info("Papaya main quest flow: made captured follow-up '{0}' available after '{1}' for '{2}'.", nextQuestName, completedQuestName, this.Character.Name);

			return changed;
		}

		private void TrackPapayaMainFollowUpIfVisible(QuestStaticData questData)
		{
			if (questData == null ||
				!string.Equals(questData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase) ||
				string.IsNullOrWhiteSpace(questData.ClassName))
				return;

			this.TrackQuestInClientSlot(questData.ClassName);
		}

		private bool ShouldKeepPapayaCapturedFollowUpPossible(QuestStaticData completedQuestData, QuestStaticData nextQuestData)
		{
			return completedQuestData != null &&
				nextQuestData != null &&
				(
					(
						string.Equals(completedQuestData.ClassName, "EAST_PREPARE_1", StringComparison.OrdinalIgnoreCase) &&
						string.Equals(nextQuestData.ClassName, "SIAUL_EAST_RECLAIM1", StringComparison.OrdinalIgnoreCase)
					) ||
					string.Equals(nextQuestData.ClassName, "GELE573_MQ_09", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(nextQuestData.ClassName, "GELE573_MQ_08", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(nextQuestData.ClassName, "GELE574_MQ_09", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(nextQuestData.ClassName, "CHAPLE575_MQ_04", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(nextQuestData.ClassName, "CHAPLE575_MQ_09", StringComparison.OrdinalIgnoreCase)
				);
		}

		private bool EnsureStaticQuestPossible(QuestStaticData questData, bool track)
		{
			if (questData == null ||
				this.StaticQuestDisabledForCloverFlow(questData) ||
				this.StaticQuestIsBlockedByPapayaMainProgression(questData) ||
				!this.MeetsStaticPrerequisites(questData))
				return false;

			var questId = new QuestId(questData.Id);
			if (this.TryGetById(questId, out var existingQuest))
			{
				if (!existingQuest.IsPossible)
					return false;

				if (track)
					existingQuest.Tracked = true;

				this.SetStaticQuestProperty(questData, QuestStatus.Possible);
				this.SyncStaticQuestSessionObject(existingQuest);
				this.UpdateClient_UpdateQuest(existingQuest);
				this.SyncTrackedQuestSlots();
				return true;
			}

			var quest = this.CreateStaticQuest(questData);
			quest.Status = QuestStatus.Possible;
			quest.StartTime = DateTime.Now;
			quest.Tracked = track;
			this.AddSilent(quest);
			this.SetStaticQuestProperty(questData, QuestStatus.Possible);
			this.SyncStaticQuestSessionObject(quest);
			this.UpdateClient_AddQuest(quest);
			this.SyncTrackedQuestSlots();
			this.SyncStaticQuestNpcStatesAfterDialog();
			return true;
		}

		private bool RepairPapayaKlaipedaMainQuestFlow()
		{
			var changed = false;

			changed |= this.SuppressPrematureKlaipedaHandoffBeforeWestRoad();
			changed |= this.CompleteSkippedWestSiauliaiRoadAfterKlaipedaArrival();

			if (this.HasCompleted(1019) &&
				!this.Has(new QuestId(1027)) &&
				ZoneServer.Instance.Data.QuestDb.TryFind("KLAPEDA_GO_TO_EAST", out var goToEast) &&
				this.StaticQuestReferencesMap(goToEast, this.Character.Map.ClassName) &&
				this.MeetsStaticPrerequisites(goToEast))
			{
				this.StartStaticQuest(goToEast, TimeSpan.Zero);
				Log.Info("Papaya main quest flow: started Klaipeda handoff '{0}' for '{1}' on map entry.", goToEast.ClassName, this.Character.Name);
				changed = true;
			}

			if (this.HasCompleted(1027) && !this.Has(20236))
			{
				if (ZoneServer.Instance.Data.QuestDb.TryFind("EAST_PREPARE", out var eastPrepare) &&
					this.StaticQuestReferencesMap(eastPrepare, this.Character.Map.ClassName))
					changed |= this.SetStaticQuestProperty(eastPrepare, QuestStatus.Possible);
			}

			if (this.HasCompleted(20236) && !this.Has(40010))
			{
				if (ZoneServer.Instance.Data.QuestDb.TryFind("EAST_PREPARE_1", out var eastPrepare1) &&
					this.MeetsStaticPrerequisites(eastPrepare1))
					changed |= this.SetStaticQuestProperty(eastPrepare1, QuestStatus.Possible);
			}

			changed |= this.RepairPrematureEastSiauliaiReclaimAfterKlaipedaPreparation();

			if (this.TryGetById(1027, out var goToEastQuest) &&
				(goToEastQuest.InProgress || goToEastQuest.Status == QuestStatus.Success))
			{
				this.TrackQuestInClientSlot("KLAPEDA_GO_TO_EAST");
				changed = true;
			}
			else if (this.TryGetById(20236, out var bishopDream) && bishopDream.InProgress)
			{
				this.TrackQuestInClientSlot("EAST_PREPARE");
				changed = true;
			}
			else if (this.TryGetById(40010, out var bishopDream2) && bishopDream2.InProgress)
			{
				this.TrackQuestInClientSlot("EAST_PREPARE_1");
				changed = true;
			}
			else if (this.TryGetById(1032, out var eastReclaim1) && eastReclaim1.IsPossible)
			{
				this.TrackQuestInClientSlot("SIAUL_EAST_RECLAIM1");
				changed = true;
			}

			return changed;
		}

		private bool RepairPrematureEastSiauliaiReclaimAfterKlaipedaPreparation()
		{
			if (!this.HasCompleted(40010) ||
				!ZoneServer.Instance.Data.QuestDb.TryFind("SIAUL_EAST_RECLAIM1", out var eastReclaimData) ||
				!this.TryGetById(eastReclaimData.Id, out var eastReclaimQuest) ||
				!eastReclaimQuest.InProgress)
				return false;

			if (eastReclaimQuest.Progresses.Any(progress => progress.Done || progress.Count > 0))
				return false;

			eastReclaimQuest.Status = QuestStatus.Possible;
			eastReclaimQuest.Tracked = true;
			this.SetStaticQuestProperty(eastReclaimData, QuestStatus.Possible);
			this.SyncStaticQuestSessionObject(eastReclaimQuest);
			this.UpdateClient_UpdateQuest(eastReclaimQuest);
			this.TrackQuestInClientSlot(eastReclaimData.ClassName);
			this.SyncStaticQuestNpcStatesAfterDialog();
			Log.Info("Papaya main quest flow: restored prematurely-started '{0}' to possible state for '{1}' until Aras is contacted.", eastReclaimData.ClassName, this.Character.Name);
			return true;
		}

		private bool CompleteSkippedWestSiauliaiRoadAfterKlaipedaArrival()
		{
			if (!this.HasCompleted(1015) || this.HasCompleted(1019))
				return false;

			var changed = false;
			changed |= this.CompleteStaticQuestForPapayaFlow(1018, "SIAUL_WEST_LAIMONAS4");
			changed |= this.CompleteStaticQuestForPapayaFlow(1019, "SIAUL_WEST_WOOD_SPIRIT");

			if (changed)
				Log.Info("Papaya main quest flow: repaired skipped West Siauliai road handoff for '{0}' after Klaipeda arrival.", this.Character.Name);

			return changed;
		}

		private bool SuppressPrematureKlaipedaHandoffBeforeWestRoad()
		{
			if (this.HasCompleted(1019))
				return false;

			var changed = false;
			foreach (var questClassName in new[] { "KLAPEDA_GO_TO_EAST", "EAST_PREPARE", "EAST_PREPARE_1" })
			{
				if (!ZoneServer.Instance.Data.QuestDb.TryFind(questClassName, out var questData))
					continue;

				if (this.TryGetById(questData.Id, out var quest))
				{
					quest.Tracked = false;
					this.UpdateClient_RemoveQuest(quest);
					lock (_syncLock)
						_quests.Remove(quest);
					changed = true;
					Log.Info("Papaya main quest flow: removed premature Klaipeda handoff quest '{0}' for '{1}' until Road to Klaipeda is complete.", questClassName, this.Character.Name);
				}

				changed |= this.RemoveStaticQuestSessionObject(questData);
				changed |= this.SetStaticQuestProperty(questData, QuestStatus.Impossible);
				this.RemoveStaticQuestFromClientList(questData.Id, questData.ClassName);
			}

			return changed;
		}

		private bool CompletePapayaKlaipedaHandoffRoadQuests(string mapClassName)
		{
			if (this.IsWestSiauliaiMap(mapClassName))
				return false;

			if (!this.HasCompleted(1019))
				return false;

			var changed = false;
			foreach (var questId in new[] { new QuestId(1018), new QuestId(1019) })
			{
				if (!this.TryGetById(questId, out var quest))
					continue;

				if (quest.Status == QuestStatus.Completed || quest.Status == QuestStatus.Abandoned)
				{
					changed |= this.SuppressCompletedKlaipedaRoadQuestClientState(quest, mapClassName);
					continue;
				}
			}

			return changed;
		}

		private bool SuppressCompletedKlaipedaRoadQuestClientState(Quest quest, string mapClassName)
		{
			if (quest == null || this.Character?.Connection == null || this.IsWestSiauliaiMap(mapClassName))
				return false;

			var tempKey = $"Papaya.KlaipedaRoadClientCleanup.{quest.Data.Id.Value}.{mapClassName}";
			if (this.Character.Variables.Temp.GetBool(tempKey, false) && !quest.Tracked)
				return false;

			quest.Tracked = false;
			this.UpdateClient_RemoveQuest(quest);
			this.SyncTrackedQuestSlots();
			this.Character.Variables.Temp.SetBool(tempKey, true);
			Log.Info("Papaya main quest flow: suppressed completed road handoff quest '{0}' from '{1}' client on map '{2}'.", quest.QuestStaticData?.ClassName ?? quest.Data.Id.ToString(), this.Character.Name, mapClassName);
			return true;
		}

		private bool RepairPapayaEastSiauliaiMainQuestFlow()
		{
			if (!this.HasCompleted(40010) || this.Has(1032))
				return false;

			if (!ZoneServer.Instance.Data.QuestDb.TryFind("SIAUL_EAST_RECLAIM1", out var questData))
				return false;

			if (!this.MeetsStaticPrerequisites(questData))
				return false;

			this.StartStaticQuest(questData, TimeSpan.Zero);
			this.SyncStaticQuestNpcStates();
			Log.Info("Papaya main quest flow: started '{0}' for '{1}' on East Siauliai Woods repair.", questData.ClassName, this.Character.Name);
			return true;
		}

		private bool RepairPapayaEastSiauliaiCompletedAutoTrackStalls()
		{
			var changed = false;
			foreach (var questName in new[] { "SIAUL_EAST_REQUEST6", "SIAUL_EAST_REQUEST7" })
			{
				if (!ZoneServer.Instance.Data.QuestDb.TryFind(questName, out var questData) ||
					!this.TryGetById(questData.Id, out var quest) ||
					!quest.InProgress ||
					(questData.Objectives != null && questData.Objectives.Count != 0))
					continue;

				var questAutoData = ZoneServer.Instance.Data.QuestAutoDb.Find(questName);
				if (!this.TryParseStaticQuestAutoTrack(questAutoData, out var track))
					continue;

				if (!PropertyTable.Exists(this.Character.Etc.Properties.Namespace, track.PropertyName) ||
					this.Character.Etc.Properties.GetFloat(track.PropertyName) != 1)
					continue;

				quest.Status = QuestStatus.Success;
				quest.Tracked = true;
				this.SetStaticQuestProperty(questData, QuestStatus.Success);
				this.SyncStaticQuestSessionObject(quest);
				this.UpdateClient_UpdateQuest(quest);
				Log.Info("Papaya main quest flow: repaired completed auto track stall for '{0}' on '{1}'.", questName, this.Character.Name);
				changed = true;
			}

			return changed;
		}

		private bool RepairPapayaMapTransitionQuestHandoffs(string mapClassName)
		{
			if (string.IsNullOrWhiteSpace(mapClassName))
				return false;

			var changed = false;
			var quests = this.GetList()
				.Where(quest =>
					quest.InProgress &&
					quest.QuestStaticData != null &&
					string.Equals(quest.QuestStaticData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase))
				.ToList();

			foreach (var quest in quests)
			{
				if (!this.TryResolvePapayaMapTransitionSuccessNext(quest, mapClassName, out var nextQuestData))
					continue;

				Log.Info(
					"Papaya map handoff: completing transition quest '{0}' for '{1}' after arrival on '{2}', next '{3}'.",
					quest.QuestStaticData.ClassName,
					this.Character.Name,
					mapClassName,
					nextQuestData.ClassName
				);
				this.Complete(quest);
				changed = true;
			}

			return changed;
		}

		private bool TryResolvePapayaMapTransitionSuccessNext(Quest quest, string mapClassName, out QuestStaticData nextQuestData)
		{
			nextQuestData = null;

			var questData = quest?.QuestStaticData;
			if (questData == null ||
				string.IsNullOrWhiteSpace(mapClassName) ||
				!quest.InProgress ||
				this.StaticQuestReferencesMap(questData, mapClassName))
				return false;

			if (questData.Objectives != null && questData.Objectives.Count != 0 && !quest.ObjectivesCompleted)
				return false;

			var questAutoData = ZoneServer.Instance.Data.QuestAutoDb.Find(questData.ClassName);
			if (questAutoData?.SuccessNextQuestNames == null)
				return false;

			foreach (var nextQuestName in questAutoData.SuccessNextQuestNames)
			{
				if (this.IsNone(nextQuestName) ||
					!ZoneServer.Instance.Data.QuestDb.TryFind(nextQuestName, out var candidate) ||
					this.StaticQuestDisabledForCloverFlow(candidate) ||
					!this.StaticQuestReferencesMap(candidate, mapClassName) ||
					this.Has(new QuestId(candidate.Id)))
					continue;

				nextQuestData = candidate;
				return true;
			}

			return false;
		}

		private void QueueDelayedNativeQuestTrackerRefresh(QuestId questId)
		{
			this.QueueDelayedNativeQuestTrackerRefresh(questId, 650);
			this.QueueDelayedNativeQuestTrackerRefresh(questId, 1500);
		}

		private void QueueDelayedNativeQuestTrackerRefresh(QuestId questId, int delayMilliseconds)
		{
			_ = Task.Run(async () =>
			{
				await Task.Delay(delayMilliseconds);

				if (this.Character?.Connection == null)
					return;

				if (!this.TryGetById(questId, out var quest) || !(quest.InProgress || quest.Status == QuestStatus.Success))
					return;

				this.SyncStaticQuestNpcStates();
				this.SyncTrackedQuestSlots();
				this.NotifyNativeQuestTracking(quest, false);
			});
		}

		public bool ShouldDeferStaticNpcDialogAdvance(string npcDialogName)
		{
			if (string.IsNullOrWhiteSpace(npcDialogName) || !this.StaticNpcDialogCanCrashWhenAdvancedInDialog(npcDialogName))
				return false;

			foreach (var quest in this.GetList())
			{
				var questData = quest.QuestStaticData;
				if (questData == null)
					continue;

				if (quest.InProgress &&
					quest.ObjectivesCompleted &&
					this.StaticQuestShouldCompleteFromNpcDialog(questData, npcDialogName))
					return true;

				if (quest.InProgress &&
					quest.TryGetProgress("manual", out var progress) &&
					!progress.Done &&
					this.StaticQuestCanAdvanceFromNpcDialog(questData, npcDialogName))
					return true;

				if (quest.InProgress &&
					this.StaticQuestCanAdvanceFromNpcDialog(questData, npcDialogName) &&
					this.StaticQuestHasAutoTrackForCurrentStatus(quest))
					return true;

				if (quest.Status == QuestStatus.Success &&
					this.StaticQuestShouldCompleteFromNpcDialog(questData, npcDialogName))
					return true;
			}

			return ZoneServer.Instance.Data.QuestDb.GetList()
				.OrderBy(a => a.Id)
				.Any(a =>
					string.Equals(a.QuestStartMode, "NPCDIALOG", StringComparison.OrdinalIgnoreCase) &&
					!this.StaticQuestDisabledForCloverFlow(a) &&
					this.StaticQuestCanStartFromNpcDialog(a, npcDialogName) &&
					!this.StaticQuestIsBlockedByPriorityQuest(a, npcDialogName) &&
					!this.StaticQuestIsBlockedByEarlierWestSiauliaiMain(a) &&
					!this.StaticQuestIsBlockedByPapayaMainProgression(a) &&
					!this.Has(new QuestId(a.Id)) &&
					this.MeetsStaticPrerequisites(a));
		}

		private bool StaticNpcDialogCanCrashWhenAdvancedInDialog(string npcDialogName)
		{
			var mapClassName = this.Character?.Map?.ClassName;
			if (string.IsNullOrWhiteSpace(mapClassName))
				return false;

			return true;
		}

		private bool CompleteStaticQuestForPapayaFlow(int questId, string questClassName)
		{
			if (this.HasCompleted(questId))
				return false;

			if (!this.TryGetById(questId, out var quest))
			{
				this.EnsureStaticQuestInProgress(questClassName);
				this.TryGetById(questId, out quest);
			}

			if (quest == null || quest.Status == QuestStatus.Completed || quest.Status == QuestStatus.Abandoned)
				return false;

			quest.CompleteObjectives();
			this.Complete(quest);
			Log.Info("Papaya main quest flow: completed handoff quest '{0}' for '{1}'.", questClassName, this.Character.Name);
			return true;
		}

		private bool RepairPapayaCrystalMineSkipAfterMinersVillage(string mapClassName)
		{
			if (_papayaCrystalMineSkipInProgress)
				return false;

			if (!this.HasCompleted(8080))
				return false;

			_papayaCrystalMineSkipInProgress = true;
			try
			{
				var changed = false;

				foreach (var questName in PapayaMinersVillageOptionalCleanupQuestNames)
					changed |= this.CompleteActiveStaticQuestForPapayaSkip(questName);

				foreach (var questName in PapayaCrystalMineSkipQuestNames)
					changed |= this.CompleteStaticQuestForPapayaSkip(questName);

				changed |= this.EnsurePapayaPostCrystalMineNextChain();

				if (changed)
				{
					Log.Info(
						"Papaya main quest flow: skipped Crystal Mine chain for '{0}' after Miners' Village; next chain is GELE572_MQ_01.",
						this.Character.Name
					);
				}

				return changed;
			}
			finally
			{
				_papayaCrystalMineSkipInProgress = false;
			}
		}

		private bool CompleteActiveStaticQuestForPapayaSkip(string questClassName)
		{
			if (!ZoneServer.Instance.Data.QuestDb.TryFind(questClassName, out var questData) ||
				!this.TryGetById(questData.Id, out var quest) ||
				quest.Status == QuestStatus.Completed ||
				quest.Status == QuestStatus.Abandoned)
				return false;

			quest.Tracked = false;
			quest.CompleteObjectives();
			this.Complete(quest);
			Log.Info("Papaya main quest flow: completed active optional quest '{0}' while skipping Miners' Village side combat.", questClassName);
			return true;
		}

		private bool CompleteStaticQuestForPapayaSkip(string questClassName)
		{
			if (!ZoneServer.Instance.Data.QuestDb.TryFind(questClassName, out var questData))
				return false;

			if (this.HasCompleted(questData.Id))
				return false;

			var questId = new QuestId(questData.Id);
			if (!this.TryGetById(questId, out var quest))
			{
				quest = this.CreateStaticQuest(questData);
				quest.Status = QuestStatus.InProgress;
				quest.StartTime = DateTime.Now;
				quest.Tracked = false;
				quest.UpdateUnlock();
				this.AddSilent(quest);
			}
			else if (quest.Status == QuestStatus.Completed || quest.Status == QuestStatus.Abandoned)
			{
				return false;
			}
			else
			{
				quest.Status = QuestStatus.InProgress;
				quest.Tracked = false;
				if (quest.StartTime == DateTime.MinValue)
					quest.StartTime = DateTime.Now;
				quest.UpdateUnlock();
			}

			quest.CompleteObjectives();
			this.Complete(quest);
			Log.Info("Papaya main quest flow: auto-completed skipped Crystal Mine quest '{0}' for '{1}'.", questClassName, this.Character.Name);
			return true;
		}

		private bool EnsurePapayaPostCrystalMineNextChain()
		{
			if (!ZoneServer.Instance.Data.QuestDb.TryFind("GELE572_MQ_01", out var questData) ||
				this.Has(new QuestId(questData.Id)) ||
				this.HasCompleted(questData.Id) ||
				!this.MeetsStaticPrerequisites(questData))
				return false;

			var changed = this.EnsureStaticQuestPossible(questData, true);
			if (changed)
				this.TrackQuestInClientSlot(questData.ClassName);

			return changed;
		}

		/// <summary>
		/// Calls OnStart on the quest's objectives to go through the
		/// potential initial checks for whether the objective was
		/// possibly already completed.
		/// </summary>
		/// <param name="quest"></param>
		private void InitialChecks(Quest quest)
		{
			var checkedTypes = new HashSet<Type>();

			foreach (var objective in quest.Data.Objectives)
			{
				// Check every objective type only once, as they're designed
				// to check all of the quest's objectives at once.
				var type = objective.GetType();
				if (checkedTypes.Contains(type))
					continue;

				objective.OnStart(this.Character, quest);
				checkedTypes.Add(type);
			}
		}

		/// <summary>
		/// Iterates over the quests' objectives, runs the given function
		/// over all objectives with the given type, and updates the quest
		/// if any progresses changed.
		/// </summary>
		/// <typeparam name="TObjective"></typeparam>
		/// <param name="updater"></param>
		public void UpdateObjectives<TObjective>(QuestObjectivesUpdateFunc<TObjective> updater) where TObjective : QuestObjective
		{
			lock (_syncLock)
			{
				foreach (var quest in _quests.ToList())
				{
					if (!_quests.Contains(quest))
						continue;

					if (quest.Status != QuestStatus.InProgress && quest.Status != QuestStatus.Success)
						continue;

					quest.UpdateObjectives(updater);

					if (!_quests.Contains(quest))
						continue;

					if (quest.ChangesOnLastUpdate)
					{
						quest.UpdateUnlock();

						if (quest.Status == QuestStatus.Success && !quest.IsCompletable)
							quest.Status = QuestStatus.InProgress;
						else if (quest.Status == QuestStatus.InProgress && quest.IsCompletable)
							quest.Status = QuestStatus.Success;

						this.UpdateClient_UpdateQuest(quest);
					}
				}
			}

			this.SyncTrackedQuestSlots();
		}

		/// <summary>
		/// Iterates over the quests' modifiers, runs the given function
		/// over all modifiers with the given type, and updates the quest
		/// if any progresses changed.
		/// </summary>
		/// <typeparam name="TModifier"></typeparam>
		/// <param name="updater"></param>
		public void UpdateModifiers<TModifier>(QuestModifiersUpdateFunc<TModifier> updater) where TModifier : QuestModifier
		{
			lock (_syncLock)
			{
				foreach (var quest in _quests.ToList())
				{
					if (!_quests.Contains(quest))
						continue;

					if (quest.Status != QuestStatus.InProgress)
						continue;

					quest.UpdateModifiers(updater);

					if (!_quests.Contains(quest))
						continue;

					if (quest.ChangesOnLastUpdate)
					{
						quest.UpdateUnlock();
					}
				}
			}
		}

		/// <summary>
		/// Starts a quest using dynamically generated QuestData and associates
		/// it with the generator script instance for callbacks.
		/// </summary>
		/// <param name="generatedData">The dynamically created QuestData.</param>
		/// <param name="generatorInstance">The QuestScript instance that generated this quest.</param>
		/// <param name="delay">Optional delay before the quest becomes active.</param>
		/// <returns></returns>
		public YieldAwaitable StartGeneratedQuest(QuestData generatedData, QuestScript generatorInstance, TimeSpan delay = default)
		{
			if (generatedData == null)
				throw new ArgumentNullException(nameof(generatedData));
			if (generatorInstance == null)
				throw new ArgumentNullException(nameof(generatorInstance));
			if (generatedData.Id == QuestId.Zero)
				throw new ArgumentException("Generated QuestData must have a valid unique QuestId.", nameof(generatedData));

			// Ensure no duplicate active quest with the same *generated* ID (important!)
			lock (_syncLock)
			{
				if (_quests.Any(q => q.Data.Id == generatedData.Id && q.Status >= QuestStatus.Possible))
				{
					// Log error or handle gracefully - shouldn't start the same generated instance twice.
					Yggdrasil.Logging.Log.Warning($"Attempted to start generated quest {generatedData.Id} which already exists or is pending for character {Character.Name}.");
					return Task.Yield(); // Or throw exception
				}
			}

			delay = Math2.Max(TimeSpan.Zero, delay);

			// Use the new constructor or SetGenerator method
			var quest = new Quest(generatedData, generatorInstance);
			// quest.SetGenerator(generatorInstance); // Alternative if not using constructor

			// Add the quest silently first
			lock (_syncLock)
			{
				_quests.Add(quest); // Add it to the list
			}

			// Handle delay or immediate start
			if (delay == TimeSpan.Zero)
			{
				// Call the internal Start method which handles objectives, status, callbacks, and client updates
				this.Start(quest);
			}
			else
			{
				quest.Status = QuestStatus.Possible; // Mark as possible but not started
				quest.StartTime = DateTime.Now.Add(delay);
				// No client update needed yet, the Update() loop will handle starting it.
			}

			return Task.Yield();
		}

		/// <summary>
		/// Starts quest for the character, returns false if the quest
		/// couldn't be started.
		/// </summary>
		/// <param name="questId"></param>
		/// <returns></returns>
		public YieldAwaitable Start(string questId)
		{
			if (!ZoneServer.Instance.Data.QuestDb.TryFind(questId, out var questData))
				throw new ArgumentException($"Unknown quest '{questId}'.");

			if (!this.TryGetNamespacedQuestId(questData.Id, out var namespacedQuestId) ||
				(!QuestScript.Exists(namespacedQuestId) && !QuestScript.Exists(new QuestId(questData.Id))))
				return this.StartStaticQuest(questData, TimeSpan.Zero);

			return this.Start(namespacedQuestId, TimeSpan.Zero);
		}

		/// <summary>
		/// Ensures a static quest from quests.txt is actively visible to
		/// the client, promoting pending restored states when necessary.
		/// </summary>
		public YieldAwaitable EnsureStaticQuestInProgress(string questClassName)
		{
			if (!ZoneServer.Instance.Data.QuestDb.TryFind(questClassName, out var questData))
				throw new ArgumentException($"Unknown quest '{questClassName}'.");

			var questId = new QuestId(questData.Id);
			if (this.HasCompleted(questId))
				return Task.Yield();

			if (this.TryGetById(questId, out var quest))
			{
				if (!quest.InProgress)
				{
					quest.Status = QuestStatus.InProgress;
					if (quest.StartTime == DateTime.MinValue)
						quest.StartTime = DateTime.Now;
					quest.UpdateUnlock();
					this.EnsureStaticQuestLayerState(quest);
					this.UpdateClient_AddQuest(quest);
				}

				this.SyncStaticQuestSessionObject(quest);
				this.SyncTrackedQuestSlots();
				this.SyncStaticQuestNpcStatesAfterDialog();
				return Task.Yield();
			}

			return this.StartStaticQuest(questData, TimeSpan.Zero);
		}

		/// <summary>
		/// Starts quest for the character, returns false if the quest
		/// couldn't be started.
		/// </summary>
		/// <param name="questId"></param>
		/// <returns></returns>
		public YieldAwaitable Start(QuestId questId)
			=> this.Start(questId, TimeSpan.Zero);

		/// <summary>
		/// Adds quest and starts it after the given delay.
		/// </summary>
		/// <param name="questId"></param>
		/// <param name="delay"></param>
		/// <returns></returns>
		public YieldAwaitable Start(QuestId questId, TimeSpan delay)
		{
			delay = Math2.Max(TimeSpan.Zero, delay);

			if (!QuestScript.TryGet(questId, out _)
				&& ZoneServer.Instance.Data.QuestDb.TryFind((int)questId.Value, out var staticQuestData))
			{
				return this.StartStaticQuest(staticQuestData, delay);
			}

			// Check prerequisites before starting the quest
			if (!this.MeetsPrerequisites(questId))
			{
				Log.Warning($"Character '{this.Character.Name}' attempted to start quest '{questId}' without meeting prerequisites.");
				return Task.Yield();
			}

			var quest = Quest.Create(questId);

			if (delay == TimeSpan.Zero)
			{
				this.Start(quest);
				this.AddSilent(quest);
				this.EnsureStaticQuestLayerState(quest);
			}
			else
			{
				quest.StartTime = DateTime.Now.Add(delay);
				this.AddSilent(quest);
			}
			return Task.Yield();
		}

		private YieldAwaitable StartStaticQuest(QuestStaticData questStaticData, TimeSpan delay)
		{
			delay = Math2.Max(TimeSpan.Zero, delay);

			if (this.StaticQuestDisabledForCloverFlow(questStaticData))
				return Task.Yield();

			if (this.StaticQuestIsBlockedByEarlierWestSiauliaiMain(questStaticData))
			{
				Log.Warning($"Character '{this.Character.Name}' attempted to start static quest '{questStaticData.ClassName}' before the earlier West Siauliai main quest chain was complete.");
				return Task.Yield();
			}

			if (this.StaticQuestIsBlockedByPapayaMainProgression(questStaticData))
			{
				Log.Warning($"Character '{this.Character.Name}' attempted to start static quest '{questStaticData.ClassName}' before the Papaya main quest progression reached it.");
				return Task.Yield();
			}

			var questId = new QuestId(questStaticData.Id);
			if (this.Has(questId))
				return Task.Yield();

			if (!this.MeetsStaticPrerequisites(questStaticData))
			{
				Log.Warning($"Character '{this.Character.Name}' attempted to start static quest '{questStaticData.ClassName}' without meeting prerequisites.");
				return Task.Yield();
			}

			var quest = this.CreateStaticQuest(questStaticData);

			if (delay == TimeSpan.Zero)
			{
				this.Start(quest);
				this.AddSilent(quest);
				this.TryAutoCompleteStaticQuestImmediatelyOnStart(quest);
			}
			else
			{
				quest.StartTime = DateTime.Now.Add(delay);
				this.AddSilent(quest);
			}

			return Task.Yield();
		}

		private bool TryAutoCompleteStaticQuestImmediatelyOnStart(Quest quest)
		{
			if (quest?.QuestStaticData == null)
				return false;

			if (quest.Progresses.Count != 0 || !quest.ObjectivesCompleted)
				return false;

			if (!string.Equals(quest.QuestStaticData.QuestEndMode, "SYSTEM", StringComparison.OrdinalIgnoreCase) &&
				!this.StaticQuestIsClientHiddenPapayaBridge(quest.QuestStaticData))
				return false;

			if (this.StaticQuestHasAnyAutoTrack(quest.QuestStaticData))
				return false;

			Log.Info("Static quest chain: auto-completing no-objective SYSTEM quest '{0}' for '{1}' immediately after start.", quest.QuestStaticData.ClassName, this.Character.Name);
			this.Complete(quest);
			return true;
		}

		private bool StaticQuestHasAnyAutoTrack(QuestStaticData questData)
		{
			if (questData == null || string.IsNullOrWhiteSpace(questData.ClassName))
				return false;

			var questAutoData = ZoneServer.Instance.Data.QuestAutoDb.Find(questData.ClassName);
			return !string.IsNullOrWhiteSpace(questAutoData?.Track);
		}

		private bool StaticQuestDisabledForCloverFlow(QuestStaticData questStaticData)
		{
			if (questStaticData == null)
				return false;

			if (this.StaticQuestParkedFromMainFlow(questStaticData))
				return true;

			if (this.IsCloverDisabledStaticMainQuest(questStaticData))
				return true;

			if (!string.Equals(questStaticData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase) &&
				!this.IsPapayaCapturedMainQuest(questStaticData, out _) &&
				!this.StaticQuestIsMainPrerequisiteBridge(questStaticData))
				return true;

			if (this.IsWestSiauliaiOptionalQuestName(questStaticData.ClassName))
				return true;

			if (this.IsObsoletePapayaStaticQuest(questStaticData))
				return true;

			return false;
		}

		private bool IsCloverDisabledStaticMainQuest(QuestStaticData questStaticData)
		{
			var className = questStaticData?.ClassName;
			if (string.IsNullOrWhiteSpace(className))
				return false;

			if (string.Equals(className, "TUTO_SKILL_RUN", StringComparison.OrdinalIgnoreCase))
				return false;

			return className.StartsWith("TUTO_", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(className, "LEGEND_CARD_LIFT", StringComparison.OrdinalIgnoreCase) ||
				className.StartsWith("JOB_", StringComparison.OrdinalIgnoreCase) ||
				className.StartsWith("ASSISTOR_TUTO_", StringComparison.OrdinalIgnoreCase) ||
				className.StartsWith("TOSHERO_TUTO_", StringComparison.OrdinalIgnoreCase);
		}

		private bool StaticQuestIsMainPrerequisiteBridge(QuestStaticData questStaticData)
		{
			if (questStaticData == null || string.IsNullOrWhiteSpace(questStaticData.ClassName))
				return false;

			return this.GetMainPrerequisiteBridgeQuestNames().Contains(questStaticData.ClassName);
		}

		private HashSet<string> GetMainPrerequisiteBridgeQuestNames()
		{
			lock (MainPrerequisiteBridgeLock)
			{
				if (MainPrerequisiteBridgeQuestNames != null)
					return MainPrerequisiteBridgeQuestNames;

				var bridgeQuestNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				var visitedQuestNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				foreach (var questData in ZoneServer.Instance.Data.QuestDb.GetList())
				{
					if (questData == null ||
						!string.Equals(questData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase) ||
						this.StaticQuestParkedFromMainFlow(questData) ||
						this.IsCloverDisabledStaticMainQuest(questData) ||
						this.IsObsoletePapayaStaticQuest(questData))
						continue;

					this.AddMainPrerequisiteBridgeQuestNames(questData, bridgeQuestNames, visitedQuestNames);
				}

				MainPrerequisiteBridgeQuestNames = bridgeQuestNames;
				return MainPrerequisiteBridgeQuestNames;
			}
		}

		private void AddMainPrerequisiteBridgeQuestNames(QuestStaticData questData, HashSet<string> bridgeQuestNames, HashSet<string> visitedQuestNames)
		{
			if (questData?.RequiredQuests == null)
				return;

			foreach (var requiredQuestName in questData.RequiredQuests)
			{
				if (string.IsNullOrWhiteSpace(requiredQuestName) ||
					!ZoneServer.Instance.Data.QuestDb.TryFind(requiredQuestName, out var requiredQuestData) ||
					string.IsNullOrWhiteSpace(requiredQuestData.ClassName))
					continue;

				if (!visitedQuestNames.Add(requiredQuestData.ClassName))
					continue;

				if (this.StaticQuestParkedFromMainFlow(requiredQuestData) ||
					this.IsCloverDisabledStaticMainQuest(requiredQuestData) ||
					this.IsObsoletePapayaStaticQuest(requiredQuestData))
					continue;

				if (!string.Equals(requiredQuestData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase))
					bridgeQuestNames.Add(requiredQuestData.ClassName);

				this.AddMainPrerequisiteBridgeQuestNames(requiredQuestData, bridgeQuestNames, visitedQuestNames);
			}
		}

		private bool IsObsoletePapayaStaticQuest(QuestStaticData questStaticData)
		{
			if (questStaticData == null)
				return false;

			return string.Equals(questStaticData.ClassName, "SOUT_Q_11", StringComparison.OrdinalIgnoreCase) ||
				questStaticData.Name?.Contains("Delete", StringComparison.OrdinalIgnoreCase) == true;
		}

		private bool StaticQuestParkedFromMainFlow(QuestStaticData questStaticData)
			=> this.IsWestSiauliaiOptionalQuestName(questStaticData?.ClassName);

		private Quest CreateStaticQuest(QuestStaticData questStaticData)
		{
			var questData = new Melia.Zone.World.Quests.QuestData
			{
				Id = new QuestId(questStaticData.Id),
				Name = questStaticData.Name,
				Description = questStaticData.Name,
				Type = this.MapQuestType(questStaticData.QuestMode),
				Location = !string.IsNullOrWhiteSpace(questStaticData.ProgMap) ? questStaticData.ProgMap : (!string.IsNullOrWhiteSpace(questStaticData.StartMap) ? questStaticData.StartMap : questStaticData.EndMap),
				QuestGiverLocation = questStaticData.StartMap,
				StartNpcUniqueName = questStaticData.StartNPC,
				EndNpcUniqueName = questStaticData.EndNPC,
				Cancelable = false,
				AutoTrack = string.Equals(questStaticData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase),
				UnlockType = QuestUnlockType.Sequential,
				ReceiveType = QuestReceiveType.Manual,
			};

			this.AddStaticQuestObjectives(questStaticData, questData);
			this.AddStaticQuestRewards(questStaticData, questData);

			return new Quest(questData);
		}

		internal bool TryRestoreStaticQuest(long questClassId, QuestStatus status, DateTime startTime, DateTime completeTime, bool tracked, out Quest quest)
		{
			quest = null;

			if (questClassId < int.MinValue || questClassId > int.MaxValue)
				return false;

			if (!ZoneServer.Instance.Data.QuestDb.TryFind((int)questClassId, out var questStaticData))
				return false;

			quest = this.CreateStaticQuest(questStaticData);
			quest.Status = status;
			quest.StartTime = startTime;
			quest.CompleteTime = completeTime;
			quest.Tracked = tracked;

			this.AddSilent(quest);
			return true;
		}

		private void AddStaticQuestObjectives(QuestStaticData questStaticData, QuestData questData)
		{
			if (questStaticData.Objectives != null && questStaticData.Objectives.Count != 0)
			{
				foreach (var objectiveData in questStaticData.Objectives)
				{
					var ident = !string.IsNullOrWhiteSpace(objectiveData.Ident) ? objectiveData.Ident : $"objective{questData.Objectives.Count + 1}";
					var text = !string.IsNullOrWhiteSpace(objectiveData.Text) ? objectiveData.Text : questStaticData.Name;

					if (string.Equals(objectiveData.Type, "Kill", StringComparison.OrdinalIgnoreCase) &&
						string.Equals(objectiveData.Target, "ALL", StringComparison.OrdinalIgnoreCase))
					{
						var objective = KillObjective.Any(Math.Max(1, objectiveData.Count));
						objective.Ident = ident;
						objective.Text = text;
						this.EnsureStaticObjectiveLoaded(objective);
						questData.Objectives.Add(objective);
						continue;
					}

					if (string.Equals(objectiveData.Type, "Kill", StringComparison.OrdinalIgnoreCase) &&
						this.TryResolveStaticMonsterIds(objectiveData.Target, out var monsterIds))
					{
						var objective = new KillObjective(Math.Max(1, objectiveData.Count), monsterIds)
						{
							Ident = ident,
							Text = text,
						};
						this.EnsureStaticObjectiveLoaded(objective);
						questData.Objectives.Add(objective);
						continue;
					}

					if (string.Equals(objectiveData.Type, "Collect", StringComparison.OrdinalIgnoreCase) &&
						this.TryResolveStaticItem(objectiveData.Item ?? objectiveData.Target, out var itemId))
					{
						var objective = new CollectItemObjective(itemId, Math.Max(1, objectiveData.Count))
						{
							Ident = ident,
							Text = text,
						};
						this.EnsureStaticObjectiveLoaded(objective);
						questData.Objectives.Add(objective);

						if (!string.IsNullOrWhiteSpace(objectiveData.DropTarget) &&
							this.TryResolveStaticMonsterIds(objectiveData.DropTarget, out var dropMonsterIds))
						{
							foreach (var dropMonsterId in dropMonsterIds)
							{
								var modifier = new ItemDropModifier(itemId, objectiveData.DropChance <= 0 ? 1 : objectiveData.DropChance, dropMonsterId);
								this.EnsureStaticModifierLoaded(modifier);
								questData.Modifiers.Add(modifier);
							}
						}

						continue;
					}

					questData.Objectives.Add(new ManualObjective
					{
						Ident = ident,
						Text = text,
					});
				}
			}

			if (questData.Objectives.Count == 0)
			{
				questData.Objectives.Add(new ManualObjective
				{
					Ident = "manual",
					Text = questStaticData.Name,
				});
			}
		}

		private bool TryResolveStaticMonsterIds(string target, out int[] monsterIds)
		{
			monsterIds = Array.Empty<int>();

			if (string.IsNullOrWhiteSpace(target))
				return false;

			var ids = new List<int>();
			foreach (var className in target.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			{
				if (ZoneServer.Instance.Data.MonsterDb.TryFind(className, out var monsterData) && !ids.Contains(monsterData.Id))
					ids.Add(monsterData.Id);
			}

			monsterIds = ids.ToArray();
			return monsterIds.Length != 0;
		}

		private bool TryResolveStaticItem(string item, out int itemId)
		{
			if (int.TryParse(item, out itemId))
				return ZoneServer.Instance.Data.ItemDb.TryFind(itemId, out _);

			if (!string.IsNullOrWhiteSpace(item) && ZoneServer.Instance.Data.ItemDb.TryFind(item, out var itemData))
			{
				itemId = itemData.Id;
				return true;
			}

			itemId = 0;
			return false;
		}

		private void EnsureStaticObjectiveLoaded(QuestObjective objective)
		{
			lock (StaticObjectiveLoadLock)
			{
				if (LoadedStaticObjectiveTypes.Add(objective.GetType()))
					objective.Load();
			}
		}

		private void EnsureStaticModifierLoaded(QuestModifier modifier)
		{
			lock (StaticModifierLoadLock)
			{
				if (LoadedStaticModifierTypes.Add(modifier.GetType()))
					modifier.Load();
			}
		}

		private void AddStaticQuestRewards(QuestStaticData questStaticData, QuestData questData)
		{
			if (questStaticData.RewardItems != null)
			{
				foreach (var rewardData in questStaticData.RewardItems)
				{
					if (string.IsNullOrWhiteSpace(rewardData.Item))
						continue;

					if (int.TryParse(rewardData.Item, out var itemId) || ZoneServer.Instance.Data.ItemDb.TryFind(rewardData.Item, out var itemData) && (itemId = itemData.Id) != 0)
						questData.Rewards.Add(new ItemReward(itemId, Math.Max(1, rewardData.Amount)));
					else
						Log.Warning("Static quest '{0}' references unknown reward item '{1}'.", questStaticData.ClassName, rewardData.Item);
				}
			}

			this.AddStaticQuestExperienceReward(questStaticData, questData);
		}

		private void AddStaticQuestExperienceReward(QuestStaticData questStaticData, QuestData questData)
		{
			var expRate = this.GetStaticQuestExperienceRate(questStaticData);

			questData.Rewards.Add(new LevelScaledExpReward(expRate, expRate));
		}

		private float GetStaticQuestExperienceRate(QuestStaticData questStaticData)
		{
			if (this.IsStarterStaticQuest(questStaticData))
			{
				var starterRate = string.Equals(questStaticData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase) ? 0.20f : 0.10f;

				if (questStaticData.Objectives?.Count > 0)
					starterRate += 0.25f;

				return starterRate;
			}

			var expRate = 0.15f;

			if (string.Equals(questStaticData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase))
				expRate = 0.35f;
			else if (string.Equals(questStaticData.QuestMode, "REPEAT", StringComparison.OrdinalIgnoreCase))
				expRate = 0.05f;

			if (questStaticData.Objectives?.Count > 0)
				expRate += string.Equals(questStaticData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase) ? 0.15f : 0.05f;

			return expRate;
		}

		private bool IsStarterStaticQuest(QuestStaticData questStaticData)
		{
			if (questStaticData == null)
				return false;

			if (string.Equals(questStaticData.QStartZone, "StartLine1", StringComparison.OrdinalIgnoreCase))
				return true;

			if (this.IsWestSiauliaiMap(questStaticData.StartMap) || this.IsWestSiauliaiMap(questStaticData.ProgMap) || this.IsWestSiauliaiMap(questStaticData.EndMap))
				return true;

			return questStaticData.ClassName?.StartsWith("SIAUL_WEST", StringComparison.OrdinalIgnoreCase) == true ||
				string.Equals(questStaticData.ClassName, "TUTO_SKILL_RUN", StringComparison.OrdinalIgnoreCase);
		}

		private bool IsWestSiauliaiMap(string mapName)
		{
			return string.Equals(mapName, "f_siauliai_west", StringComparison.OrdinalIgnoreCase);
		}

		private bool MeetsStaticPrerequisites(QuestStaticData questStaticData)
		{
			if (questStaticData.Level > 0 &&
				this.Character.Level < questStaticData.Level &&
				!string.Equals(questStaticData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase))
				return false;

			if (!this.MeetsStaticCheckScripts(questStaticData))
				return false;

			if (this.StaticQuestIsBlockedByPapayaAutoMainChain(questStaticData))
				return false;

			if (questStaticData.RequiredQuests == null || questStaticData.RequiredQuests.Count == 0)
				return true;

			foreach (var requiredQuest in questStaticData.RequiredQuests)
			{
				if (!ZoneServer.Instance.Data.QuestDb.TryFind(requiredQuest, out var requiredQuestData))
					continue;

				if (!this.HasCompleted(requiredQuestData.Id))
					return false;
			}

			return true;
		}

		private bool MeetsStaticCheckScripts(QuestStaticData questStaticData)
		{
			if (questStaticData.CheckScripts == null || questStaticData.CheckScripts.Count == 0)
				return true;

			foreach (var script in questStaticData.CheckScripts)
			{
				if (string.Equals(script, "IS_SELECTED_JOB", StringComparison.OrdinalIgnoreCase))
					return false;
			}

			return true;
		}

		/// <summary>
		/// Advances, completes, and starts static NPC-dialog quests that
		/// are tied to the given NPC dialog name.
		/// </summary>
		public bool HandleStaticNpcDialog(string npcDialogName)
		{
			if (string.IsNullOrWhiteSpace(npcDialogName))
				return false;

			this.RepairStaticQuestObjectiveSuccessStatus("NPC dialog");

			var quests = this.GetList()
				.OrderByDescending(quest => quest.Tracked)
				.ThenByDescending(quest => string.Equals(quest.QuestStaticData?.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase))
				.ThenBy(quest => quest.Data.Level)
				.ThenBy(quest => quest.StartTime)
				.ToList();

			foreach (var quest in quests)
			{
				if (quest.QuestStaticData == null)
					continue;

				if (quest.IsPossible && this.StaticQuestCanStartFromNpcDialog(quest.QuestStaticData, npcDialogName))
				{
					this.Start(quest);
					this.SyncStaticQuestNpcStatesAfterDialog();
					return true;
				}

				if ((quest.InProgress || quest.Status == QuestStatus.Success) &&
					this.StaticQuestCanTurnInFromNpcDialog(quest, npcDialogName))
				{
					this.Complete(quest);
					this.SyncStaticQuestNpcStatesAfterDialog();
					return true;
				}

				if (quest.InProgress && this.TryAdvanceStaticQuestFromNpcDialog(quest, npcDialogName))
				{
					this.SyncStaticQuestNpcStatesAfterDialog();
					return true;
				}

				if (quest.InProgress && this.TryTriggerStaticQuestFromNpcDialog(quest, npcDialogName))
				{
					this.SyncStaticQuestNpcStatesAfterDialog();
					return true;
				}

				if (quest.Status == QuestStatus.Success && this.TryTurnInStaticQuestFromNpcDialog(quest, npcDialogName))
				{
					this.SyncStaticQuestNpcStatesAfterDialog();
					return true;
				}
			}

			var startableQuest = ZoneServer.Instance.Data.QuestDb.GetList()
				.OrderBy(a => a.Id)
				.FirstOrDefault(a =>
					string.Equals(a.QuestStartMode, "NPCDIALOG", StringComparison.OrdinalIgnoreCase) &&
					!this.StaticQuestDisabledForCloverFlow(a) &&
					this.StaticQuestCanStartFromNpcDialog(a, npcDialogName) &&
					!this.StaticQuestIsBlockedByPriorityQuest(a, npcDialogName) &&
					!this.StaticQuestIsBlockedByEarlierWestSiauliaiMain(a) &&
					!this.StaticQuestIsBlockedByPapayaMainProgression(a) &&
					!this.Has(new QuestId(a.Id)) &&
					this.MeetsStaticPrerequisites(a));

			if (startableQuest == null)
				return false;

			this.StartStaticQuest(startableQuest, TimeSpan.Zero);
			this.SyncStaticQuestNpcStatesAfterDialog();
			return true;
		}

		/// <summary>
		/// Advances an already active static NPC/location objective without
		/// completing turn-ins or starting follow-up quests from the same click.
		/// </summary>
		public bool AdvanceStaticNpcDialogProgressOnly(string npcDialogName)
		{
			if (string.IsNullOrWhiteSpace(npcDialogName))
				return false;

			var quests = this.GetList()
				.OrderByDescending(quest => quest.Tracked)
				.ThenByDescending(quest => string.Equals(quest.QuestStaticData?.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase))
				.ThenBy(quest => quest.Data.Level)
				.ThenBy(quest => quest.StartTime)
				.ToList();

			foreach (var quest in quests)
			{
				if (quest.QuestStaticData == null || !quest.InProgress)
					continue;

				if (this.TryAdvanceStaticQuestFromNpcDialog(quest, npcDialogName) ||
					this.TryTriggerStaticQuestFromNpcDialog(quest, npcDialogName))
				{
					this.SyncStaticQuestNpcStatesAfterDialog();
					return true;
				}
			}

			return false;
		}

		private void SyncStaticQuestNpcStatesAfterDialog(int attempt = 0)
		{
			if (this.Character?.Connection?.CurrentDialog == null)
			{
				this.SyncStaticQuestNpcStates();
				return;
			}

			this.SyncStaticQuestSessionObjects();
			if (attempt > 0)
				return;

			_ = Task.Run(async () =>
			{
				await Task.Delay(650);
				if (this.Character?.Connection != null)
					this.SyncStaticQuestNpcStatesAfterDialog(attempt + 1);
			});
		}

		/// <summary>
		/// Reconciles static quest availability with NPC actor visibility on the
		/// current map. Quest data can make the client show a marker, but the
		/// server must still reveal the actual actor for this character.
		/// </summary>
		public void SyncStaticQuestNpcStates()
		{
			if (this.Character?.Connection == null || this.Character.Map == null)
				return;

			var mapClassName = this.Character.Map.ClassName;
			this.SuppressWestSiauliaiSideQuestNoise(mapClassName);
			this.SuppressDisabledStaticQuestNoise(mapClassName);
			this.ReconcileStaticQuestVisibility(mapClassName);
			this.EnsureStaticQuestLayerState();
			this.SyncStaticQuestSessionObjects();
			this.EnsureStaticQuestNpcActors(mapClassName);
			var startedQuestAutoTrack = this.TryStartStaticQuestAutoTracks(mapClassName);
			if (!startedQuestAutoTrack)
			{
				this.EnsureStaticQuestObjectiveMonsters(mapClassName);
				this.SyncStaticQuestObjectiveMonsterMarkers(mapClassName);
			}

			var npcs = this.Character.Map.GetNpcs(a =>
				a is Npc npc &&
				!string.IsNullOrWhiteSpace(npc.DialogName) &&
				(npc.Layer == this.Character.Layer || this.Character.Layer == 0));

			foreach (var minMon in npcs)
			{
				if (minMon is not Npc npc)
					continue;

				var isRelevantStaticNpc = this.StaticNpcIsRelevantForCurrentQuestState(npc.DialogName, mapClassName);
				if (this.IsTechnicalStaticQuestNpc(npc.DialogName) && !isRelevantStaticNpc)
				{
					if (this.Character.GetMapNPCState(npc) != NpcState.Invisible)
						this.Character.SetMapNPCState(npc, NpcState.Invisible);
					continue;
				}

				if (!isRelevantStaticNpc)
				{
					if (this.ShouldSuppressStaticQuestNpcState(npc.DialogName, mapClassName))
					{
						if (this.Character.GetMapNPCState(npc) != NpcState.Invisible)
							this.Character.SetMapNPCState(npc, NpcState.Invisible);
						continue;
					}

					continue;
				}

				var currentState = this.Character.GetMapNPCState(npc);
				if (currentState != NpcState.Highlighted)
				{
					this.Character.SetMapNPCState(npc, NpcState.Highlighted);
					continue;
				}

				this.RefreshVisibleStaticQuestNpc(npc, currentState);
			}
		}

		private void RefreshVisibleStaticQuestNpc(Npc npc, NpcState state)
		{
			if (npc == null || this.Character?.Connection == null)
				return;

			if (state == NpcState.Invisible || state == NpcState.IgnoreState)
				return;

			Send.ZC_ENTER_MONSTER(this.Character.Connection, npc);
			Send.ZC_SET_NPC_STATE(this.Character.Connection, npc, (short)state);
		}

		private bool TryStartStaticQuestAutoTracks(string mapClassName)
		{
			if (this.Character?.Connection == null || this.Character.Tracks.ActiveTrack != null || string.IsNullOrWhiteSpace(mapClassName))
				return false;

			this.RepairStaticQuestObjectiveSuccessStatus("quest_auto scan");

			var quests = this.GetList()
				.Where(quest => quest.QuestStaticData != null && quest.Status != QuestStatus.Completed && quest.Status != QuestStatus.Abandoned)
				.OrderByDescending(quest => quest.Tracked)
				.ThenBy(quest => quest.Data.Level)
				.ThenBy(quest => quest.StartTime)
				.ToList();

			foreach (var quest in quests)
			{
				if (this.TryStartStaticQuestAutoTrack(quest, mapClassName))
					return true;
			}

			return false;
		}

		private bool RepairStaticQuestObjectiveSuccessStatus(string reason)
		{
			var changed = false;

			foreach (var quest in this.GetList().ToList())
			{
				var questData = quest.QuestStaticData;
				if (questData == null ||
					quest.Status != QuestStatus.InProgress ||
					quest.Progresses.Count == 0 ||
					questData.Objectives == null ||
					questData.Objectives.Count == 0 ||
					!quest.ObjectivesCompleted)
					continue;

				quest.Status = QuestStatus.Success;
				this.SetStaticQuestProperty(questData, QuestStatus.Success);
				this.SyncStaticQuestSessionObject(quest);
				this.UpdateClient_UpdateQuest(quest);
				this.TryAutoCompleteStaticQuestOnSuccess(quest);
				changed = true;

				Log.Info(
					"Static quest chain: restored quest '{0}' from InProgress to Success after completed objectives during {1} for '{2}'.",
					questData.ClassName,
					reason,
					this.Character.Name
				);
			}

			if (changed)
				this.SyncTrackedQuestSlots();

			return changed;
		}

		private bool TryStartStaticQuestAutoTrack(Quest quest, string mapClassName)
		{
			var questData = quest?.QuestStaticData;
			if (questData == null || !string.Equals(questData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase))
				return false;

			if (this.TryCompletePapayaGelePlateauImminentInvasion(quest, mapClassName, "runtime map sync"))
				return true;

			var questAutoData = ZoneServer.Instance.Data.QuestAutoDb.Find(questData.ClassName);
			if (!this.TryParseStaticQuestAutoTrack(questAutoData, out var track))
				return false;

			if (!this.StaticQuestAutoTrackStartStatusMatches(quest, track.StartStatus))
				return false;

			if (!this.StaticQuestAutoTrackCanStartAtCurrentLocation(quest, track, mapClassName))
				return false;

			if (PropertyTable.Exists(this.Character.Etc.Properties.Namespace, track.PropertyName) &&
				this.Character.Etc.Properties.GetFloat(track.PropertyName) == 1)
				return false;

			var tempKey = $"QuestAutoTrack.Started.{quest.Data.Id.Value}.{track.TrackName}";
			if (this.Character.Variables.Temp.GetBool(tempKey, false))
				return false;

			this.Character.Variables.Temp.SetBool(tempKey, true);
			var delay = TimeSpan.FromMilliseconds(Math.Max(0, track.DelayMilliseconds));
			var onStartStatus = this.ParseStaticQuestAutoStatus(track.StartStatus, quest.Status);
			var onCompleteStatus = this.ParseStaticQuestAutoStatus(track.EndStatus, quest.Status);
			var shouldApplyQuestStatus = this.StaticQuestAutoTrackShouldApplyQuestStatus(quest) &&
				!this.StaticQuestAutoTrackWouldRegressQuestStatus(quest, onStartStatus, onCompleteStatus);
			var questIdForTrackStatus = shouldApplyQuestStatus ? questData.Id : 0;

			Log.Info("Papaya quest_auto: starting track '{0}' for quest '{1}' ({2}) on map '{3}' at status {4}.", track.TrackName, questData.ClassName, questData.Id, mapClassName, quest.Status);
			_ = this.Character.Tracks.Start(track.TrackName, delay, questIdForTrackStatus, onStartStatus, onCompleteStatus, track.PropertyName, questData.Id);
			this.QueueDelayedNativeQuestTrackerRefresh(quest.Data.Id, Math.Max(650, track.DelayMilliseconds + 3500));
			return true;
		}

		private bool StaticQuestAutoTrackWouldRegressQuestStatus(Quest quest, QuestStatus onStartStatus, QuestStatus onCompleteStatus)
		{
			if (quest == null || quest.Status < QuestStatus.Success)
				return false;

			return onStartStatus < quest.Status || onCompleteStatus < quest.Status;
		}

		public IActor[] CreateGenericQuestAutoTrackActors(Track track)
		{
			var quest = this.ResolveGenericQuestAutoTrackQuest(track);
			if (quest?.QuestStaticData == null || this.Character?.Map == null)
				return Array.Empty<IActor>();

			var mapClassName = this.Character.Map.ClassName;
			var actors = new List<IActor>();
			this.AddGenericQuestAutoTrackNpcActors(quest, mapClassName, actors);
			this.AddGenericQuestAutoTrackMonsterActors(quest, mapClassName, actors);

			if (actors.Count != 0)
			{
				Log.Info(
					"Papaya quest_auto: generic track '{0}' for quest '{1}' prepared {2} actor(s) on map '{3}' layer {4}.",
					track.Id,
					quest.QuestStaticData.ClassName,
					actors.Count,
					mapClassName,
					this.Character.Layer
				);
			}

			return actors.ToArray();
		}

		public void QueueGenericQuestAutoTrackFollowUp(Track track)
		{
			var questId = track?.Data?.SourceQuestId > 0 ? track.Data.SourceQuestId : track?.Data?.QuestId ?? 0;
			var trackId = track?.Id ?? "";

			_ = Task.Run(async () =>
			{
				await Task.Delay(250);

				if (this.Character?.Connection == null || this.Character.Map == null)
					return;

				Quest quest = null;
				if (questId > 0)
					this.TryGetById(questId, out quest);

				if (quest?.QuestStaticData != null &&
					quest.Status == QuestStatus.Success &&
					this.TryAutoCompleteStaticQuestOnSuccess(quest))
					return;

				var mapClassName = this.Character.Map.ClassName;
				this.EnsureStaticQuestNpcActors(mapClassName);
				this.EnsureStaticQuestObjectiveMonsters(mapClassName);
				this.SyncStaticQuestObjectiveMonsterMarkers(mapClassName);
				this.SyncStaticQuestSessionObjects();
				this.SyncStaticQuestNpcStates();
				this.SyncTrackedQuestSlots();
				this.UpdateClient();
				this.Character.RestoreCoreHudState(true, true);

				Log.Info("Papaya quest_auto: generic track '{0}' completed follow-up sync for '{1}' on map '{2}'.", trackId, this.Character.Name, mapClassName);
			});
		}

		private Quest ResolveGenericQuestAutoTrackQuest(Track track)
		{
			var questId = track?.Data?.SourceQuestId > 0 ? track.Data.SourceQuestId : track?.Data?.QuestId ?? 0;
			if (questId <= 0)
				return null;

			return this.TryGetById(questId, out var quest) ? quest : null;
		}

		private void AddGenericQuestAutoTrackNpcActors(Quest quest, string mapClassName, List<IActor> actors)
		{
			var questData = quest?.QuestStaticData;
			if (questData == null || this.Character?.Map == null)
				return;

			var dialogNames = new[] { questData.StartNPC, questData.ProgNPC, questData.EndNPC }
				.Where(dialogName => !string.IsNullOrWhiteSpace(dialogName))
				.Distinct(StringComparer.OrdinalIgnoreCase);

			foreach (var dialogName in dialogNames)
			{
				if (!this.TryResolveStaticNpcPosition(questData, dialogName, mapClassName, out var x, out var y, out var z, out var range))
					continue;

				var modelId = this.ResolveStaticQuestNpcMonsterId(dialogName);
				var name = this.ResolveStaticQuestNpcName(dialogName);
				var dialogHash = StringComparer.OrdinalIgnoreCase.GetHashCode(dialogName) & 0x7fffffff;
				var genType = 1800000 + dialogHash % 100000;
				var npc = Shortcuts.AddNpc(genType, modelId, name, mapClassName, x, y, z, 0, dialogName, state: (int)NpcState.Normal, range: range);
				npc.SetVisibilty(ActorVisibility.Track, this.Character.ObjectId);
				npc.Layer = this.Character.Layer;

				if (this.Character.Connection != null)
					Send.ZC_ENTER_MONSTER(this.Character.Connection, npc);

				actors.Add(npc);
			}
		}

		private void AddGenericQuestAutoTrackMonsterActors(Quest quest, string mapClassName, List<IActor> actors)
		{
			var questData = quest?.QuestStaticData;
			if (questData == null)
				return;

			if (this.StaticQuestAutoTrackShouldAvoidGenericMonsterActors(quest))
				return;

			var actorBudget = Math.Max(0, 10 - actors.Count);
			if (actorBudget <= 0)
				return;

			var requests = this.GetRelevantStaticMonsterSpawnRequests(mapClassName)
				.Where(request => string.Equals(request.QuestClassName, questData.ClassName, StringComparison.OrdinalIgnoreCase))
				.ToList();

			var actorIndex = 0;
			foreach (var request in requests)
			{
				var count = Math.Min(request.Count, actorBudget);
				for (var i = 0; i < count; i++)
				{
					var actor = this.SpawnGenericQuestAutoTrackMonster(request, actorIndex++);
					if (actor == null)
						continue;

					actors.Add(actor);
					actorBudget--;
					if (actorBudget <= 0)
						return;
				}
			}
		}

		private bool StaticQuestAutoTrackShouldAvoidGenericMonsterActors(Quest quest)
		{
			var questData = quest?.QuestStaticData;
			if (questData?.Objectives == null || questData.Objectives.Count == 0)
				return false;

			if (this.HasPrivateEncounterObjective(quest))
				return true;

			return questData.Objectives.Any(objectiveData =>
				objectiveData != null &&
				(string.Equals(objectiveData.Type, "Kill", StringComparison.OrdinalIgnoreCase) ||
				 string.Equals(objectiveData.Type, "Collect", StringComparison.OrdinalIgnoreCase)));
		}

		private Mob SpawnGenericQuestAutoTrackMonster(StaticQuestMonsterSpawnRequest request, int actorIndex)
		{
			if (this.Character?.Map == null)
				return null;

			if (!ZoneServer.Instance.Data.MonsterDb.TryFind(request.MonsterId, out var monsterData))
				return null;

			var ring = actorIndex / 8;
			var angle = (Math.PI * 2 / 8) * (actorIndex % 8);
			var offset = 35 + ring * 25;
			var x = request.X + Math.Cos(angle) * offset;
			var z = request.Z + Math.Sin(angle) * offset;

			var monster = new Mob(monsterData.Id, RelationType.Enemy)
			{
				Name = request.Name,
				Position = new Position((float)x, (float)request.Y, (float)z),
				Direction = new Direction(0),
				Layer = this.Character.Layer,
			};

			monster.SpawnPosition = monster.Position;
			monster.SetVisibilty(ActorVisibility.Track, this.Character.ObjectId);
			monster.Components.Add(new AiComponent(monster, "BasicMonster"));
			this.Character.Map.AddMonster(monster);

			if (this.Character.Connection != null)
				Send.ZC_ENTER_MONSTER(this.Character.Connection, monster);

			return monster;
		}

		private bool StaticQuestAutoTrackShouldApplyQuestStatus(Quest quest)
		{
			var questData = quest?.QuestStaticData;
			if (questData?.Objectives == null || questData.Objectives.Count == 0)
				return true;

			foreach (var objectiveData in questData.Objectives)
			{
				if (objectiveData == null)
					continue;

				var isCombatOrCollect =
					string.Equals(objectiveData.Type, "Kill", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(objectiveData.Type, "Collect", StringComparison.OrdinalIgnoreCase);
				if (!isCombatOrCollect)
					continue;

				if (!quest.TryGetProgress(objectiveData.Ident, out var progress) || !progress.Done)
					return false;
			}

			return true;
		}

		private bool StaticQuestHasAutoTrackForCurrentStatus(Quest quest)
		{
			var questData = quest?.QuestStaticData;
			if (questData == null || !string.Equals(questData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase))
				return false;

			var questAutoData = ZoneServer.Instance.Data.QuestAutoDb.Find(questData.ClassName);
			return this.TryParseStaticQuestAutoTrack(questAutoData, out var track) &&
				this.StaticQuestAutoTrackStartStatusMatches(quest, track.StartStatus);
		}

		private bool TryParseStaticQuestAutoTrack(QuestAutoData questAutoData, out StaticQuestAutoTrackRequest track)
		{
			track = default;
			if (questAutoData == null || string.IsNullOrWhiteSpace(questAutoData.Track))
				return false;

			var parts = questAutoData.Track.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[2]))
				return false;

			var delayMilliseconds = 0;
			if (parts.Length >= 4)
				int.TryParse(parts[3], out delayMilliseconds);

			track = new StaticQuestAutoTrackRequest(parts[0], parts[1], parts[2], parts[2], delayMilliseconds);
			return true;
		}

		private bool StaticMainFollowUpShouldStartImmediately(QuestStaticData completedQuestData, QuestStaticData nextQuestData)
		{
			if (nextQuestData == null ||
				!string.Equals(nextQuestData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase))
				return false;

			if (!string.Equals(nextQuestData.QuestStartMode, "NPCDIALOG", StringComparison.OrdinalIgnoreCase))
				return true;

			var mapClassName = this.Character?.Map?.ClassName;
			if (string.IsNullOrWhiteSpace(mapClassName))
				return false;

			return this.StaticQuestReferencesMap(nextQuestData, mapClassName);
		}

		private bool StaticQuestAutoTrackStartStatusMatches(Quest quest, string startStatus)
		{
			var status = (startStatus ?? "").Trim();
			if (status.StartsWith("S", StringComparison.OrdinalIgnoreCase))
				status = status.Substring(1);

			if (string.Equals(status, "Possible", StringComparison.OrdinalIgnoreCase))
				return quest.Status == QuestStatus.Possible;
			if (string.Equals(status, "Progress", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "InProgress", StringComparison.OrdinalIgnoreCase))
				return quest.Status == QuestStatus.InProgress;
			if (string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase))
				return quest.Status == QuestStatus.Success;
			if (string.Equals(status, "Complete", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
				return quest.Status == QuestStatus.Completed;

			return quest.InProgress || quest.Status == QuestStatus.Success;
		}

		private QuestStatus ParseStaticQuestAutoStatus(string status, QuestStatus fallback)
		{
			status = (status ?? string.Empty).Trim();
			if (status.Length > 1 &&
				(status[0] == 'S' || status[0] == 's' || status[0] == 'E' || status[0] == 'e'))
				status = status.Substring(1);

			if (string.Equals(status, "Impossible", StringComparison.OrdinalIgnoreCase))
				return QuestStatus.Impossible;
			if (string.Equals(status, "Possible", StringComparison.OrdinalIgnoreCase))
				return QuestStatus.Possible;
			if (string.Equals(status, "Progress", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(status, "InProgress", StringComparison.OrdinalIgnoreCase))
				return QuestStatus.InProgress;
			if (string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase))
				return QuestStatus.Success;
			if (string.Equals(status, "Complete", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
				return QuestStatus.Completed;

			return fallback;
		}

		private bool StaticQuestAutoTrackCanStartAtCurrentLocation(Quest quest, StaticQuestAutoTrackRequest track, string mapClassName)
		{
			var questData = quest.QuestStaticData;
			var location = this.GetStaticQuestAutoTrackLocation(questData, track.StartStatus);
			var points = this.GetStaticQuestLocationPoints(location, mapClassName).ToList();

			if (points.Count != 0)
				return points.Any(point =>
				{
					var dx = this.Character.Position.X - point.X;
					var dz = this.Character.Position.Z - point.Z;
					return (dx * dx) + (dz * dz) <= point.Range * point.Range;
				});

			if (!string.IsNullOrWhiteSpace(location) && this.StaticQuestLocationReferencesMap(location, mapClassName))
				return this.QuestTouchesMap(quest, mapClassName);

			return this.QuestTouchesMap(quest, mapClassName);
		}

		private string GetStaticQuestAutoTrackLocation(QuestStaticData questData, string startStatus)
		{
			var status = (startStatus ?? "").Trim();
			if (status.StartsWith("S", StringComparison.OrdinalIgnoreCase))
				status = status.Substring(1);

			if (string.Equals(status, "Possible", StringComparison.OrdinalIgnoreCase))
				return questData.StartLocation;
			if (string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase))
				return questData.EndLocation;

			return questData.ProgLocation;
		}

		private IEnumerable<StaticQuestLocationPoint> GetStaticQuestLocationPoints(string location, string mapClassName)
		{
			if (string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(mapClassName))
				yield break;

			var parts = location.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			for (var i = 0; i < parts.Length; i++)
			{
				if (!string.Equals(parts[i], mapClassName, StringComparison.OrdinalIgnoreCase))
					continue;

				if (i + 4 < parts.Length &&
					double.TryParse(parts[i + 1], out var x) &&
					double.TryParse(parts[i + 2], out var y) &&
					double.TryParse(parts[i + 3], out var z))
				{
					if (!double.TryParse(parts[i + 4], out var range) || range <= 0)
						range = 100;
					yield return new StaticQuestLocationPoint(x, y, z, range);
					continue;
				}

				if (i + 2 < parts.Length)
				{
					var anchorName = parts[i + 1];
					if (!double.TryParse(parts[i + 2], out var range) || range <= 0)
						range = 100;

					if (this.TryResolveLiveStaticNpcPosition(anchorName, mapClassName, out x, out y, out z, out var liveRange))
					{
						if (liveRange > 0)
							range = Math.Max(range, liveRange);
						yield return new StaticQuestLocationPoint(x, y, z, range);
						continue;
					}

					if (this.TryResolvePapayaCapturedStaticNpcPosition(mapClassName, anchorName, out x, out y, out z, out var capturedRange))
					{
						if (capturedRange > 0)
							range = capturedRange;
						yield return new StaticQuestLocationPoint(x, y, z, range);
						continue;
					}

					if (this.TryResolveStaticNamedAnchor(anchorName, out x, out y, out z))
						yield return new StaticQuestLocationPoint(x, y, z, range);
				}
			}
		}

		public bool ShouldSuppressStaticQuestNpcState(string npcDialogName, string mapClassName)
		{
			if (string.IsNullOrWhiteSpace(npcDialogName) || string.IsNullOrWhiteSpace(mapClassName))
				return false;

			if (this.StaticNpcIsRelevantForCurrentQuestState(npcDialogName, mapClassName))
				return false;

			if (this.IsTechnicalStaticQuestNpc(npcDialogName))
				return true;

			if (this.StaticNpcIsBlockedByWestSiauliaiMainOrder(npcDialogName, mapClassName))
				return true;

			return this.StaticNpcOnlyBelongsToUnavailableStaticQuests(npcDialogName, mapClassName);
		}

		private void ReconcileStaticQuestVisibility(string mapClassName)
		{
			if (string.IsNullOrWhiteSpace(mapClassName))
				return;

			foreach (var questData in ZoneServer.Instance.Data.QuestDb.GetList())
			{
				if (this.StaticQuestDisabledForCloverFlow(questData))
					continue;

				if (!this.StaticQuestReferencesMap(questData, mapClassName))
					continue;

				if (!this.StaticQuestUsesNativeQuestState(questData))
					continue;

				if (this.StaticQuestIsClientHiddenPapayaBridge(questData))
				{
					this.HideUnavailableStaticQuestFromNativeClient(questData);
					continue;
				}

				var questId = new QuestId(questData.Id);
				if (this.TryGetById(questId, out var quest))
				{
					if (quest.Status == QuestStatus.Completed)
					{
						this.SetStaticQuestProperty(questData, QuestStatus.Completed);
						continue;
					}

					if (quest.InProgress || quest.Status == QuestStatus.Success)
					{
						this.SyncStaticQuestSessionObject(quest);
						this.SetStaticQuestProperty(questData, quest.Status);
						continue;
					}
				}

				if (this.StaticQuestShouldBeHiddenUntilAvailable(questData, mapClassName))
				{
					this.HideUnavailableStaticQuestFromNativeClient(questData);
					continue;
				}

				if (this.StaticQuestCanShowAsPossible(questData, mapClassName))
					this.SetStaticQuestProperty(questData, QuestStatus.Possible);
			}
		}

		private bool StaticQuestUsesNativeQuestState(QuestStaticData questData)
		{
			if (questData == null || string.IsNullOrWhiteSpace(questData.QuestProperty))
				return false;

			if (string.Equals(questData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase))
				return true;

			return questData.RequiredQuests?.Count > 0 ||
				questData.ClassName?.StartsWith("SIAUL_WEST", StringComparison.OrdinalIgnoreCase) == true ||
				string.Equals(questData.ClassName, "TUTO_SKILL_RUN", StringComparison.OrdinalIgnoreCase);
		}

		private bool StaticQuestShouldBeHiddenUntilAvailable(QuestStaticData questData, string mapClassName)
		{
			if (questData == null)
				return false;

			if (this.StaticQuestDisabledForCloverFlow(questData))
				return true;

			if (this.StaticQuestIsClientHiddenPapayaBridge(questData))
				return true;

			if (this.StaticQuestIsBlockedByEarlierWestSiauliaiMain(questData))
				return true;

			if (this.StaticQuestIsBlockedByPapayaMainProgression(questData))
				return true;

			if (!this.MeetsStaticPrerequisites(questData))
				return true;

			if (this.IsWestSiauliaiSideQuestNoise(questData, mapClassName))
				return true;

			return false;
		}

		private bool StaticQuestCanShowAsPossible(QuestStaticData questData, string mapClassName)
		{
			if (questData == null || this.Has(new QuestId(questData.Id)))
				return false;

			if (this.StaticQuestDisabledForCloverFlow(questData))
				return false;

			if (this.StaticQuestIsClientHiddenPapayaBridge(questData))
				return false;

			if (!this.MeetsStaticPrerequisites(questData))
				return false;

			if (this.StaticQuestIsBlockedByEarlierWestSiauliaiMain(questData))
				return false;

			if (this.StaticQuestIsBlockedByPapayaMainProgression(questData))
				return false;

			if (this.IsWestSiauliaiSideQuestNoise(questData, mapClassName))
				return false;

			return string.Equals(questData.QuestStartMode, "NPCDIALOG", StringComparison.OrdinalIgnoreCase) &&
				(this.StaticQuestMapMatches(questData.StartMap, mapClassName) ||
				 this.StaticQuestLocationReferencesMap(questData.StartLocation, mapClassName));
		}

		private bool IsWestSiauliaiSideQuestNoise(QuestStaticData questData, string mapClassName)
		{
			if (questData == null || !this.IsWestSiauliaiMap(mapClassName))
				return false;

			if (string.Equals(questData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase))
				return false;

			if (this.IsWestSiauliaiOptionalQuestName(questData.ClassName))
				return true;

			if (questData.ClassName?.StartsWith("SIAUL_WEST", StringComparison.OrdinalIgnoreCase) != true &&
				!string.Equals(questData.ClassName, "TUTO_SKILL_RUN", StringComparison.OrdinalIgnoreCase))
				return false;

			return !this.HasCompleted(1015);
		}

		private bool HideUnavailableStaticQuestFromNativeClient(QuestStaticData questData)
		{
			if (questData == null)
				return false;

			var hiddenStatus = this.StaticQuestParkedFromMainFlow(questData)
				? QuestStatus.Completed
				: QuestStatus.Impossible;
			var removedSessionObject = this.RemoveStaticQuestSessionObject(questData);
			var changedQuestProperty = this.SetStaticQuestProperty(questData, hiddenStatus);
			this.NotifyNativeStaticQuestStateChanged(questData, false);

			if (removedSessionObject || changedQuestProperty)
				this.RemoveStaticQuestFromClientList(questData.Id, questData.ClassName);

			return removedSessionObject || changedQuestProperty;
		}

		private void SuppressDisabledStaticQuestNoise(string mapClassName)
		{
			var changed = false;

			foreach (var questData in ZoneServer.Instance.Data.QuestDb.GetList())
			{
				if (!this.StaticQuestDisabledForCloverFlow(questData))
					continue;

				if (!this.StaticQuestReferencesMap(questData, mapClassName))
					continue;

				changed |= this.HideUnavailableStaticQuestFromNativeClient(questData);
			}

			foreach (var quest in this.GetList())
			{
				if (quest.QuestStaticData == null || !this.StaticQuestDisabledForCloverFlow(quest.QuestStaticData))
					continue;

				if (!quest.InProgress && quest.Status != QuestStatus.Success && quest.Status != QuestStatus.Possible)
					continue;

				Log.Info("Clover quest flow: removing disabled static quest {0} ({1}) from '{2}' on map '{3}'.", quest.QuestStaticData.ClassName, quest.Data.Id.Value, this.Character.Name, mapClassName);
				quest.Tracked = false;
				this.UpdateClient_RemoveQuest(quest);
				this.HideUnavailableStaticQuestFromNativeClient(quest.QuestStaticData);

				lock (_syncLock)
					_quests.Remove(quest);

				changed = true;
			}

			if (changed)
				this.SyncTrackedQuestSlots();
		}

		private bool StaticNpcOnlyBelongsToUnavailableStaticQuests(string npcDialogName, string mapClassName)
		{
			var matchedStaticQuest = false;

			foreach (var questData in ZoneServer.Instance.Data.QuestDb.GetList())
			{
				if (!this.StaticQuestReferencesMap(questData, mapClassName))
					continue;

				if (!this.StaticQuestUsesNativeQuestState(questData))
					continue;

				if (!this.StaticNpcDialogMatches(questData.StartNPC, npcDialogName) &&
					!this.StaticNpcDialogMatches(questData.ProgNPC, npcDialogName) &&
					!this.StaticNpcDialogMatches(questData.EndNPC, npcDialogName))
					continue;

				matchedStaticQuest = true;

				if (!this.StaticQuestShouldBeHiddenUntilAvailable(questData, mapClassName))
					return false;
			}

			return matchedStaticQuest;
		}

		private bool StaticNpcIsRelevantForCurrentQuestState(string npcDialogName, string mapClassName)
		{
			foreach (var questData in ZoneServer.Instance.Data.QuestDb.GetList())
			{
				if (this.StaticQuestDisabledForCloverFlow(questData))
					continue;

				if (!this.StaticQuestReferencesMap(questData, mapClassName))
					continue;

				var questId = new QuestId(questData.Id);
				if (this.TryGetById(questId, out var quest))
				{
					if (quest.Status == QuestStatus.Completed || quest.Status == QuestStatus.Abandoned)
						continue;

					if (quest.Status == QuestStatus.Success && this.StaticNpcDialogMatches(questData.EndNPC, npcDialogName))
						return true;

					if (quest.InProgress &&
						(this.StaticNpcDialogMatches(questData.ProgNPC, npcDialogName) ||
						 this.StaticNpcDialogMatches(questData.EndNPC, npcDialogName) ||
						 this.StaticNpcDialogMatches(questData.StartNPC, npcDialogName)))
						return true;

					continue;
				}

				if (string.Equals(questData.QuestStartMode, "NPCDIALOG", StringComparison.OrdinalIgnoreCase) &&
					this.StaticNpcDialogMatches(questData.StartNPC, npcDialogName) &&
					!this.StaticQuestIsBlockedByEarlierWestSiauliaiMain(questData) &&
					!this.StaticQuestIsBlockedByPapayaMainProgression(questData) &&
					this.MeetsStaticPrerequisites(questData))
					return true;
			}

			return false;
		}

		private bool StaticNpcIsBlockedByWestSiauliaiMainOrder(string npcDialogName, string mapClassName)
		{
			if (string.IsNullOrWhiteSpace(npcDialogName) ||
				!string.Equals(mapClassName, "f_siauliai_west", StringComparison.OrdinalIgnoreCase))
				return false;

			foreach (var questData in ZoneServer.Instance.Data.QuestDb.GetList())
			{
				if (!this.StaticQuestReferencesMap(questData, mapClassName))
					continue;

				if (!this.StaticQuestIsBlockedByEarlierWestSiauliaiMain(questData))
					continue;

				if (this.StaticNpcDialogMatches(questData.StartNPC, npcDialogName) ||
					this.StaticNpcDialogMatches(questData.ProgNPC, npcDialogName) ||
					this.StaticNpcDialogMatches(questData.EndNPC, npcDialogName))
					return true;
			}

			return false;
		}

		private void EnsureStaticQuestObjectiveMonsters(string mapClassName)
		{
			if (!this.CanSendStaticQuestObjectiveActors())
				return;

			if (this.Character?.Tracks.ActiveTrack != null)
				return;

			foreach (var request in this.GetRelevantStaticMonsterSpawnRequests(mapClassName))
			{
				var radius = Math.Max(125, request.Range);
				var existingMonsters = this.GetStaticQuestObjectiveMonsters(request, radius).ToList();
				existingMonsters = this.DeduplicateStaticQuestObjectiveMonsters(request, existingMonsters);
				foreach (var existingMonster in existingMonsters)
				{
					this.ConfigureStaticQuestObjectiveMonster(existingMonster);
					this.SendStaticQuestObjectiveMonsterIfNeeded(existingMonster, request);
				}

				var existingCount = existingMonsters.Count;
				var pendingKey = this.GetStaticQuestObjectiveSpawnPendingKey(request);
				if (existingCount > 0)
					_staticQuestObjectiveSpawnPending.Remove(pendingKey);

				if (existingCount >= request.Count)
					continue;

				if (this.StaticQuestObjectiveSpawnIsPending(pendingKey))
					continue;

				var spawnCount = Math.Min(request.Count - existingCount, 5);
				if (spawnCount <= 0)
					continue;

				_staticQuestObjectiveSpawnPending[pendingKey] = DateTime.UtcNow;

				for (var i = 0; i < spawnCount; i++)
				{
					var offset = i * 35;
					var monster = Shortcuts.AddMonster(0, request.MonsterId, request.Name, mapClassName, request.X + offset, request.Y, request.Z + offset, 0, "Monster");
					monster.Layer = this.Character.Layer;
					if (request.IsPrivateEncounter)
					{
						monster.OwnerHandle = this.Character.Handle;
						monster.Vars.SetBool("Clover.StaticQuestPrivateEncounter", true);
						monster.SetVisibilty(ActorVisibility.Individual, this.Character.ObjectId);
					}
					monster.SpawnPosition = monster.Position;
					this.ConfigureStaticQuestObjectiveMonster(monster);

					this.SendStaticQuestObjectiveMonsterIfNeeded(monster, request);

					if (this.Character.Connection != null && this.StaticQuestMonsterMarkerEnabled(request.QuestClassName))
						Send.ZC_NORMAL.MinimapMarker(this.Character.Connection, monster, 1, 1, 0);
				}

				Log.Info("Static quest chain: spawned {0} fallback objective monster(s) '{1}' for quest '{2}' on map '{3}' layer {4} at {5:0.##}/{6:0.##}/{7:0.##}.", spawnCount, request.ClassName, request.QuestClassName, mapClassName, this.Character.Layer, request.X, request.Y, request.Z);
			}
		}

		private bool CanSendStaticQuestObjectiveActors()
		{
			if (this.Character?.Connection == null || this.Character.Map == null)
				return false;

			return !this.Character.IsWarping;
		}

		private void SendStaticQuestObjectiveMonsterIfNeeded(Mob monster, StaticQuestMonsterSpawnRequest request)
		{
			if (monster == null || this.Character?.Connection == null)
				return;

			if (!request.IsPrivateEncounter && this.Character.Layer == 0)
				return;

			var sentKey = $"Clover.StaticQuestObjective.EnterSent.{this.Character.ObjectId}";
			if (monster.Vars.GetBool(sentKey, false))
				return;

			monster.Vars.SetBool(sentKey, true);
			Send.ZC_ENTER_MONSTER(this.Character.Connection, monster);
		}

		private string GetStaticQuestObjectiveSpawnPendingKey(StaticQuestMonsterSpawnRequest request)
		{
			return string.Join(":",
				this.Character.ObjectId,
				this.Character.Layer,
				request.QuestClassName ?? "",
				request.MonsterId,
				request.IsPrivateEncounter ? "private" : "public",
				Math.Round(request.X),
				Math.Round(request.Y),
				Math.Round(request.Z)
			);
		}

		private bool StaticQuestObjectiveSpawnIsPending(string pendingKey)
		{
			if (!_staticQuestObjectiveSpawnPending.TryGetValue(pendingKey, out var pendingAt))
				return false;

			if (DateTime.UtcNow - pendingAt <= TimeSpan.FromSeconds(3))
				return true;

			_staticQuestObjectiveSpawnPending.Remove(pendingKey);
			return false;
		}

		private IEnumerable<Mob> GetStaticQuestObjectiveMonsters(StaticQuestMonsterSpawnRequest request, double radius)
		{
			if (this.Character?.Map == null || request == null)
				yield break;

			foreach (var monster in this.Character.Map.GetMonsters(monster =>
				this.StaticQuestObjectiveMonsterMatches(monster.Id, request.MonsterId) &&
				monster.Hp > 0 &&
				monster.Layer == this.Character.Layer &&
				monster is Mob mob &&
				this.StaticQuestObjectiveMonsterBelongsToRequest(mob, request, radius)))
			{
				if (monster is Mob mob)
					yield return mob;
			}
		}

		private bool StaticQuestObjectiveMonsterBelongsToRequest(Mob monster, StaticQuestMonsterSpawnRequest request, double radius)
		{
			if (monster == null)
				return false;

			if (!request.IsPrivateEncounter)
				return this.IsNearStaticQuestObjective(monster, request.X, request.Z, radius);

			if (monster.OwnerHandle == this.Character.Handle || monster.VisibilityId == this.Character.ObjectId)
				return true;

			return monster.OwnerHandle == 0 && this.IsNearStaticQuestObjective(monster, request.X, request.Z, radius);
		}

		private List<Mob> DeduplicateStaticQuestObjectiveMonsters(StaticQuestMonsterSpawnRequest request, List<Mob> existingMonsters)
		{
			if (!request.IsPrivateEncounter || existingMonsters.Count <= request.Count || this.Character?.Map == null)
				return existingMonsters;

			var keepCount = Math.Max(1, request.Count);
			var keep = existingMonsters
				.OrderBy(monster => monster.OwnerHandle == this.Character.Handle || monster.VisibilityId == this.Character.ObjectId ? 0 : 1)
				.ThenBy(monster => this.DistanceSquared2D(monster.Position.X, monster.Position.Z, this.Character.Position.X, this.Character.Position.Z))
				.Take(keepCount)
				.ToList();

			var extras = existingMonsters.Except(keep).ToList();
			foreach (var monster in extras)
			{
				if (this.Character.Connection != null)
					Send.ZC_LEAVE(this.Character.Connection, monster);

				this.Character.Map.RemoveMonster(monster);
			}

			Log.Info("Static quest chain: removed {0} duplicate private encounter monster(s) for quest '{1}' on map '{2}' layer {3}.", extras.Count, request.QuestClassName, this.Character.Map.ClassName, this.Character.Layer);
			return keep;
		}

		private void ConfigureStaticQuestObjectiveMonster(Mob monster)
		{
			if (monster == null || this.Character?.Map == null)
				return;

			if (!monster.Components.Has<MovementComponent>())
				monster.Components.Add(new MovementComponent(monster));

			if (!monster.Components.Has<AiComponent>())
			{
				var aiName = !string.IsNullOrWhiteSpace(monster.Data?.AiName)
					? monster.Data.AiName
					: "BasicMonster";

				if (!Melia.Zone.Scripting.AI.AiScript.Exists(aiName))
					aiName = "BasicMonster";

				monster.Components.Add(new AiComponent(monster, aiName));
			}

			monster.MonsterType = RelationType.Enemy;
			monster.Tendency = TendencyType.Aggressive;
			monster.SetTarget(this.Character);
			monster.InsertHate(this.Character, 5000);
			monster.FromGround = true;

			if (this.Character.Map.TryGetPropertyOverrides(monster.Id, out var propertyOverrides))
				monster.ApplyOverrides(propertyOverrides, syncClient: true);
		}

		private void SyncStaticQuestObjectiveMonsterMarkers(string mapClassName)
		{
			if (!this.CanSendStaticQuestObjectiveActors())
				return;

			foreach (var request in this.GetRelevantStaticMonsterSpawnRequests(mapClassName))
			{
				if (!this.StaticQuestMonsterMarkerEnabled(request.QuestClassName))
					continue;

				var radius = Math.Max(125, request.Range);
				var monsters = this.Character.Map.GetMonsters(monster =>
					this.StaticQuestObjectiveMonsterMatches(monster.Id, request.MonsterId) &&
					monster.Hp > 0 &&
					monster.Layer == this.Character.Layer &&
					this.IsNearStaticQuestObjective(monster, request.X, request.Z, radius));

				foreach (var monster in monsters)
					Send.ZC_NORMAL.MinimapMarker(this.Character.Connection, monster, 1, 1, 0);
			}
		}

		private bool StaticQuestMonsterMarkerEnabled(string questClassName)
		{
			return false;
		}

		private bool StaticQuestObjectiveMonsterMatches(int monsterId, int requestedMonsterId)
		{
			return Mob.IsSameMonsterFamily(monsterId, requestedMonsterId);
		}

		private bool StaticQuestMapPointTrackingDisabled(string questClassName)
		{
			return string.Equals(questClassName, "SIAUL_WEST_MEET_TITAS", StringComparison.OrdinalIgnoreCase);
		}

		private bool StaticQuestMonsterMarkerDisabled(string questClassName)
		{
			if (string.IsNullOrWhiteSpace(questClassName))
				return false;

			return questClassName.StartsWith("SIAUL_WEST_", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(questClassName, "SIAUL_EAST_RECLAIM1", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(questClassName, "TUTO_SKILL_RUN", StringComparison.OrdinalIgnoreCase);
		}

		private bool StaticQuestNativeTrackingDisabled(string questClassName)
		{
			return false;
		}

		private bool IsNearStaticQuestObjective(IMonster monster, double x, double z, double radius)
		{
			var dx = monster.Position.X - x;
			var dz = monster.Position.Z - z;
			return (dx * dx) + (dz * dz) <= radius * radius;
		}

		private double DistanceSquared2D(double ax, double az, double bx, double bz)
		{
			var dx = ax - bx;
			var dz = az - bz;
			return (dx * dx) + (dz * dz);
		}

		private void EnsureStaticQuestLayerState()
		{
			foreach (var quest in this.GetInProgress())
				this.EnsureStaticQuestLayerState(quest);
		}

		private void EnsureStaticQuestLayerState(Quest quest)
		{
			if (quest?.QuestStaticData == null || this.Character.Map == null)
				return;

			if (!quest.InProgress || !this.HasActiveStaticLayerObjective(quest))
				return;

			// Do not move generic static quests into private layers here. Papaya's
			// layer flow also controls NPC copies, hidden actors, and track state;
			// enabling only the layer flag in Clover hides the real field NPCs and
			// forces unsafe fallback actors into the map.
		}

		private bool HasActiveStaticLayerObjective(Quest quest)
		{
			var questStaticData = quest?.QuestStaticData;
			if (questStaticData?.Objectives == null)
				return false;

			foreach (var objectiveData in questStaticData.Objectives)
			{
				if (objectiveData == null || !objectiveData.Layer)
					continue;

				if (!quest.TryGetProgress(objectiveData.Ident, out var progress))
					continue;

				if (!progress.Done && progress.Unlocked)
					return true;
			}

			return this.HasActivePrivateEncounterObjective(quest);
		}

		private bool HasActivePrivateEncounterObjective(Quest quest)
		{
			var questStaticData = quest?.QuestStaticData;
			if (questStaticData?.Objectives == null)
				return false;

			var encounters = ZoneServer.Instance.Data.PrivateEncounterDb.FindByQuest(questStaticData.ClassName).ToList();
			if (encounters.Count == 0)
				return false;

			foreach (var objectiveData in questStaticData.Objectives)
			{
				if (objectiveData == null || !quest.TryGetProgress(objectiveData.Ident, out var progress))
					continue;

				if (progress.Done || !progress.Unlocked)
					continue;

				var target = this.GetStaticObjectiveMonsterTarget(objectiveData);
				if (this.IsNone(target))
					continue;

				if (encounters.Any(encounter => this.StaticObjectiveTargetMatchesEncounter(target, encounter.Target)))
					return true;
			}

			return false;
		}

		private bool HasPrivateEncounterObjective(Quest quest)
		{
			var questStaticData = quest?.QuestStaticData;
			if (questStaticData?.Objectives == null)
				return false;

			var encounters = ZoneServer.Instance.Data.PrivateEncounterDb.FindByQuest(questStaticData.ClassName).ToList();
			if (encounters.Count == 0)
				return false;

			foreach (var objectiveData in questStaticData.Objectives)
			{
				var target = this.GetStaticObjectiveMonsterTarget(objectiveData);
				if (!this.IsNone(target) && encounters.Any(encounter => this.StaticObjectiveTargetMatchesEncounter(target, encounter.Target)))
					return true;
			}

			return false;
		}

		private bool StaticObjectiveTargetMatchesEncounter(string objectiveTarget, string encounterTarget)
		{
			if (this.IsNone(objectiveTarget) || this.IsNone(encounterTarget))
				return false;

			var objectiveTargets = objectiveTarget.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			var encounterTargets = encounterTarget.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			return objectiveTargets.Any(objective => encounterTargets.Any(encounter => string.Equals(objective, encounter, StringComparison.OrdinalIgnoreCase)));
		}

		private void StopStaticQuestLayerIfDone(Quest quest)
		{
			if (quest?.QuestStaticData?.Objectives == null || this.Character.Layer == 0)
				return;

			if (!quest.QuestStaticData.Objectives.Any(objective => objective.Layer) && !this.HasPrivateEncounterObjective(quest))
				return;

			if (this.HasActiveStaticLayerObjective(quest))
				return;

			Log.Info("Static quest layer: returning '{0}' to normal layer after quest '{1}' objective completion.", this.Character.Name, quest.QuestStaticData.ClassName);
			this.Character.StopLayer();
		}

		private IEnumerable<StaticQuestMonsterSpawnRequest> GetRelevantStaticMonsterSpawnRequests(string mapClassName)
		{
			foreach (var quest in this.GetInProgress())
			{
				var questData = quest.QuestStaticData;
				if (questData?.Objectives == null)
					continue;

				if (!this.StaticQuestReferencesMap(questData, mapClassName))
					continue;

				foreach (var objectiveData in questData.Objectives)
				{
					if (objectiveData == null || !quest.TryGetProgress(objectiveData.Ident, out var progress) || progress.Done || !progress.Unlocked)
						continue;

					var target = this.GetStaticObjectiveMonsterTarget(objectiveData);
					if (string.IsNullOrWhiteSpace(target))
						continue;

					var privateEncounters = ZoneServer.Instance.Data.PrivateEncounterDb
						.FindByQuestAndMap(questData.ClassName, mapClassName)
						.ToList();
					if (privateEncounters.Count != 0)
					{
						var privateRequests = this.CreatePrivateEncounterMonsterSpawnRequests(privateEncounters, objectiveData, progress.Count, target, questData, mapClassName).ToList();
						if (privateRequests.Count != 0)
						{
							foreach (var request in privateRequests)
								yield return request;
							continue;
						}

						this.LogUnresolvedPrivateEncounterFallbackOnce(questData.ClassName, objectiveData.Ident, mapClassName);
					}

					if (!this.StaticQuestObjectiveReferencesMap(questData, mapClassName))
						continue;

					if (!this.TryResolveStaticObjectivePosition(questData, mapClassName, out var x, out var y, out var z, out var range))
					{
						x = this.Character.Position.X;
						y = this.Character.Position.Y;
						z = this.Character.Position.Z;
						range = 150;
					}

					var missingCount = Math.Max(1, Math.Min(objectiveData.Count - progress.Count, 5));
					foreach (var spawnTarget in this.DistributeStaticObjectiveMonsterSpawnBudget(target, missingCount))
					{
						yield return new StaticQuestMonsterSpawnRequest(spawnTarget.MonsterData.Id, spawnTarget.MonsterData.ClassName, spawnTarget.MonsterData.Name, questData.ClassName, x, y, z, range, spawnTarget.Count, false);
					}
				}
			}
		}

		private string GetStaticObjectiveMonsterTarget(Melia.Shared.Data.Database.QuestObjectiveStaticData objectiveData)
		{
			if (string.Equals(objectiveData.Type, "Kill", StringComparison.OrdinalIgnoreCase))
				return objectiveData.Target;

			if (string.Equals(objectiveData.Type, "Collect", StringComparison.OrdinalIgnoreCase))
				return objectiveData.DropTarget;

			return null;
		}

		private IEnumerable<StaticQuestMonsterSpawnRequest> CreatePrivateEncounterMonsterSpawnRequests(IEnumerable<Melia.Shared.Data.Database.PrivateEncounterData> encounters, Melia.Shared.Data.Database.QuestObjectiveStaticData objectiveData, int progressCount, string defaultTarget, QuestStaticData questData, string mapClassName)
		{
			foreach (var encounter in encounters)
			{
				var target = !this.IsNone(encounter.Target) ? encounter.Target : defaultTarget;
				var missingCount = Math.Max(0, objectiveData.Count - progressCount);
				if (missingCount <= 0)
					continue;

				var spawnBudget = Math.Min(missingCount, 5);
				var spawnChunkSize = Math.Max(1, encounter.MinSpawnCount);
				var resolvedPoints = this.ResolvePrivateEncounterSpawnPoints(encounter, questData, mapClassName).ToList();
				if (resolvedPoints.Count == 0)
					continue;

				foreach (var spawnTarget in this.DistributeStaticObjectiveMonsterSpawnBudget(target, spawnBudget))
				{
					var remaining = spawnTarget.Count;
					var pointIndex = 0;

					while (remaining > 0)
					{
						var point = resolvedPoints[pointIndex % resolvedPoints.Count];
						var count = Math.Min(remaining, spawnChunkSize);

						yield return new StaticQuestMonsterSpawnRequest(spawnTarget.MonsterData.Id, spawnTarget.MonsterData.ClassName, spawnTarget.MonsterData.Name, questData.ClassName, point.X, point.Y, point.Z, point.Range, count, true);

						remaining -= count;
						pointIndex++;
					}
				}
			}
		}

		private IEnumerable<StaticQuestLocationPoint> ResolvePrivateEncounterSpawnPoints(Melia.Shared.Data.Database.PrivateEncounterData encounter, QuestStaticData questData, string mapClassName)
		{
			foreach (var mapPointGroup in encounter.MapPointGroup ?? Enumerable.Empty<string>())
			{
				if (this.TryResolveStaticPositionFromLocation(mapPointGroup, mapClassName, out var x, out var y, out var z, out var range))
				{
					yield return new StaticQuestLocationPoint(x, y, z, range);
					continue;
				}

				if (this.TryParseUnresolvedStaticAnchor(mapPointGroup, mapClassName, out var anchorName, out _))
					this.LogUnresolvedPrivateEncounterAnchorOnce(questData?.ClassName, anchorName, mapClassName);
			}
		}

		private void LogUnresolvedPrivateEncounterAnchorOnce(string questClassName, string anchorName, string mapClassName)
		{
			var tempKey = $"Clover.StaticQuest.UnresolvedPrivateAnchor.{questClassName}.{anchorName}.{mapClassName}";
			if (this.Character?.Variables.Temp.GetBool(tempKey, false) == true)
				return;

			this.Character?.Variables.Temp.SetBool(tempKey, true);
			Log.Info("Static quest chain: skipped unresolved private encounter anchor '{0}' for quest '{1}' on map '{2}' to keep client map-entry stable.", anchorName, questClassName, mapClassName);
		}

		private void LogUnresolvedPrivateEncounterFallbackOnce(string questClassName, string objectiveIdent, string mapClassName)
		{
			var tempKey = $"Clover.StaticQuest.PrivateEncounterFallback.{questClassName}.{objectiveIdent}.{mapClassName}";
			if (this.Character?.Variables.Temp.GetBool(tempKey, false) == true)
				return;

			this.Character?.Variables.Temp.SetBool(tempKey, true);
			Log.Info("Static quest chain: private encounter for quest '{0}' objective '{1}' on map '{2}' had no resolved spawn points; using generic objective fallback.", questClassName, objectiveIdent, mapClassName);
		}

		private bool TryParseUnresolvedStaticAnchor(string mapPointGroup, string mapClassName, out string anchorName, out double range)
		{
			anchorName = null;
			range = 150;

			if (string.IsNullOrWhiteSpace(mapPointGroup) || string.IsNullOrWhiteSpace(mapClassName))
				return false;

			var parts = mapPointGroup.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			for (var i = 0; i < parts.Length; i++)
			{
				if (!string.Equals(parts[i], mapClassName, StringComparison.OrdinalIgnoreCase))
					continue;

				if (i + 1 >= parts.Length || double.TryParse(parts[i + 1], out _))
					continue;

				anchorName = parts[i + 1];
				if (i + 2 < parts.Length && double.TryParse(parts[i + 2], out var parsedRange) && parsedRange > 0)
					range = parsedRange;
				return true;
			}

			return false;
		}

		private IEnumerable<Melia.Shared.Data.Database.MonsterData> ResolveStaticObjectiveMonsterTargets(string target)
		{
			foreach (var className in target
				.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Distinct(StringComparer.OrdinalIgnoreCase))
			{
				if (string.Equals(className, "ALL", StringComparison.OrdinalIgnoreCase))
					continue;

				if (ZoneServer.Instance.Data.MonsterDb.TryFind(className, out var monsterData))
					yield return monsterData;
			}
		}

		private IEnumerable<(Melia.Shared.Data.Database.MonsterData MonsterData, int Count)> DistributeStaticObjectiveMonsterSpawnBudget(string target, int spawnBudget)
		{
			if (spawnBudget <= 0)
				yield break;

			var monsters = this.ResolveStaticObjectiveMonsterTargets(target).ToList();
			if (monsters.Count == 0)
				yield break;

			var counts = new int[monsters.Count];
			for (var i = 0; i < spawnBudget; i++)
				counts[i % monsters.Count]++;

			for (var i = 0; i < monsters.Count; i++)
			{
				if (counts[i] > 0)
					yield return (monsters[i], counts[i]);
			}
		}

		private void EnsureStaticQuestNpcActors(string mapClassName)
		{
			var existingDialogLayers = this.Character.Map
				.GetNpcs(a => a is Npc npc && !string.IsNullOrWhiteSpace(npc.DialogName))
				.OfType<Npc>()
				.GroupBy(npc => this.GetStaticNpcLayerKey(npc.DialogName, npc.Layer), StringComparer.OrdinalIgnoreCase)
				.ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

			foreach (var request in this.GetRelevantStaticNpcSpawnRequests(mapClassName))
			{
				var layerKey = this.GetStaticNpcLayerKey(request.DialogName, this.Character.Layer);
				if (existingDialogLayers.TryGetValue(layerKey, out var existingNpc))
				{
					if (this.Character.Layer == 0 && this.Character.GetMapNPCState(existingNpc) == NpcState.Invisible)
					{
						this.Character.SetMapNPCState(existingNpc, NpcState.Normal);
						Log.Info("Static quest chain: revealed existing NPC '{0}' for quest '{1}' on map '{2}' layer {3}.", request.DialogName, request.QuestClassName, mapClassName, this.Character.Layer);
					}
					else
					{
						this.RefreshVisibleStaticQuestNpc(existingNpc, this.Character.GetMapNPCState(existingNpc));
					}
					continue;
				}

				var modelId = this.ResolveStaticQuestNpcMonsterId(request.DialogName);
				var npc = this.Character.Layer == 0
					? Shortcuts.AddNpc(0, modelId, request.Name, mapClassName, request.X, request.Y, request.Z, 0, request.DialogName, state: (int)NpcState.Invisible, range: request.Range)
					: Shortcuts.AddNpc(this.Character, modelId, request.Name, mapClassName, request.X, request.Y, request.Z, 0, request.DialogName, state: (int)NpcState.Normal, range: request.Range);

				if (this.Character.Layer == 0)
					this.Character.SetMapNPCState(npc, NpcState.Normal);
				else if (this.Character.Connection != null)
					Send.ZC_ENTER_MONSTER(this.Character.Connection, npc);

				existingDialogLayers[layerKey] = npc;

				Log.Info("Static quest chain: spawned fallback NPC '{0}' for quest '{1}' on map '{2}' layer {3} at {4:0.##}/{5:0.##}/{6:0.##}.", request.DialogName, request.QuestClassName, mapClassName, this.Character.Layer, request.X, request.Y, request.Z);
			}
		}

		private void EnsureWestSiauliaiPersonalQuestActors(string mapClassName)
		{
			if (!string.Equals(mapClassName, "f_siauliai_west", StringComparison.OrdinalIgnoreCase))
				return;

			this.EnsurePersonalStaticQuestNpc(
				"SIAUL_WEST_LAIMONAS",
				"SIAUL_WEST_LAIMONAS1",
				1015,
				20117,
				"Laimonas",
				1751,
				285,
				349);
		}

		private void EnsurePersonalStaticQuestNpc(string dialogName, string questClassName, long questId, int monsterId, string name, double x, double y, double z)
		{
			if (this.Character?.Connection == null)
				return;

			var shouldExist =
				this.IsActive(questId) ||
				(questId == 1015 && this.HasCompleted(1013) && !this.HasCompleted(1015));
			if (!shouldExist)
				return;

			var tempKey = $"Clover.StaticQuestNpcSpawned.{dialogName}.{this.Character.MapId}.{this.Character.Layer}";
			if (!this.Character.Variables.Temp.GetBool(tempKey, false))
			{
				var genType = 1500000 + (int)(this.Character.ObjectId % 100000);
				var npc = Shortcuts.AddNpc(genType, monsterId, name, this.Character.Map.ClassName, x, y, z, 90, dialogName, state: (int)NpcState.Highlighted, range: 120);
				npc.SetVisibilty(ActorVisibility.Individual, this.Character.ObjectId);
				this.Character.Variables.Temp.SetBool(tempKey, true);
				this.Character.SetMapNPCState(npc, NpcState.Highlighted);
				Log.Info("Static quest chain: spawned personal NPC '{0}' for quest '{1}' for '{2}' on map '{3}' layer {4} at {5:0.##}/{6:0.##}/{7:0.##}.", dialogName, questClassName, this.Character.Name, this.Character.Map.ClassName, this.Character.Layer, x, y, z);
				return;
			}
		}

		private string GetStaticNpcLayerKey(string dialogName, int layer)
			=> $"{dialogName}:{layer}";

		private IEnumerable<StaticQuestNpcSpawnRequest> GetRelevantStaticNpcSpawnRequests(string mapClassName)
		{
			foreach (var questData in ZoneServer.Instance.Data.QuestDb.GetList().OrderBy(a => a.Id))
			{
				if (!this.StaticQuestReferencesMap(questData, mapClassName))
					continue;

				var questId = new QuestId(questData.Id);
				if (this.TryGetById(questId, out var quest))
				{
					if (quest.Status == QuestStatus.Completed || quest.Status == QuestStatus.Abandoned)
						continue;

					if (quest.Status == QuestStatus.Success)
					{
						if (this.TryCreateStaticNpcSpawnRequest(questData, questData.EndNPC, mapClassName, out var endRequest))
							yield return endRequest;
						continue;
					}

					if (quest.InProgress)
					{
						if (this.TryCreateStaticNpcSpawnRequest(questData, questData.ProgNPC, mapClassName, out var progressRequest))
							yield return progressRequest;
						if (this.TryCreateStaticNpcSpawnRequest(questData, questData.EndNPC, mapClassName, out var endRequest))
							yield return endRequest;
						continue;
					}
				}

				if (string.Equals(questData.QuestStartMode, "NPCDIALOG", StringComparison.OrdinalIgnoreCase) &&
					string.Equals(questData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase) &&
					!this.StaticQuestDisabledForCloverFlow(questData) &&
					!this.Has(questId) &&
					!this.StaticQuestIsBlockedByEarlierWestSiauliaiMain(questData) &&
					!this.StaticQuestIsBlockedByPapayaMainProgression(questData) &&
					this.MeetsStaticPrerequisites(questData) &&
					this.TryCreateStaticNpcSpawnRequest(questData, questData.StartNPC, mapClassName, out var startRequest))
				{
					yield return startRequest;
				}
			}
		}

		private bool TryCreateStaticNpcSpawnRequest(QuestStaticData questData, string dialogName, string mapClassName, out StaticQuestNpcSpawnRequest request)
		{
			request = default;

			if (string.IsNullOrWhiteSpace(dialogName))
				return false;

			if (this.IsStaticQuestAnchorNpcRole(questData, dialogName))
				return false;

			if (!this.StaticQuestNpcBelongsToMap(questData, dialogName, mapClassName))
				return false;

			if (!this.TryResolveStaticNpcPosition(questData, dialogName, mapClassName, out var x, out var y, out var z, out var range))
			{
				x = this.Character.Position.X;
				y = this.Character.Position.Y;
				z = this.Character.Position.Z;
				range = 100;
			}

			request = new StaticQuestNpcSpawnRequest(dialogName, questData.ClassName, this.ResolveStaticQuestNpcName(dialogName), x, y, z, range);
			return true;
		}

		private bool IsStaticQuestAnchorNpcRole(QuestStaticData questData, string dialogName)
		{
			if (questData == null || string.IsNullOrWhiteSpace(dialogName))
				return true;

			if (string.Equals(dialogName, questData.ClassName, StringComparison.OrdinalIgnoreCase))
				return true;

			if (dialogName.EndsWith("_TRACK", StringComparison.OrdinalIgnoreCase))
				return true;

			return false;
		}

		private bool StaticQuestNpcBelongsToMap(QuestStaticData questData, string dialogName, string mapClassName)
		{
			return this.StaticQuestNpcRoleBelongsToMap(questData.StartNPC, questData.StartMap, questData.StartLocation, dialogName, mapClassName) ||
				this.StaticQuestNpcRoleBelongsToMap(questData.ProgNPC, questData.ProgMap, questData.ProgLocation, dialogName, mapClassName) ||
				this.StaticQuestNpcRoleBelongsToMap(questData.EndNPC, questData.EndMap, questData.EndLocation, dialogName, mapClassName);
		}

		private bool StaticQuestNpcRoleBelongsToMap(string roleNpc, string roleMap, string roleLocation, string dialogName, string mapClassName)
		{
			if (!this.StaticNpcDialogMatches(roleNpc, dialogName))
				return false;

			return this.StaticQuestMapMatches(roleMap, mapClassName) ||
				this.StaticQuestLocationReferencesNpcOnMap(roleLocation, dialogName, mapClassName);
		}

		private bool StaticQuestScriptExists(QuestId questId)
		{
			if (QuestScript.Exists(questId))
				return true;

			return this.TryGetNamespacedQuestId(questId.Value, out var namespacedQuestId) &&
				QuestScript.Exists(namespacedQuestId);
		}

		private bool TryGetNamespacedQuestId(long questId, out QuestId namespacedQuestId)
		{
			namespacedQuestId = default;

			if (questId < 1 || questId > 0xFFFF)
				return false;

			namespacedQuestId = new QuestId("Laima.Quest", questId);
			return true;
		}

		private bool StaticQuestLocationReferencesNpcOnMap(string location, string dialogName, string mapClassName)
		{
			if (string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(dialogName) || string.IsNullOrWhiteSpace(mapClassName))
				return false;

			var parts = location.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			for (var i = 0; i < parts.Length; i++)
			{
				if (!string.Equals(parts[i], mapClassName, StringComparison.OrdinalIgnoreCase))
					continue;

				if (i + 1 < parts.Length && this.StaticNpcDialogMatches(parts[i + 1], dialogName))
					return true;
			}

			return false;
		}

		private bool TryResolveStaticNpcPosition(QuestStaticData questData, string dialogName, string mapClassName, out double x, out double y, out double z, out double range)
		{
			if (string.Equals(mapClassName, "f_siauliai_west", StringComparison.OrdinalIgnoreCase) &&
				string.Equals(dialogName, "SIAUL_WEST_CAMP_MANAGER", StringComparison.OrdinalIgnoreCase))
			{
				x = -576;
				y = 260;
				z = -719;
				range = 100;
				return true;
			}

			if (string.Equals(mapClassName, "f_siauliai_west", StringComparison.OrdinalIgnoreCase) &&
				string.Equals(dialogName, "SIALUL_WEST_DRASIUS", StringComparison.OrdinalIgnoreCase))
			{
				x = -1121;
				y = 260;
				z = -528;
				range = 100;
				return true;
			}

			if (string.Equals(mapClassName, "f_siauliai_west", StringComparison.OrdinalIgnoreCase) &&
				string.Equals(dialogName, "SIAUL_WEST_NAGLIS2", StringComparison.OrdinalIgnoreCase))
			{
				x = -1490;
				y = 260;
				z = -140;
				range = 100;
				return true;
			}

			if (string.Equals(mapClassName, "f_siauliai_west", StringComparison.OrdinalIgnoreCase) &&
				string.Equals(dialogName, "SIAUL_WEST_SOL3", StringComparison.OrdinalIgnoreCase))
			{
				x = -663;
				y = 322;
				z = 503;
				range = 100;
				return true;
			}

			if (string.Equals(mapClassName, "f_siauliai_west", StringComparison.OrdinalIgnoreCase) &&
				string.Equals(dialogName, "SIAUL_WEST_LAIMONAS", StringComparison.OrdinalIgnoreCase))
			{
				x = 1751;
				y = 285;
				z = 349;
				range = 100;
				return true;
			}

			if (string.Equals(mapClassName, "f_siauliai_west", StringComparison.OrdinalIgnoreCase) &&
				string.Equals(dialogName, "SIAUL_ST1_ST2", StringComparison.OrdinalIgnoreCase))
			{
				x = 1880;
				y = 210;
				z = -1175;
				range = 100;
				return true;
			}

			if (this.TryResolveLiveStaticNpcPosition(dialogName, mapClassName, out x, out y, out z, out range))
				return true;

			if (this.TryResolvePapayaCapturedStaticNpcPosition(mapClassName, dialogName, out x, out y, out z, out range))
				return true;

			if (this.TryResolveStaticNpcPositionFromLocation(questData.StartLocation, dialogName, mapClassName, out x, out y, out z, out range))
				return true;
			if (this.TryResolveStaticNpcPositionFromLocation(questData.ProgLocation, dialogName, mapClassName, out x, out y, out z, out range))
				return true;
			if (this.TryResolveStaticNpcPositionFromLocation(questData.EndLocation, dialogName, mapClassName, out x, out y, out z, out range))
				return true;

			x = 0;
			y = 0;
			z = 0;
			range = 100;
			return false;
		}

		private bool TryResolvePapayaCapturedStaticNpcPosition(string mapClassName, string dialogName, out double x, out double y, out double z, out double range)
		{
			x = 0;
			y = 0;
			z = 0;
			range = 100;

			if (string.Equals(mapClassName, "c_Klaipe", StringComparison.OrdinalIgnoreCase))
			{
				if (string.Equals(dialogName, "KLAPEDA_USKA", StringComparison.OrdinalIgnoreCase))
				{
					x = -467.28;
					y = 148.67;
					z = 114.01;
					range = 100;
					return true;
				}

				if (string.Equals(dialogName, "EMILIA", StringComparison.OrdinalIgnoreCase))
				{
					x = 510.7029;
					y = -349.3194;
					z = 90.0;
					range = 100;
					return true;
				}

				if (string.Equals(dialogName, "ALFONSO", StringComparison.OrdinalIgnoreCase))
				{
					x = 269;
					y = -611;
					z = 90;
					range = 100;
					return true;
				}

				if (string.Equals(dialogName, "AKALABETH", StringComparison.OrdinalIgnoreCase))
				{
					x = 394;
					y = -475;
					z = 90;
					range = 100;
					return true;
				}

				if (string.Equals(dialogName, "WARP_C_KLAIPE", StringComparison.OrdinalIgnoreCase))
				{
					x = -206.574;
					y = 148.8251;
					z = 98.63973;
					range = 100;
					return true;
				}

				if (string.Equals(dialogName, "WS_KLAPEDA_SIAULST2", StringComparison.OrdinalIgnoreCase))
				{
					x = 799;
					y = 148.8251;
					z = 331;
					range = 100;
					return true;
				}

				if (string.Equals(dialogName, "WS_KLAPEDA_SIAULST1", StringComparison.OrdinalIgnoreCase))
				{
					x = -194.36829;
					y = 148.8251;
					z = -1172.699;
					range = 100;
					return true;
				}
			}

			if (string.Equals(mapClassName, "f_siauliai_2", StringComparison.OrdinalIgnoreCase))
			{
				if (string.Equals(dialogName, "SIAUL_EAST_MANAGER", StringComparison.OrdinalIgnoreCase))
				{
					x = 167;
					y = 151;
					z = 697;
					return true;
				}

				if (string.Equals(dialogName, "SIAUL_EAST_SUPPLY_MANAGER2", StringComparison.OrdinalIgnoreCase))
				{
					x = -1290;
					y = 130;
					z = 928;
					return true;
				}

				if (string.Equals(dialogName, "SIAUL_EAST_SOLDIER8", StringComparison.OrdinalIgnoreCase))
				{
					x = 1242;
					y = 130;
					z = 339;
					return true;
				}

				if (string.Equals(dialogName, "SIAUL_EAST_REQUEST6", StringComparison.OrdinalIgnoreCase))
				{
					x = 2008;
					y = 185;
					z = -389;
					range = 250;
					return true;
				}
			}

			if (string.Equals(mapClassName, "f_siauliai_out", StringComparison.OrdinalIgnoreCase))
			{
				if (string.Equals(dialogName, "SIAULIAIOUT_Q01", StringComparison.OrdinalIgnoreCase))
				{
					x = 275;
					y = 157;
					z = -1262;
					range = 300;
					return true;
				}

				if (string.Equals(dialogName, "SIAULIAIOUT_CHIEF_A", StringComparison.OrdinalIgnoreCase))
				{
					x = 335;
					y = 157;
					z = -1215;
					return true;
				}

				if (string.Equals(dialogName, "SIAULIAIOUT_ALCHE", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(dialogName, "SIAULIAIOUT_ALCHE_A", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(dialogName, "SIAULIAIOUT_PREAL", StringComparison.OrdinalIgnoreCase))
				{
					x = 520;
					y = 157;
					z = -1135;
					return true;
				}

				if (string.Equals(dialogName, "SIAULIAIOUT_BLOCK", StringComparison.OrdinalIgnoreCase))
				{
					x = -61;
					y = 157;
					z = -656;
					range = 180;
					return true;
				}
			}

			if (string.Equals(mapClassName, "d_cmine_6", StringComparison.OrdinalIgnoreCase))
			{
				if (string.Equals(dialogName, "MINE_3_ALCHEMIST", StringComparison.OrdinalIgnoreCase))
				{
					x = -250;
					y = 65;
					z = -1320;
					range = 400;
					return true;
				}

				if (string.Equals(dialogName, "MINE_3_RESIENT1", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(dialogName, "MINE_3_RESIENT1_BIND", StringComparison.OrdinalIgnoreCase))
				{
					x = 860;
					y = 65;
					z = -620;
					range = 100;
					return true;
				}

				if (string.Equals(dialogName, "CMINE6_TO_KATYN7_1_START", StringComparison.OrdinalIgnoreCase))
				{
					x = 2070;
					y = 63;
					z = 1580;
					range = 175;
					return true;
				}

				if (string.Equals(dialogName, "CMINE3_BOSSROOM_OPEN", StringComparison.OrdinalIgnoreCase))
				{
					x = 2050;
					y = 63;
					z = 1600;
					range = 200;
					return true;
				}
			}

			if (string.Equals(mapClassName, "f_gele_57_2", StringComparison.OrdinalIgnoreCase) &&
				string.Equals(dialogName, "GELE572_MQ_01", StringComparison.OrdinalIgnoreCase))
			{
				x = 13;
				y = 419;
				z = -983;
				range = 100;
				return true;
			}

			if (string.Equals(mapClassName, "f_gele_57_3", StringComparison.OrdinalIgnoreCase))
			{
				if (string.Equals(dialogName, "GELE573_MASTER", StringComparison.OrdinalIgnoreCase))
				{
					x = 871;
					y = -68;
					z = -514;
					range = 150;
					return true;
				}

				if (string.Equals(dialogName, "GELE573_MQ_07_F", StringComparison.OrdinalIgnoreCase))
				{
					x = 805;
					y = -68;
					z = -450;
					range = 100;
					return true;
				}
			}

			if (string.Equals(mapClassName, "f_gele_57_4", StringComparison.OrdinalIgnoreCase))
			{
				if (string.Equals(dialogName, "GELE574_ALLGES", StringComparison.OrdinalIgnoreCase))
				{
					x = -833;
					y = -80;
					z = -48;
					range = 150;
					return true;
				}

				if (string.Equals(dialogName, "GELE574_ARUNE_1", StringComparison.OrdinalIgnoreCase))
				{
					x = 1125;
					y = -78;
					z = 1975;
					range = 150;
					return true;
				}
			}

			if (string.Equals(mapClassName, "d_chapel_57_5", StringComparison.OrdinalIgnoreCase))
			{
				if (string.Equals(dialogName, "CHAPEL_TOMAS", StringComparison.OrdinalIgnoreCase))
				{
					x = -1258;
					y = 1;
					z = 1095;
					range = 150;
					return true;
				}

				if (string.Equals(dialogName, "CHAPLE575_MQ_04", StringComparison.OrdinalIgnoreCase))
				{
					x = 300;
					y = 1;
					z = -300;
					range = 130;
					return true;
				}

				if (string.Equals(dialogName, "CHAPEL_VIDAS", StringComparison.OrdinalIgnoreCase))
				{
					x = -1120;
					y = 1;
					z = 980;
					range = 125;
					return true;
				}

				if (string.Equals(dialogName, "CHAPLE575_MQ_09", StringComparison.OrdinalIgnoreCase))
				{
					x = 500;
					y = 1;
					z = -500;
					range = 100;
					return true;
				}
			}

			if (string.Equals(mapClassName, "d_chapel_57_6", StringComparison.OrdinalIgnoreCase))
			{
				if (string.Equals(dialogName, "CHAPEL_VIRGINIJA", StringComparison.OrdinalIgnoreCase))
				{
					x = 746;
					y = 0;
					z = -251;
					range = 125;
					return true;
				}

				if (string.Equals(dialogName, "CHAPLE576_MQ_04", StringComparison.OrdinalIgnoreCase))
				{
					x = 298;
					y = -78;
					z = -110;
					range = 150;
					return true;
				}

				if (string.Equals(dialogName, "CHAPEL576_DONATAS", StringComparison.OrdinalIgnoreCase))
				{
					x = -536;
					y = 0;
					z = 1563;
					range = 125;
					return true;
				}

				if (string.Equals(dialogName, "CHAPLE576_MQ_09", StringComparison.OrdinalIgnoreCase))
				{
					x = 259;
					y = 0;
					z = 427;
					range = 125;
					return true;
				}
			}

			if (string.Equals(mapClassName, "f_huevillage_58_1", StringComparison.OrdinalIgnoreCase))
			{
				if (string.Equals(dialogName, "HUEVILLAGE_58_1_MQ11_TRIGGER", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(dialogName, "HUEVILLAGE_58_3_MQ04_TO_HUE1", StringComparison.OrdinalIgnoreCase))
				{
					x = 975;
					y = 97;
					z = 951;
					range = 150;
					return true;
				}
			}

			if (string.Equals(mapClassName, "c_voodoo", StringComparison.OrdinalIgnoreCase) &&
				string.Equals(dialogName, "MASTER_BOCORS", StringComparison.OrdinalIgnoreCase))
			{
				x = 0;
				y = 0;
				z = 0;
				range = 100;
				return true;
			}

			return false;
		}

		private bool TryResolveStaticObjectivePosition(QuestStaticData questData, string mapClassName, out double x, out double y, out double z, out double range)
		{
			if (this.TryResolveStaticPositionFromLocation(questData.ProgLocation, mapClassName, out x, out y, out z, out range))
				return true;
			if (this.TryResolveStaticPositionFromLocation(questData.EndLocation, mapClassName, out x, out y, out z, out range))
				return true;
			if (this.TryResolveStaticPositionFromLocation(questData.StartLocation, mapClassName, out x, out y, out z, out range))
				return true;

			x = 0;
			y = 0;
			z = 0;
			range = 100;
			return false;
		}

		private bool TryResolveStaticPositionFromLocation(string location, string mapClassName, out double x, out double y, out double z, out double range)
		{
			x = 0;
			y = 0;
			z = 0;
			range = 100;

			if (string.IsNullOrWhiteSpace(location))
				return false;

			var parts = location.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			for (var i = 0; i < parts.Length; i++)
			{
				if (!string.Equals(parts[i], mapClassName, StringComparison.OrdinalIgnoreCase))
					continue;

				if (i + 4 < parts.Length &&
					double.TryParse(parts[i + 1], out x) &&
					double.TryParse(parts[i + 2], out y) &&
					double.TryParse(parts[i + 3], out z))
				{
					double.TryParse(parts[i + 4], out range);
					if (range <= 0)
						range = 100;
					return true;
				}

				if (i + 2 < parts.Length)
				{
					var anchorName = parts[i + 1];
					if (!double.TryParse(parts[i + 2], out range) || range <= 0)
						range = 100;

					if (this.TryResolveLiveStaticNpcPosition(anchorName, mapClassName, out x, out y, out z, out var liveRange))
					{
						if (liveRange > 0)
							range = Math.Max(range, liveRange);
						return true;
					}

					if (this.TryResolvePapayaCapturedStaticNpcPosition(mapClassName, anchorName, out x, out y, out z, out var capturedRange))
					{
						if (capturedRange > 0)
							range = capturedRange;
						return true;
					}

					if (this.TryResolveStaticNamedAnchor(anchorName, out x, out y, out z))
						return true;
				}
			}

			return false;
		}

		private bool TryResolveLiveStaticNpcPosition(string dialogName, string mapClassName, out double x, out double y, out double z, out double range)
		{
			x = 0;
			y = 0;
			z = 0;
			range = 100;

			if (string.IsNullOrWhiteSpace(dialogName) ||
				string.IsNullOrWhiteSpace(mapClassName) ||
				this.Character?.Map == null ||
				!string.Equals(this.Character.Map.ClassName, mapClassName, StringComparison.OrdinalIgnoreCase))
				return false;

			var npc = this.Character.Map.GetNpcs(a =>
					a is Npc candidate &&
					(this.StaticNpcDialogMatches(candidate.DialogName, dialogName) ||
					 string.Equals(candidate.UniqueName, dialogName, StringComparison.OrdinalIgnoreCase) ||
					 string.Equals(candidate.Name, dialogName, StringComparison.OrdinalIgnoreCase)))
				.OfType<Npc>()
				.OrderBy(candidate =>
				{
					var dx = candidate.Position.X - this.Character.Position.X;
					var dz = candidate.Position.Z - this.Character.Position.Z;
					return (dx * dx) + (dz * dz);
				})
				.FirstOrDefault();
			if (npc == null)
			{
				var warp = this.Character.Map.GetWarps(candidate =>
						string.Equals(candidate.WarpName, dialogName, StringComparison.OrdinalIgnoreCase) ||
						string.Equals(candidate.UniqueName, dialogName, StringComparison.OrdinalIgnoreCase) ||
						string.Equals(candidate.Name, dialogName, StringComparison.OrdinalIgnoreCase))
					.OrderBy(candidate =>
					{
						var dx = candidate.Position.X - this.Character.Position.X;
						var dz = candidate.Position.Z - this.Character.Position.Z;
						return (dx * dx) + (dz * dz);
					})
					.FirstOrDefault();
				if (warp == null)
					return false;

				x = warp.Position.X;
				y = warp.Position.Y;
				z = warp.Position.Z;
				range = 125;
				return true;
			}

			x = npc.Position.X;
			y = npc.Position.Y;
			z = npc.Position.Z;
			return true;
		}

		private bool TryResolveStaticNamedAnchor(string anchorName, out double x, out double y, out double z)
		{
			x = 0;
			y = 0;
			z = 0;

			if (string.IsNullOrWhiteSpace(anchorName) || this.Character.Map == null)
				return false;

			var npc = this.Character.Map.GetNpcs(a =>
					a is Npc candidate &&
					(string.Equals(candidate.DialogName, anchorName, StringComparison.OrdinalIgnoreCase) ||
					 string.Equals(candidate.UniqueName, anchorName, StringComparison.OrdinalIgnoreCase) ||
					 string.Equals(candidate.Name, anchorName, StringComparison.OrdinalIgnoreCase)))
				.OfType<Npc>()
				.FirstOrDefault();
			if (npc == null)
			{
				var warp = this.Character.Map.GetWarps(candidate =>
						string.Equals(candidate.WarpName, anchorName, StringComparison.OrdinalIgnoreCase) ||
						string.Equals(candidate.UniqueName, anchorName, StringComparison.OrdinalIgnoreCase) ||
						string.Equals(candidate.Name, anchorName, StringComparison.OrdinalIgnoreCase))
					.FirstOrDefault();
				if (warp == null)
					return false;

				x = warp.Position.X;
				y = warp.Position.Y;
				z = warp.Position.Z;
				return true;
			}

			x = npc.Position.X;
			y = npc.Position.Y;
			z = npc.Position.Z;
			return true;
		}

		private bool TryResolveStaticNpcPositionFromLocation(string location, string dialogName, string mapClassName, out double x, out double y, out double z, out double range)
		{
			x = 0;
			y = 0;
			z = 0;
			range = 100;

			if (string.IsNullOrWhiteSpace(location))
				return false;

			var parts = location.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			for (var i = 0; i < parts.Length; i++)
			{
				if (!string.Equals(parts[i], mapClassName, StringComparison.OrdinalIgnoreCase))
					continue;

				if (i + 4 < parts.Length &&
					double.TryParse(parts[i + 1], out x) &&
					double.TryParse(parts[i + 2], out y) &&
					double.TryParse(parts[i + 3], out z))
				{
					double.TryParse(parts[i + 4], out range);
					if (range <= 0)
						range = 100;
					return true;
				}

				if (i + 2 < parts.Length && this.StaticNpcDialogMatches(parts[i + 1], dialogName))
				{
					double.TryParse(parts[i + 2], out range);
					if (range <= 0)
						range = 100;
					return false;
				}
			}

			return false;
		}

		private int ResolveStaticQuestNpcMonsterId(string dialogName)
		{
			if (string.IsNullOrWhiteSpace(dialogName))
				return 20117;

			if (string.Equals(dialogName, "KLAPEDA_USKA", StringComparison.OrdinalIgnoreCase))
				return 20113;

			if (string.Equals(dialogName, "EMILIA", StringComparison.OrdinalIgnoreCase))
				return 20115;

			if (string.Equals(dialogName, "ALFONSO", StringComparison.OrdinalIgnoreCase))
				return 20104;

			if (string.Equals(dialogName, "AKALABETH", StringComparison.OrdinalIgnoreCase))
				return 20111;

			if (string.Equals(dialogName, "SIAUL_EAST_MANAGER", StringComparison.OrdinalIgnoreCase))
				return 20125;

			if (string.Equals(dialogName, "SIAUL_EAST_SUPPLY_MANAGER2", StringComparison.OrdinalIgnoreCase))
				return 20014;

			if (string.Equals(dialogName, "SIAUL_EAST_SOLDIER8", StringComparison.OrdinalIgnoreCase))
				return 10032;

			if (string.Equals(dialogName, "SIAULIAIOUT_Q01", StringComparison.OrdinalIgnoreCase))
				return 20026;

			if (string.Equals(dialogName, "SIAULIAIOUT_CHIEF_A", StringComparison.OrdinalIgnoreCase))
				return 20118;

			if (string.Equals(dialogName, "SIAULIAIOUT_ALCHE", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "SIAULIAIOUT_ALCHE_A", StringComparison.OrdinalIgnoreCase))
				return 20110;

			if (string.Equals(dialogName, "SIAULIAIOUT_PREAL", StringComparison.OrdinalIgnoreCase))
				return 20026;

			if (string.Equals(dialogName, "SIAULIAIOUT_BLOCK", StringComparison.OrdinalIgnoreCase))
				return 40095;

			if (string.Equals(dialogName, "MINE_1_ALCHEMIST", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "MINE_3_ALCHEMIST", StringComparison.OrdinalIgnoreCase))
				return 20110;

			if (string.Equals(dialogName, "MINE_3_RESIENT1_BIND", StringComparison.OrdinalIgnoreCase))
				return 151009;

			if (string.Equals(dialogName, "MINE_3_RESIENT1", StringComparison.OrdinalIgnoreCase))
				return 20150;

			if (string.Equals(dialogName, "CMINE6_TO_KATYN7_1_START", StringComparison.OrdinalIgnoreCase))
				return 47233;

			if (string.Equals(dialogName, "GELE572_MQ_01", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "GELE573_MASTER", StringComparison.OrdinalIgnoreCase))
				return 57223;

			if (string.Equals(dialogName, "GELE573_MQ_07_F", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "GELE574_ALLGES", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "GELE574_ARUNE_1", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "CHAPEL_TOMAS", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "CHAPEL_VIDAS", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "CHAPEL_VIRGINIJA", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "CHAPEL576_DONATAS", StringComparison.OrdinalIgnoreCase))
				return 147390;

			if (string.Equals(dialogName, "MASTER_BOCORS", StringComparison.OrdinalIgnoreCase))
				return 20136;

			if (string.Equals(dialogName, "SIAUL_WEST_CAMP_MANAGER", StringComparison.OrdinalIgnoreCase))
				return 20107;

			if (string.Equals(dialogName, "SIALUL_WEST_DRASIUS", StringComparison.OrdinalIgnoreCase))
				return 10032;

			if (string.Equals(dialogName, "SIAUL_WEST_NAGLIS2", StringComparison.OrdinalIgnoreCase))
				return 20016;

			if (string.Equals(dialogName, "SIAUL_WEST_SOL3", StringComparison.OrdinalIgnoreCase))
				return 20016;

			if (string.Equals(dialogName, "SIAUL_ST1_ST2", StringComparison.OrdinalIgnoreCase))
				return 20016;

			if (dialogName.Contains("BOX", StringComparison.OrdinalIgnoreCase) ||
				dialogName.Contains("CHEST", StringComparison.OrdinalIgnoreCase))
				return 147392;

			if (dialogName.Contains("BOOK", StringComparison.OrdinalIgnoreCase) ||
				dialogName.Contains("MAIL", StringComparison.OrdinalIgnoreCase))
				return 155005;

			if (string.Equals(dialogName, "CHAPLE575_MQ_04", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "CHAPLE575_MQ_09", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "CHAPLE576_MQ_04", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "CHAPLE576_MQ_09", StringComparison.OrdinalIgnoreCase))
				return 147353;

			if (dialogName.Contains("STONE", StringComparison.OrdinalIgnoreCase) ||
				dialogName.Contains("ROCK", StringComparison.OrdinalIgnoreCase) ||
				dialogName.Contains("CRYSTAL", StringComparison.OrdinalIgnoreCase) ||
				dialogName.Contains("PURIFY", StringComparison.OrdinalIgnoreCase))
				return 12080;

			if (dialogName.Contains("TRIGGER", StringComparison.OrdinalIgnoreCase) ||
				dialogName.Contains("HIDDEN", StringComparison.OrdinalIgnoreCase))
				return 20041;

			return 20117;
		}

		private string ResolveStaticQuestNpcName(string dialogName)
		{
			if (string.Equals(dialogName, "KLAPEDA_USKA", StringComparison.OrdinalIgnoreCase))
				return "[Templar Master]\nKnight Commander Uska";

			if (string.Equals(dialogName, "EMILIA", StringComparison.OrdinalIgnoreCase))
				return "[Item Merchant]\nMirina";

			if (string.Equals(dialogName, "ALFONSO", StringComparison.OrdinalIgnoreCase))
				return "[Accessory Merchant]\nRonesa";

			if (string.Equals(dialogName, "AKALABETH", StringComparison.OrdinalIgnoreCase))
				return "[Equipment Merchant]\nDunkel";

			if (string.Equals(dialogName, "SIAUL_EAST_MANAGER", StringComparison.OrdinalIgnoreCase))
				return "Knight Aras";

			if (string.Equals(dialogName, "SIAUL_EAST_SUPPLY_MANAGER2", StringComparison.OrdinalIgnoreCase))
				return "Operations Officer";

			if (string.Equals(dialogName, "SIAUL_EAST_SOLDIER8", StringComparison.OrdinalIgnoreCase))
				return "Search Scout";

			if (string.Equals(dialogName, "SIAULIAIOUT_CHIEF_A", StringComparison.OrdinalIgnoreCase))
				return "Village Chief";

			if (string.Equals(dialogName, "SIAULIAIOUT_ALCHE", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "SIAULIAIOUT_ALCHE_A", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "MINE_1_ALCHEMIST", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "MINE_3_ALCHEMIST", StringComparison.OrdinalIgnoreCase))
				return "[Alchemist Master]\nVaidotas";

			if (string.Equals(dialogName, "MINE_3_RESIENT1", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "MINE_3_RESIENT1_BIND", StringComparison.OrdinalIgnoreCase))
				return "Miner";

			if (string.Equals(dialogName, "MASTER_BOCORS", StringComparison.OrdinalIgnoreCase))
				return "[Bokor Master]\nMama Marilabo";

			if (string.Equals(dialogName, "GELE572_MQ_01", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "GELE573_MASTER", StringComparison.OrdinalIgnoreCase))
				return "Paladin Master";

			if (string.Equals(dialogName, "GELE573_MQ_07_F", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "GELE574_ALLGES", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "GELE574_ARUNE_1", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "CHAPEL_TOMAS", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "CHAPEL_VIDAS", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "CHAPEL_VIRGINIJA", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "CHAPEL576_DONATAS", StringComparison.OrdinalIgnoreCase))
				return "Paladin Follower";

			if (string.Equals(dialogName, "CHAPLE575_MQ_04", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "CHAPLE575_MQ_09", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "CHAPLE576_MQ_04", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(dialogName, "CHAPLE576_MQ_09", StringComparison.OrdinalIgnoreCase))
				return string.Empty;

			if (string.Equals(dialogName, "SIAUL_WEST_SOL3", StringComparison.OrdinalIgnoreCase))
				return "Battle Commander";

			if (string.Equals(dialogName, "SIAUL_WEST_CAMP_MANAGER", StringComparison.OrdinalIgnoreCase))
				return "Knight Titas";

			if (string.Equals(dialogName, "SIALUL_WEST_DRASIUS", StringComparison.OrdinalIgnoreCase))
				return "Scout";

			if (string.Equals(dialogName, "SIAUL_WEST_NAGLIS2", StringComparison.OrdinalIgnoreCase))
				return "Search Scout";

			if (string.Equals(dialogName, "SIAUL_WEST_LAIMONAS", StringComparison.OrdinalIgnoreCase))
				return "Laimonas";

			if (string.Equals(dialogName, "SIAUL_ST1_ST2", StringComparison.OrdinalIgnoreCase))
				return "Klaipeda Guard Captain";

			return dialogName;
		}

		private bool IsTechnicalStaticQuestNpc(string dialogName)
		{
			if (string.IsNullOrWhiteSpace(dialogName))
				return true;

			return dialogName.Contains("_AUTO", StringComparison.OrdinalIgnoreCase) ||
				dialogName.Contains("TRIGGER", StringComparison.OrdinalIgnoreCase) ||
				dialogName.Contains("HIDDEN", StringComparison.OrdinalIgnoreCase);
		}

		private readonly record struct StaticQuestNpcSpawnRequest(string DialogName, string QuestClassName, string Name, double X, double Y, double Z, double Range);
		private readonly record struct StaticQuestMonsterSpawnRequest(int MonsterId, string ClassName, string Name, string QuestClassName, double X, double Y, double Z, double Range, int Count, bool IsPrivateEncounter);
		private readonly record struct StaticQuestAutoTrackRequest(string StartStatus, string EndStatus, string TrackName, string PropertyName, int DelayMilliseconds);
		private readonly record struct StaticQuestLocationPoint(double X, double Y, double Z, double Range);

		private bool StaticQuestReferencesMap(QuestStaticData questData, string mapClassName)
		{
			return this.StaticQuestMapMatches(questData.StartMap, mapClassName) ||
				this.StaticQuestMapMatches(questData.ProgMap, mapClassName) ||
				this.StaticQuestMapMatches(questData.EndMap, mapClassName) ||
				this.StaticQuestLocationReferencesMap(questData.StartLocation, mapClassName) ||
				this.StaticQuestLocationReferencesMap(questData.ProgLocation, mapClassName) ||
				this.StaticQuestLocationReferencesMap(questData.EndLocation, mapClassName);
		}

		private bool StaticQuestObjectiveReferencesMap(QuestStaticData questData, string mapClassName)
		{
			if (questData == null || string.IsNullOrWhiteSpace(mapClassName))
				return false;

			if (this.StaticQuestMapMatches(questData.ProgMap, mapClassName) ||
				this.StaticQuestLocationReferencesMap(questData.ProgLocation, mapClassName))
				return true;

			if (!string.IsNullOrWhiteSpace(questData.ProgMap) ||
				!string.IsNullOrWhiteSpace(questData.ProgLocation))
				return false;

			return this.StaticQuestMapMatches(questData.StartMap, mapClassName) ||
				this.StaticQuestLocationReferencesMap(questData.StartLocation, mapClassName) ||
				this.StaticQuestMapMatches(questData.EndMap, mapClassName) ||
				this.StaticQuestLocationReferencesMap(questData.EndLocation, mapClassName);
		}

		private bool StaticQuestMapMatches(string questMap, string mapClassName)
		{
			return !string.IsNullOrWhiteSpace(questMap) &&
				string.Equals(questMap, mapClassName, StringComparison.OrdinalIgnoreCase);
		}

		private bool StaticQuestLocationReferencesMap(string location, string mapClassName)
		{
			if (string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(mapClassName))
				return false;

			return location.Split(' ', StringSplitOptions.RemoveEmptyEntries)
				.Any(part => string.Equals(part, mapClassName, StringComparison.OrdinalIgnoreCase));
		}

		public void RepairWestSiauliaiMainQuestState()
		{
			if (!string.Equals(this.Character?.Map?.ClassName, "f_siauliai_west", StringComparison.OrdinalIgnoreCase))
				return;

			this.CompleteSuccessfulWestSiauliaiSystemQuests();
			this.EnsureWestSiauliaiMainQuestSeeded();
			this.EnsureWestSiauliaiMainSystemFollowUps();
			this.RepairPrematureBattleCommanderQuestStart();
			this.ParkStaticQuestFromWestSiauliaiMainFlow(1022, "SIAUL_WEST_BOSS_GOLEM");
			this.ParkStaticQuestFromWestSiauliaiMainFlow(1023, "SIAUL_WEST_ONION_BIG");
			this.ParkStaticQuestFromWestSiauliaiMainFlow(20128, "SIAUL_WEST_LAIMONAS3_2");
			this.ParkOutOfSequenceWestSiauliaiMainQuests();
			this.SuppressOutOfSequenceWestSiauliaiNativeQuestState();

			this.SyncStaticQuestSessionObjects();
			this.SyncTrackedQuestSlots();
		}

		private bool CompleteSuccessfulWestSiauliaiSystemQuests()
		{
			var completedAny = false;
			var quests = this.GetList()
				.Where(quest =>
					quest.Status == QuestStatus.Success &&
					quest.QuestStaticData != null &&
					string.Equals(quest.QuestStaticData.QuestEndMode, "SYSTEM", StringComparison.OrdinalIgnoreCase) &&
					this.IsWestSiauliaiOrderedMainQuest(quest.QuestStaticData, out _))
				.ToList();

			foreach (var quest in quests)
			{
				Log.Info("West Siauliai main flow: auto-completing SYSTEM quest {0} for '{1}' after objective success.", quest.QuestStaticData.ClassName, this.Character.Name);
				this.Complete(quest);
				completedAny = true;
			}

			return completedAny;
		}

		private bool EnsureWestSiauliaiMainQuestSeeded()
		{
			if (WestSiauliaiMainQuestOrder.Any(questId => this.TryGetById(questId, out _)))
				return false;

			if (!ZoneServer.Instance.Data.QuestDb.TryFind("SIAUL_WEST_MEET_TITAS", out var firstQuest))
				return false;

			Log.Info("West Siauliai main flow: seeding first Papaya main quest SIAUL_WEST_MEET_TITAS for '{0}'.", this.Character.Name);
			this.StartStaticQuest(firstQuest, TimeSpan.Zero);
			return true;
		}

		private bool EnsureWestSiauliaiMainSystemFollowUps()
		{
			var changed = false;

			if (this.HasCompleted(1020) && !this.IsActive(1021) && !this.HasCompleted(1021))
				changed |= this.TryStartWestSiauliaiMainQuest("SIAUL_WEST_HAMING_LEAF", "after SIAUL_WEST_SOLDIER3");

			return changed;
		}

		private bool RepairPrematureBattleCommanderQuestStart()
		{
			if (!this.HasCompleted(1014) || this.HasCompleted(1020))
				return false;

			if (!this.TryGetById(1020, out var quest) || !quest.InProgress)
				return false;

			if (this.Character.Variables.Perm.GetBool("Clover.WestSiauliai.Soldier3Accepted", false))
				return false;

			if (quest.Progresses.Any(progress => progress.Count > 0 || progress.Done))
			{
				this.Character.Variables.Perm.SetBool("Clover.WestSiauliai.Soldier3Accepted", true);
				return false;
			}

			Log.Info("West Siauliai main flow: rolling back prematurely started SIAUL_WEST_SOLDIER3 for '{0}'. It must be accepted at Battle Commander.", this.Character.Name);
			this.Cancel(quest);
			return true;
		}

		private bool TryStartWestSiauliaiMainQuest(string questClassName, string reason)
		{
			if (!ZoneServer.Instance.Data.QuestDb.TryFind(questClassName, out var questData))
				return false;

			if (this.Has(new QuestId(questData.Id)) || this.StaticQuestIsBlockedByEarlierWestSiauliaiMain(questData))
				return false;

			Log.Info("West Siauliai main flow: starting Papaya follow-up quest {0} for '{1}' {2}.", questClassName, this.Character.Name, reason);
			this.StartStaticQuest(questData, TimeSpan.Zero);
			return true;
		}

		private void SuppressWestSiauliaiSideQuestNoise(string mapClassName)
		{
			if (!string.Equals(mapClassName, "f_siauliai_west", StringComparison.OrdinalIgnoreCase))
				return;

			this.ParkStaticQuestFromWestSiauliaiMainFlow(1022, "SIAUL_WEST_BOSS_GOLEM");
			this.ParkStaticQuestFromWestSiauliaiMainFlow(1023, "SIAUL_WEST_ONION_BIG");
			this.ParkStaticQuestFromWestSiauliaiMainFlow(20128, "SIAUL_WEST_LAIMONAS3_2");
			this.SuppressOutOfSequenceWestSiauliaiNativeQuestState();
		}

		private bool IsParkedFromWestSiauliaiMainFlow(Quest quest)
			=> quest?.QuestStaticData != null && this.IsWestSiauliaiOptionalQuestName(quest.QuestStaticData.ClassName);

		private bool IsWestSiauliaiOptionalQuestName(string questClassName)
			=> string.Equals(questClassName, "SIAUL_WEST_ONION_BIG", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(questClassName, "SIAUL_WEST_BOSS_GOLEM", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(questClassName, "SIAUL_WEST_LAIMONAS3_2", StringComparison.OrdinalIgnoreCase);

		private bool ParkStaticQuestFromWestSiauliaiMainFlow(long questId, string questClassName)
		{
			var changed = false;

			if (this.TryGetById(questId, out var quest))
			{
				if (quest.Tracked)
				{
					quest.Tracked = false;
					changed = true;
				}

				this.UpdateClient_RemoveQuest(quest);
				lock (_syncLock)
					_quests.Remove(quest);
				changed = true;
			}

			if (ZoneServer.Instance.Data.QuestDb.TryFind(questClassName, out var questData))
			{
				changed |= this.RemoveStaticQuestSessionObject(questData);
				changed |= this.SetStaticQuestProperty(questData, QuestStatus.Completed);
				this.NotifyNativeStaticQuestStateChanged(questData, false);
				this.RemoveStaticQuestFromClientList(questData.Id, questData.ClassName);
			}

			if (changed)
				Log.Info("West Siauliai main flow: parked non-main quest {0} for '{1}'.", questClassName, this.Character.Name);

			return changed;
		}

		private static readonly int[] WestSiauliaiMainQuestOrder =
		{
			1001, 1002, 1003, 20127, 1004, 1014, 8350, 1020, 1021, 1013, 1015, 1018, 1019,
		};

		private static readonly string[] PapayaCapturedMainQuestOrder =
		{
			"KLAPEDA_GO_TO_EAST",
			"EAST_PREPARE",
			"EAST_PREPARE_1",
			"SIAUL_EAST_RECLAIM1",
			"SIAUL_EAST_REQUEST1",
			"SIAUL_EAST_REQUEST2",
			"SIAUL_EAST_REQUEST3",
			"SIAUL_EAST_REQUEST6",
			"SIAUL_EAST_REQUEST7",
			"SOUT_Q_01",
			"SOUT_Q_14",
			"SOUT_Q_16",
			"MINE_1_ALCHEMIST",
			"MINE_2_ALCHEMIST",
			"MINE_3_RESQUE1",
			"CMINE6_TO_KATYN7_1",
			"CMINE6_TO_KATYN7_2",
			"CMINE6_TO_KATYN7_3",
			"SOUT_Q_41",
			"GELE572_MQ_01",
			"GELE573_MQ_07",
			"GELE573_MQ_09",
			"GELE573_MQ_08",
			"GELE574_MQ_09",
			"CHAPLE575_MQ_04",
			"CHAPLE575_MQ_09",
		};

			private static readonly string[] PapayaCrystalMineSkipQuestNames =
			{
				"SOUT_Q_16",
				"MINE_1_ALCHEMIST",
				"MINE_1_CRYSTAL_2",
				"MINE_1_CRYSTAL_8",
				"MINE_1_CRYSTAL_9",
				"MINE_1_CRYSTAL_10",
				"MINE_1_CRYSTAL_13",
				"MINE_1_CRYSTAL_18",
				"MINE_1_CRYSTAL_19",
				"MINE_2_ALCHEMIST",
				"MINE_2_CRYSTAL_2",
				"MINE_2_CRYSTAL_3",
				"MINE_2_CRYSTAL_4",
				"MINE_2_CRYSTAL_5",
				"MINE_2_CRYSTAL_7",
				"MINE_2_CRYSTAL_10",
				"MINE_2_CRYSTAL_11",
				"MINE_2_CRYSTAL_14",
				"MINE_2_CRYSTAL_20",
				"MINE_2_CRYSTAL_21",
				"MINE_3_RESQUE1",
				"MINE_3_RESQUE3",
				"ACT4_MINE3_ENTER",
				"MINE_3_BOSS",
				"CMINE6_TO_KATYN7_1",
			"CMINE6_TO_KATYN7_2",
			"CMINE6_TO_KATYN7_3",
			"SOUT_Q_41",
		};

			private static readonly string[] PapayaMinersVillageOptionalCleanupQuestNames =
			{
				"SOUT_Q_05",
				"SOUT_Q_07",
				"SOUT_Q_08",
				"SOUT_Q_09",
				"SOUT_Q_10",
				"SOUT_SUDD_PREBOSS",
				"SOUT_Q_13",
				"SOUT_Q_15",
				"SOUT_Q_20",
				"SOUT_Q_21",
				"SOUT_Q_22",
				"SOUT_Q_23",
				"SOUT_Q_24",
				"SOUT_Q_31",
				"SOUT_Q_32",
			};

		private bool StaticQuestIsBlockedByEarlierPapayaCapturedMain(QuestStaticData questData)
		{
			if (!this.IsPapayaCapturedMainQuest(questData, out var questIndex))
				return false;

			for (var i = 0; i < questIndex; i++)
			{
				if (!ZoneServer.Instance.Data.QuestDb.TryFind(PapayaCapturedMainQuestOrder[i], out var earlierQuestData))
					return true;

				if (!this.HasCompleted(earlierQuestData.Id))
					return true;
			}

			return false;
		}

		private bool StaticQuestIsBlockedByPapayaMainProgression(QuestStaticData questData)
		{
			return this.StaticQuestIsBlockedByEarlierPapayaCapturedMain(questData) ||
				this.StaticQuestIsBlockedByPapayaAutoMainChain(questData);
		}

		private bool StaticQuestIsBlockedByPapayaAutoMainChain(QuestStaticData questData)
		{
			if (questData == null ||
				!string.Equals(questData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase) ||
				string.IsNullOrWhiteSpace(questData.ClassName))
				return false;

			var predecessorCount = 0;
			foreach (var predecessorQuestData in this.GetPapayaAutoMainPredecessors(questData.ClassName))
			{
				predecessorCount++;
				if (this.HasCompleted(predecessorQuestData.Id))
					return false;
			}

			return predecessorCount > 0;
		}

		private IEnumerable<QuestStaticData> GetPapayaAutoMainPredecessors(string questClassName)
		{
			if (string.IsNullOrWhiteSpace(questClassName))
				yield break;

			foreach (var questAutoData in ZoneServer.Instance.Data.QuestAutoDb.Entries.Values)
			{
				if (questAutoData?.SuccessNextQuestNames == null ||
					!questAutoData.SuccessNextQuestNames.Any(nextQuestName => string.Equals(nextQuestName, questClassName, StringComparison.OrdinalIgnoreCase)))
					continue;

				if (!ZoneServer.Instance.Data.QuestDb.TryFind(questAutoData.QuestName, out var predecessorQuestData))
					continue;

				if (!string.Equals(predecessorQuestData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase))
					continue;

				if (this.StaticQuestDisabledForCloverFlow(predecessorQuestData))
					continue;

				yield return predecessorQuestData;
			}
		}

		private bool SuppressOutOfSequencePapayaCapturedMainQuestState()
		{
			var changed = false;

			foreach (var questName in PapayaCapturedMainQuestOrder)
			{
				if (!ZoneServer.Instance.Data.QuestDb.TryFind(questName, out var questData))
					continue;

				if (!this.StaticQuestIsBlockedByEarlierPapayaCapturedMain(questData))
					continue;

				if (this.TryGetById(new QuestId(questData.Id), out var quest) &&
					quest.Status != QuestStatus.Completed &&
					quest.Status != QuestStatus.Abandoned)
				{
					Log.Info("Papaya main quest flow: removing out-of-sequence quest '{0}' for '{1}' until the captured chain reaches it.", questData.ClassName, this.Character.Name);
					this.RemoveOutOfSequencePapayaCapturedMainQuest(quest);
					changed = true;
				}
				else
				{
					changed |= this.RemoveStaticQuestSessionObject(questData);
					changed |= this.SetStaticQuestProperty(questData, QuestStatus.Impossible);
					this.RemoveStaticQuestFromClientList(questData.Id, questData.ClassName);
				}
			}

			return changed;
		}

		private bool SuppressOutOfSequencePapayaAutoMainQuestState()
		{
			var changed = false;

			foreach (var quest in this.GetList().ToList())
			{
				if (quest.QuestStaticData == null ||
					quest.Status == QuestStatus.Completed ||
					quest.Status == QuestStatus.Abandoned ||
					!this.StaticQuestIsBlockedByPapayaAutoMainChain(quest.QuestStaticData))
					continue;

				Log.Info("Papaya main quest graph: removing out-of-sequence quest '{0}' for '{1}' until its quest_auto predecessor is complete.", quest.QuestStaticData.ClassName, this.Character.Name);
				this.RemoveOutOfSequencePapayaCapturedMainQuest(quest);
				changed = true;
			}

			return changed;
		}

		private void RemoveOutOfSequencePapayaCapturedMainQuest(Quest quest)
		{
			if (quest == null)
				return;

			quest.Tracked = false;
			this.UpdateClient_RemoveQuest(quest);

			if (quest.QuestStaticData != null)
			{
				this.RemoveStaticQuestFromClientList((int)quest.Data.Id.Value, quest.QuestStaticData.ClassName);
				this.RemoveStaticQuestSessionObject(quest.QuestStaticData);
				this.SetStaticQuestProperty(quest.QuestStaticData, QuestStatus.Impossible);
			}

			lock (_syncLock)
				_quests.Remove(quest);
		}

		private bool IsPapayaCapturedMainQuest(QuestStaticData questData, out int questIndex)
		{
			questIndex = -1;
			if (questData == null || string.IsNullOrWhiteSpace(questData.ClassName))
				return false;

			for (var i = 0; i < PapayaCapturedMainQuestOrder.Length; i++)
			{
				if (!string.Equals(PapayaCapturedMainQuestOrder[i], questData.ClassName, StringComparison.OrdinalIgnoreCase))
					continue;

				questIndex = i;
				return true;
			}

			return false;
		}

		private bool StaticQuestIsBlockedByEarlierWestSiauliaiMain(QuestStaticData questData)
		{
			if (!this.IsWestSiauliaiOrderedMainQuest(questData, out var questIndex))
				return false;

			foreach (var earlierQuestId in WestSiauliaiMainQuestOrder.Take(questIndex))
			{
				if (!this.TryGetById(earlierQuestId, out var earlierQuest))
					return true;

				if (earlierQuest.Status != QuestStatus.Completed)
					return true;
			}

			return false;
		}

		private bool ParkOutOfSequenceWestSiauliaiMainQuests()
		{
			var changed = false;
			var activeEarlierIndex = -1;

			for (var i = 0; i < WestSiauliaiMainQuestOrder.Length; i++)
			{
				if (!this.TryGetById(WestSiauliaiMainQuestOrder[i], out var quest))
					continue;

				if (quest.InProgress || quest.Status == QuestStatus.Success)
				{
					activeEarlierIndex = i;
					break;
				}
			}

			if (activeEarlierIndex < 0)
				return false;

			for (var i = activeEarlierIndex + 1; i < WestSiauliaiMainQuestOrder.Length; i++)
			{
				if (!this.TryGetById(WestSiauliaiMainQuestOrder[i], out var quest))
					continue;

				if (quest.Status == QuestStatus.Completed || quest.Status == QuestStatus.Abandoned)
					continue;

				Log.Info("West Siauliai main flow: removing out-of-sequence quest {0} for '{1}' while quest {2} is active.", quest.QuestStaticData?.ClassName ?? quest.Data.Id.ToString(), this.Character.Name, WestSiauliaiMainQuestOrder[activeEarlierIndex]);
				this.RemoveOutOfSequenceWestSiauliaiMainQuest(quest);
				changed = true;
			}

			return changed;
		}

		private bool SuppressOutOfSequenceWestSiauliaiNativeQuestState()
		{
			var activeEarlierIndex = this.GetActiveWestSiauliaiMainQuestIndex();
			if (activeEarlierIndex < 0)
				return false;

			var changed = false;

			for (var i = activeEarlierIndex + 1; i < WestSiauliaiMainQuestOrder.Length; i++)
			{
				if (!ZoneServer.Instance.Data.QuestDb.TryFind(WestSiauliaiMainQuestOrder[i], out var questData))
					continue;

				if (this.TryGetById(WestSiauliaiMainQuestOrder[i], out var quest) &&
					quest.Status != QuestStatus.Completed &&
					quest.Status != QuestStatus.Abandoned)
				{
					this.RemoveOutOfSequenceWestSiauliaiMainQuest(quest);
					changed = true;
				}
				else
				{
					this.RemoveStaticQuestSessionObject(questData);
					this.SetStaticQuestProperty(questData, QuestStatus.Impossible);
					this.RemoveStaticQuestFromClientList(WestSiauliaiMainQuestOrder[i], questData.ClassName);
				}
			}

			return changed;
		}

		private int GetActiveWestSiauliaiMainQuestIndex()
		{
			for (var i = 0; i < WestSiauliaiMainQuestOrder.Length; i++)
			{
				if (!this.TryGetById(WestSiauliaiMainQuestOrder[i], out var quest))
					continue;

				if (quest.InProgress || quest.Status == QuestStatus.Success)
					return i;
			}

			return -1;
		}

		private void RemoveOutOfSequenceWestSiauliaiMainQuest(Quest quest)
		{
			if (quest == null)
				return;

			quest.Tracked = false;
			this.UpdateClient_RemoveQuest(quest);
			this.RemoveStaticQuestFromClientList((int)quest.Data.Id.Value, quest.QuestStaticData?.ClassName);

			if (quest.QuestStaticData != null)
			{
				this.RemoveStaticQuestSessionObject(quest.QuestStaticData);
				this.SetStaticQuestProperty(quest.QuestStaticData, QuestStatus.Impossible);
			}

			lock (_syncLock)
				_quests.Remove(quest);
		}

		private bool IsWestSiauliaiOrderedMainQuest(QuestStaticData questData, out int index)
		{
			index = -1;
			if (questData == null ||
				!string.Equals(questData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase) ||
				!this.StaticQuestReferencesMap(questData, "f_siauliai_west"))
				return false;

			index = Array.IndexOf(WestSiauliaiMainQuestOrder, questData.Id);
			return index >= 0;
		}

		private bool StaticQuestCanStartFromNpcDialog(QuestStaticData questData, string npcDialogName)
		{
			if (this.StaticNpcDialogMatches(questData.StartNPC, npcDialogName))
				return true;

			if (this.StaticQuestNpcDialogMatchesLocation(questData, npcDialogName, questData.StartLocation))
				return true;

			if (string.Equals(questData.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase) &&
				this.StaticQuestNpcDialogMatchesLocation(questData, npcDialogName, questData.ProgLocation))
				return true;

			return false;
		}

		private bool StaticQuestIsBlockedByPriorityQuest(QuestStaticData questData, string npcDialogName)
		{
			if (questData == null)
				return false;

			if (!string.Equals(questData.QuestMode, "SUB", StringComparison.OrdinalIgnoreCase) &&
				!string.Equals(questData.QuestMode, "REPEAT", StringComparison.OrdinalIgnoreCase))
				return false;

			if (questData.Id == 1023 && !this.HasCompleted(1013))
				return true;

			return ZoneServer.Instance.Data.QuestDb.GetList().Any(candidate =>
				candidate.Id != questData.Id &&
				string.Equals(candidate.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase) &&
				string.Equals(candidate.QuestStartMode, "NPCDIALOG", StringComparison.OrdinalIgnoreCase) &&
				this.StaticQuestCanStartFromNpcDialog(candidate, npcDialogName) &&
				!this.Has(new QuestId(candidate.Id)) &&
				!this.StaticQuestIsBlockedByPapayaMainProgression(candidate) &&
				this.MeetsStaticPrerequisites(candidate));
		}

		private bool TryAdvanceStaticQuestFromNpcDialog(Quest quest, string npcDialogName)
		{
			if (!quest.TryGetProgress("manual", out var progress))
				return false;

			if (progress.Done)
				return false;

			var questData = quest.QuestStaticData;
			if (!this.StaticQuestCanAdvanceFromNpcDialog(questData, npcDialogName))
				return false;

			this.CompleteObjective(quest.Data.Id, progress.Objective.Ident);

			if (quest.Status == QuestStatus.Success && this.StaticQuestShouldCompleteFromNpcDialog(questData, npcDialogName))
				this.Complete(quest);

			return true;
		}

		private bool TryTriggerStaticQuestFromNpcDialog(Quest quest, string npcDialogName)
		{
			var questData = quest.QuestStaticData;
			if (!this.StaticQuestCanAdvanceFromNpcDialog(questData, npcDialogName))
				return false;

			var mapClassName = this.Character?.Map?.ClassName;
			if (string.IsNullOrWhiteSpace(mapClassName))
				return false;

			if (this.TryStartStaticQuestAutoTrack(quest, mapClassName))
				return true;

			if (!this.StaticQuestNpcDialogMatchesLocation(questData, npcDialogName, questData.ProgLocation))
				return false;

			this.EnsureStaticQuestObjectiveMonsters(mapClassName);
			this.SyncStaticQuestSessionObject(quest);
			this.SyncTrackedQuestSlots();
			return true;
		}

		private bool TryTurnInStaticQuestFromNpcDialog(Quest quest, string npcDialogName)
		{
			if (!this.StaticQuestCanTurnInFromNpcDialog(quest, npcDialogName))
				return false;

			this.Complete(quest);
			return true;
		}

		private bool StaticQuestCanTurnInFromNpcDialog(Quest quest, string npcDialogName)
		{
			if (quest?.QuestStaticData == null ||
				!this.StaticQuestShouldCompleteFromNpcDialog(quest.QuestStaticData, npcDialogName))
				return false;

			return quest.Status == QuestStatus.Success ||
				quest.ObjectivesCompleted ||
				this.StaticQuestNativePropertyAtLeast(quest.QuestStaticData, QuestStatus.Success);
		}

		private bool StaticQuestNativePropertyAtLeast(QuestStaticData questData, QuestStatus status)
		{
			if (questData == null || string.IsNullOrWhiteSpace(questData.QuestProperty))
				return false;

			var main = this.Character?.SessionObjects?.Main;
			if (main == null || !main.Properties.Has(questData.QuestProperty))
				return false;

			return main.Properties.GetFloat(questData.QuestProperty) >= (int)status;
		}

		private bool StaticQuestCanAdvanceFromNpcDialog(QuestStaticData questData, string npcDialogName)
		{
			if (this.StaticNpcDialogMatches(questData.ProgNPC, npcDialogName))
				return true;

			if (this.StaticQuestNpcDialogMatchesLocation(questData, npcDialogName, questData.ProgLocation))
				return true;

			if (this.StaticNpcDialogMatches(questData.EndNPC, npcDialogName))
				return true;

			if (this.StaticQuestNpcDialogMatchesLocation(questData, npcDialogName, questData.EndLocation))
				return true;

			if (this.StaticNpcDialogMatches(questData.StartNPC, npcDialogName))
				return string.IsNullOrWhiteSpace(questData.ProgNPC) &&
					string.IsNullOrWhiteSpace(questData.ProgLocation) &&
					string.IsNullOrWhiteSpace(questData.EndNPC);

			if (string.Equals(questData.QuestEndMode, "SYSTEM", StringComparison.OrdinalIgnoreCase) &&
				string.IsNullOrWhiteSpace(questData.ProgNPC) &&
				string.IsNullOrWhiteSpace(questData.EndNPC) &&
				this.StaticQuestHasNextNpcDialogStarter(questData, npcDialogName))
				return true;

			return false;
		}

		private bool StaticQuestShouldCompleteFromNpcDialog(QuestStaticData questData, string npcDialogName)
		{
			if (string.Equals(questData.QuestEndMode, "SYSTEM", StringComparison.OrdinalIgnoreCase))
				return true;

			if (string.Equals(questData.QuestEndMode, "NPCDIALOG", StringComparison.OrdinalIgnoreCase) &&
				this.StaticNpcDialogMatches(questData.EndNPC, npcDialogName))
				return true;

			if (string.Equals(questData.QuestEndMode, "NPCDIALOG", StringComparison.OrdinalIgnoreCase) &&
				this.StaticQuestNpcDialogMatchesLocation(questData, npcDialogName, questData.EndLocation))
				return true;

			return false;
		}

		private bool StaticQuestHasNextNpcDialogStarter(QuestStaticData questData, string npcDialogName)
		{
			return ZoneServer.Instance.Data.QuestDb.GetList().Any(a =>
				a.RequiredQuests != null &&
				a.RequiredQuests.Any(requiredQuest => string.Equals(requiredQuest, questData.ClassName, StringComparison.OrdinalIgnoreCase)) &&
				string.Equals(a.QuestStartMode, "NPCDIALOG", StringComparison.OrdinalIgnoreCase) &&
				this.StaticNpcDialogMatches(a.StartNPC, npcDialogName));
		}

		private bool StaticNpcDialogMatches(string expectedDialogName, string npcDialogName)
		{
			return !string.IsNullOrWhiteSpace(expectedDialogName) &&
				string.Equals(expectedDialogName, npcDialogName, StringComparison.OrdinalIgnoreCase);
		}

		private bool StaticQuestNpcDialogMatchesLocation(QuestStaticData questData, string npcDialogName, string location)
		{
			if (questData == null || string.IsNullOrWhiteSpace(npcDialogName) || string.IsNullOrWhiteSpace(location))
				return false;

			var mapClassName = this.Character?.Map?.ClassName;
			if (string.IsNullOrWhiteSpace(mapClassName) || !this.StaticQuestLocationReferencesMap(location, mapClassName))
				return false;

			var points = this.GetStaticQuestLocationPoints(location, mapClassName).ToList();
			if (points.Count == 0)
				return false;

			if (!this.TryResolveCurrentNpcDialogPosition(npcDialogName, mapClassName, out var npcX, out _, out var npcZ, out var npcRange))
				return false;

			foreach (var point in points)
			{
				var radius = Math.Max(125, Math.Max(point.Range, npcRange));
				var npcDx = npcX - point.X;
				var npcDz = npcZ - point.Z;
				if ((npcDx * npcDx) + (npcDz * npcDz) > radius * radius)
					continue;

				var playerDx = this.Character.Position.X - point.X;
				var playerDz = this.Character.Position.Z - point.Z;
				return (playerDx * playerDx) + (playerDz * playerDz) <= radius * radius;
			}

			return false;
		}

		private bool TryResolveCurrentNpcDialogPosition(string npcDialogName, string mapClassName, out double x, out double y, out double z, out double range)
		{
			x = 0;
			y = 0;
			z = 0;
			range = 100;

			if (this.Character?.Map != null)
			{
				var npc = this.Character.Map
					.GetNpcs(actor => actor is Npc candidate && this.StaticNpcDialogMatches(candidate.DialogName, npcDialogName))
					.OfType<Npc>()
					.OrderBy(candidate =>
					{
						var dx = candidate.Position.X - this.Character.Position.X;
						var dz = candidate.Position.Z - this.Character.Position.Z;
						return (dx * dx) + (dz * dz);
					})
					.FirstOrDefault();

				if (npc != null)
				{
					x = npc.Position.X;
					y = npc.Position.Y;
					z = npc.Position.Z;
					range = 100;
					return true;
				}
			}

			return this.TryResolvePapayaCapturedStaticNpcPosition(mapClassName, npcDialogName, out x, out y, out z, out range);
		}

		private QuestType MapQuestType(string questMode)
		{
			if (string.Equals(questMode, "MAIN", StringComparison.OrdinalIgnoreCase))
				return QuestType.Main;
			if (string.Equals(questMode, "PARTY", StringComparison.OrdinalIgnoreCase))
				return QuestType.Party;
			if (string.Equals(questMode, "REPEAT", StringComparison.OrdinalIgnoreCase))
				return QuestType.Repeat;

			return QuestType.Sub;
		}

		/// <summary>
		/// Starts the given quest, adding it to the character's quest log.
		/// </summary>
		/// <param name="quest"></param>
		/// <returns></returns>
		private void Start(Quest quest)
		{
			lock (_syncLock)
			{
				if (!_quests.Contains(quest))
				{
					var oldQuest = _quests.FirstOrDefault(q => q.Data.Id == quest.Data.Id);
					if (oldQuest != null)
						_quests.Remove(oldQuest);
					_quests.Add(quest);
				}
			}

			this.InitialChecks(quest);

			quest.Status = QuestStatus.InProgress;
			quest.UpdateUnlock();

			if (quest.StartTime == DateTime.MinValue)
				quest.StartTime = DateTime.Now;

			var questScript = quest.AssociatedGenerator;
			if (questScript == null && !QuestScript.TryGet(quest.Data.Id, out questScript))
			{
				Log.Debug($"No static QuestScript found for QuestId {quest.Data.Id} during Start.");
			}
			questScript?.OnStart(this.Character, quest);

			this.EnsureStaticQuestLayerState(quest);

			this.UpdateClient_AddQuest(quest);
		}

		/// <summary>
		/// Returns true if a quest with the given id is currently in
		/// progress and the objective with the given identifier is
		/// unlocked, but hasn't been completed yet.
		/// </summary>
		/// <param name="questId"></param>
		/// <param name="objectiveIdent"></param>
		/// <returns></returns>
		public bool IsActive(QuestId questId, string objectiveIdent)
		{
			lock (_syncLock)
			{
				foreach (var quest in _quests)
				{
					if (!quest.InProgress || quest.Data.Id != questId)
						continue;

					if (!quest.TryGetProgress(objectiveIdent, out var progress))
						continue;

					if (progress.Unlocked && !progress.Done)
						return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Returns true if a quest with the given id is currently active,
		/// meaning that it was started, but not completed yet, even if
		/// all objectives were completed already.
		/// </summary>
		/// <param name="questId"></param>
		/// <returns></returns>
		[Obsolete("Use IsActive(QuestId questId)")]
		public bool IsActive(long questId)
		{
			lock (_syncLock)
			{
				foreach (var quest in _quests)
				{
					if (quest.InProgress && quest.Data.Id.Value == questId)
						return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Returns true if a quest with the given id is currently active,
		/// meaning that it was started, but not completed yet, even if
		/// all objectives were completed already.
		/// </summary>
		/// <param name="questId"></param>
		/// <returns></returns>
		public bool IsActive(QuestId questId)
		{
			lock (_syncLock)
			{
				foreach (var quest in _quests)
				{
					if (quest.InProgress && quest.Data.Id == questId)
						return true;
				}
			}

			return false;
		}

		public bool IsPossible(QuestId questId)
			=> this.IsPossible(questId.Value);

		/// <summary>
		/// Check if all prerequisites are met and the quest isn't started.
		/// </summary>
		/// <param name="questId"></param>
		/// <returns></returns>
		public bool IsPossible(long questId)
		{
			// Can't start a quest if a track is active.
			if (this.Character.Tracks.ActiveTrack != null)
				return false;

			lock (_syncLock)
			{
				foreach (var quest in _quests)
				{
					if (quest.Data.Id.Value != questId)
						continue;
					return quest.IsPossible;
				}
				if (this.TryGetNamespacedQuestId(questId, out var namespacedQuestId) && QuestScript.TryGet(namespacedQuestId, out var questScript))
				{
					for (var j = 0; j < questScript.Data.Prerequisites.Count; j++)
					{
						var prerequisite = questScript.Data.Prerequisites[j];
						if (!prerequisite.Met(this.Character))
							return false;
					}
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Returns true if the character has the quest, is slated to
		/// receive it soon, or has completed it in the past.
		/// </summary>
		/// <param name="questId"></param>
		/// <returns></returns>
		public bool Has(QuestId questId)
		{
			lock (_syncLock)
			{
				foreach (var quest in _quests)
				{
					if (quest.Data.Id != questId)
						continue;

					if (quest.Status > QuestStatus.Possible)
						return true;
				}
			}

			return false;
		}

		[Obsolete("Use Has(QuestId questId)")]
		public bool Has(long questId) => this.Has(new QuestId(questId));

		/// <summary>
		/// Returns true if the character meets the prerequisites to start the
		/// given quest.
		/// </summary>
		/// <param name="questNamespace"></param>
		/// <param name="id"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException">
		/// Thrown if no quest with the given id was found.
		/// </exception>
		public bool MeetsPrerequisites(string questNamespace, long id)
			=> this.MeetsPrerequisites(new QuestId(questNamespace, id));

		/// <summary>
		/// Returns true if the character meets the prerequisites to start the
		/// given quest.
		/// </summary>
		/// <param name="questId"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException">
		/// Thrown if no quest with the given id was found.
		/// </exception>
		public bool MeetsPrerequisites(QuestId questId)
		{
			if (!QuestScript.TryGet(questId, out var questScript))
			{
				if (ZoneServer.Instance.Data.QuestDb.TryFind((int)questId.Value, out var staticQuestData))
					return this.MeetsStaticPrerequisites(staticQuestData);

				throw new ArgumentException($"Quest '{questId}' not found.");
			}

			return this.MeetsPrerequisites(questScript);
		}

		/// <summary>
		/// Returns true if the character meets the prerequisites to start the
		/// given quest.
		/// </summary>
		/// <param name="questScript"></param>
		/// <returns></returns>
		internal bool MeetsPrerequisites(QuestScript questScript)
		{
			foreach (var prerequisite in questScript.Data.Prerequisites)
			{
				if (!prerequisite.Met(this.Character))
					return false;
			}

			return true;
		}

		/// <summary>
		/// Returns true if the character has ever completed the quest
		/// before.
		/// </summary>
		/// <param name="questId"></param>
		/// <returns></returns>
		public bool HasCompleted(QuestId questId)
		{
			lock (_syncLock)
			{
				foreach (var quest in _quests)
				{
					if (quest.Data.Id != questId)
						continue;

					if (quest.Status == QuestStatus.Completed)
						return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Returns true if the character has ever completed the quest
		/// before.
		/// </summary>
		/// <param name="questId"></param>
		/// <returns></returns>
		[Obsolete("Use HasCompleted(QuestId questId)")]
		public bool HasCompleted(long questId)
		{
			lock (_syncLock)
			{
				foreach (var quest in _quests)
				{
					if (quest.Data.Id.Value != questId)
						continue;

					if (quest.Status == QuestStatus.Completed)
						return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Completes the objective on all quests with the given id.
		/// </summary>
		/// <param name="questId"></param>
		/// <param name="objectiveIdent"></param>
		public void CompleteObjective(QuestId questId, string objectiveIdent)
		{
			lock (_syncLock)
			{
				for (var i = 0; i < _quests.Count; i++)
				{
					var quest = _quests[i];
					if (!quest.InProgress || quest.Data.Id != questId)
						continue;

					if (!quest.TryGetProgress(objectiveIdent, out var progress))
						continue;

					if (!progress.Done)
					{
						progress.SetDone();
						quest.UpdateUnlock();
						this.UpdateQuestProgress(questId, progress.Objective.Id);
						this.UpdateClient_UpdateQuest(quest);
					}
				}
			}
		}

		/// <summary>
		/// Completes the objective on all quests with the given id.
		/// </summary>
		/// <param name="questId"></param>
		/// <param name="objectiveIdent"></param>
		[Obsolete("Use CompleteObjective(QuestId questId)")]
		public void CompleteObjective(long questId, string objectiveIdent)
		{
			lock (_syncLock)
			{
				for (var i = 0; i < _quests.Count; i++)
				{
					var quest = _quests[i];
					if (!quest.InProgress || quest.Data.Id.Value != questId)
						continue;

					if (!quest.TryGetProgress(objectiveIdent, out var progress))
						continue;

					if (!progress.Done)
					{
						progress.SetDone();
						quest.UpdateUnlock();
						this.UpdateQuestProgress(questId, progress.Objective.Id);
						this.UpdateClient_UpdateQuest(quest);
					}
				}
			}
		}

		/// <summary>
		/// Completes the objective on all quests with the given id.
		/// </summary>
		/// <param name="questId"></param>
		/// <param name="objectiveIdent"></param>
		public bool Complete(QuestId questId)
		{
			lock (_syncLock)
			{
				foreach (var quest in _quests.ToList())
				{
					if (!_quests.Contains(quest))
						continue;

					if (!quest.InProgress || quest.Data.Id != questId)
						continue;

					quest.CompleteObjectives();
					this.Complete(quest);
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Completes all quests with the given id and gives the rewards
		/// to the character.
		/// </summary>
		/// <param name="questId"></param>
		[Obsolete("Use Complete(QuestId questId)")]
		public void Complete(long questId)
		{
			lock (_syncLock)
			{
				foreach (var quest in _quests.ToList())
				{
					if (!_quests.Contains(quest))
						continue;

					if (!quest.InProgress || quest.Data.Id.Value != questId)
						continue;

					quest.CompleteObjectives();

					this.Complete(quest);
				}
			}
		}

		/// <summary>
		/// Completes quest and gives rewards to character.
		/// </summary>
		/// <param name="quest"></param>
		public void Complete(Quest quest)
		{
			quest.Status = QuestStatus.Completed;
			quest.CompleteTime = DateTime.Now;
			quest.CompleteObjectives();

			if (QuestScript.TryGet(quest.Data.Id, out var questScript))
				questScript.OnComplete(this.Character, quest);

			this.GiveRewards(quest);

			// Track achievement points for quest completion via server event
			ZoneServer.Instance.ServerEvents.PlayerCompletedQuest.Raise(new PlayerCompletedQuestEventArgs(this.Character, (int)quest.Data.Id.Value));

			this.SyncStaticQuestSessionObject(quest);
			this.UpdateClient_RemoveQuest(quest);
			this.UpdateClient_CompleteQuest(quest);
			this.SyncTrackedQuestSlots();
			this.StartStaticSystemFollowUpQuests(quest);
			this.RepairPapayaCrystalMineSkipAfterMinersVillage(this.Character.Map?.ClassName);
			this.RepairPapayaCapturedMainQuestChain();
			this.RepairWestSiauliaiMainQuestState();
			this.SyncStaticQuestNpcStatesAfterDialog();
		}

		private void StartStaticSystemFollowUpQuests(Quest quest)
		{
			var completedQuestData = quest?.QuestStaticData;
			if (completedQuestData == null || string.IsNullOrWhiteSpace(completedQuestData.ClassName))
				return;

			var startedQuestNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var questAutoData = ZoneServer.Instance.Data.QuestAutoDb.Find(completedQuestData.ClassName);
			if (questAutoData?.SuccessNextQuestNames != null)
			{
				foreach (var nextQuestName in questAutoData.SuccessNextQuestNames)
				{
					if (this.IsNone(nextQuestName) || !ZoneServer.Instance.Data.QuestDb.TryFind(nextQuestName, out var nextQuest))
						continue;

					var nextQuestId = new QuestId(nextQuest.Id);
					if (this.Has(nextQuestId) ||
						!this.MeetsStaticPrerequisites(nextQuest) ||
						this.StaticQuestIsBlockedByPapayaMainProgression(nextQuest))
						continue;

					if (this.StaticMainFollowUpShouldStartImmediately(completedQuestData, nextQuest) ||
						!string.Equals(nextQuest.QuestStartMode, "NPCDIALOG", StringComparison.OrdinalIgnoreCase))
					{
						Log.Info("Static quest chain: starting Papaya success follow-up quest '{0}' after '{1}'.", nextQuest.ClassName, completedQuestData.ClassName);
						this.StartStaticQuest(nextQuest, TimeSpan.Zero);
						this.TrackPapayaMainFollowUpIfVisible(nextQuest);
						startedQuestNames.Add(nextQuest.ClassName);
					}
					else
					{
						if (this.ShouldKeepPapayaCapturedFollowUpPossible(completedQuestData, nextQuest))
							this.EnsureStaticQuestPossible(nextQuest, true);
						else
							this.SetStaticQuestProperty(nextQuest, QuestStatus.Possible);
						Log.Info("Static quest chain: made Papaya NPC follow-up quest '{0}' available after '{1}'.", nextQuest.ClassName, completedQuestData.ClassName);
					}
				}
			}

			var nextQuests = ZoneServer.Instance.Data.QuestDb.GetList()
				.Where(candidate =>
					string.Equals(candidate.QuestStartMode, "SYSTEM", StringComparison.OrdinalIgnoreCase) &&
					candidate.RequiredQuests != null &&
					candidate.RequiredQuests.Any(requiredQuest => string.Equals(requiredQuest, completedQuestData.ClassName, StringComparison.OrdinalIgnoreCase)) &&
					!startedQuestNames.Contains(candidate.ClassName) &&
					!this.Has(new QuestId(candidate.Id)) &&
					this.MeetsStaticPrerequisites(candidate) &&
					!this.StaticQuestIsBlockedByPapayaMainProgression(candidate))
				.OrderBy(candidate => candidate.Id)
				.ToList();

			foreach (var nextQuest in nextQuests)
			{
				this.StartStaticQuest(nextQuest, TimeSpan.Zero);
				this.TrackPapayaMainFollowUpIfVisible(nextQuest);
			}
		}

		/// <summary>
		/// Removes quest from quest log.
		/// </summary>
		/// <param name="quest"></param>
		public void Cancel(Quest quest)
		{
			quest.Status = QuestStatus.Abandoned;

			if (ZoneServer.Instance.Data.QuestDb.TryFind((int)quest.Data.Id.Value, out var questData) && !string.IsNullOrEmpty(quest.QuestStaticData.QuestProperty))
			{
				var main = this.Character.SessionObjects.Main;

				if (main.Properties.Has(quest.QuestStaticData.QuestProperty))
				{
					main.Properties.SetFloat(quest.QuestStaticData.QuestProperty, (int)quest.Status);
					Send.ZC_OBJECT_PROPERTY(this.Character, main, quest.QuestStaticData.QuestProperty);
				}
			}

			if (QuestScript.TryGet(quest.Data.Id, out var questScript))
				questScript.OnCancel(this.Character, quest);

			this.SyncStaticQuestSessionObject(quest);
			this.UpdateClient_RemoveQuest(quest);
			this.SyncTrackedQuestSlots();
		}

		/// <summary>
		/// Gives quest's rewards to character.
		/// </summary>
		/// <param name="quest"></param>
		private void GiveRewards(Quest quest)
		{
			foreach (var reward in quest.Data.Rewards)
				reward.Give(this.Character);
		}

		/// <summary>
		/// Abandon a quest
		/// </summary>
		/// <param name="questId"></param>
		/// <returns></returns>
		public bool Abandon(long questId)
		{
			if (!this.Has(questId) || !this.TryGet(questId, out var quest) || !quest.InProgress)
				return false;

			this.Cancel(quest);

			return true;
		}

		/// <summary>
		/// Restart a quest
		/// </summary>
		/// <param name="questId"></param>
		/// <returns></returns>
		public bool Restart(int questId, QuestStatus status = QuestStatus.Restarted)
		{
			if (!this.IsPossible(questId))
				return false;

			if (!this.TryGet(questId, out var quest))
				quest = Quest.Create(this.TryGetNamespacedQuestId(questId, out var namespacedQuestId) ? namespacedQuestId : new QuestId(questId));
			quest.Status = status;
			this.UpdateQuestStatus(questId, quest.Status);

			if (QuestScript.TryGet(quest.Data.Id, out var questScript))
				questScript.OnStart(this.Character, quest);

			return true;
		}

		public void UpdateQuestStatus(long questId, QuestStatus status)
		{
			lock (_syncLock)
			{
				foreach (var quest in _quests)
				{
					if (quest.Data.Id.Value != questId)
						continue;

					quest.Status = status;

					this.UpdateClient_UpdateQuest(quest);
					break;
				}
			}
		}

		/// <summary>
		/// Updates quests: starts pending quests, handles auto-receive,
		/// and checks for completion of location-based objectives.
		/// </summary>
		/// <param name="elapsed"></param>
		public void Update(TimeSpan elapsed)
		{
			var now = DateTime.Now;

			lock (_syncLock)
			{
				// --- 1. Start Pending Quests ---
				// Iterate backwards if removing, but here we are just starting
				// or modifying status, so forward is fine. Using ToList() to avoid collection modified issues if Start(quest) changes _quests.
				foreach (var quest in _quests.ToList()) // Iterate a copy if Start() can modify _quests
				{
					if (quest.Status == QuestStatus.Possible && quest.StartTime <= now) // Use <= for safety
					{
						Log.Debug($"QuestComponent: Starting delayed quest {quest.Data.Id.Value} for {Character.Name}.");
						this.Start(quest); // This updates status, client, etc.
					}
				}

				// --- 2. Check Location-Based Objectives (e.g., VisitLocationObjective) ---
				_timeSinceLastLocationCheck += elapsed;
				if (_timeSinceLastLocationCheck >= LocationCheckInterval)
				{
					_timeSinceLastLocationCheck -= LocationCheckInterval; // Reset timer correctly
					this.CheckVisitLocationObjectivesInternal(); // Call internal method
					this.CheckVariableCheckObjectivesInternal(); // Check variable-based objectives
					this.CheckStaticMainQuestRuntimeStateInternal();
				}
			}

			// --- 3. Handle Auto-Receive Quests (Outside main lock if QuestScript.StartAuto... is safe) ---
			_autoReceiveDelay = Math2.Max(TimeSpan.Zero, _autoReceiveDelay - elapsed);
			if (_autoReceiveDelay == TimeSpan.Zero)
			{
				QuestScript.StartAutoReceiveQuests(this.Character);
				_autoReceiveDelay = AutoReceiveDelay;
			}
		}

		private void CheckStaticMainQuestRuntimeStateInternal()
		{
			if (this.Character?.Connection == null || this.Character.Map == null)
				return;

			var mapClassName = this.Character.Map.ClassName;
			if (string.IsNullOrWhiteSpace(mapClassName))
				return;

			this.SyncTrackedQuestSlots();
			var startedTrack = this.TryStartStaticQuestAutoTracks(mapClassName);
			if (!startedTrack && this.Character.Tracks.ActiveTrack == null)
			{
				this.EnsureStaticQuestObjectiveMonsters(mapClassName);
				this.SyncStaticQuestObjectiveMonsterMarkers(mapClassName);
			}
		}

		/// <summary>
		/// Sends a list of all quests to the client to update it.
		/// </summary>
		public void UpdateClient()
		{
			var quests = this.GetList();
			var currentMap = this.Character?.Map?.ClassName ?? this.Character?.Map?.Data?.ClassName;
			Send.ZC_EXEC_CLIENT_SCP(this.Character.Connection, "if Melia ~= nil and Melia.Quests ~= nil and Melia.Quests.Clear ~= nil then Melia.Quests.Clear() end");

			foreach (var quest in quests.Where(a => this.QuestShouldBeVisibleInClientList(a, currentMap)))
			{
				// Re-check quest objectives to sync with current state (e.g., collection items in inventory)
				if (quest.InProgress)
					this.InitialChecks(quest);
				this.SyncStaticQuestSessionObject(quest);

				var questTable = this.QuestToTable(quest);

				var lua = "Melia.Quests.Restore(" + questTable.Serialize() + ")";
				Send.ZC_EXEC_CLIENT_SCP(this.Character.Connection, lua);
			}

			this.SyncTrackedQuestSlots();
		}

		/// <summary>
		/// Adds the quest to the client's quest log.
		/// </summary>
		/// <param name="quest"></param>
		private void UpdateClient_AddQuest(Quest quest)
		{
			if (!this.QuestShouldBeVisibleInClientList(quest))
			{
				this.HideQuestFromClientList(quest);
				this.SyncTrackedQuestSlots();
				return;
			}

			this.SyncTrackedQuestSlots();
			var questTable = this.QuestToTable(quest);

			if (ZoneServer.Instance.Data.QuestDb.TryFind((int)quest.Data.Id.Value, out var questData))
			{
				if (quest.QuestStaticData != null)
				{
					var main = this.Character.SessionObjects.Main;

					if (!string.IsNullOrWhiteSpace(quest.QuestStaticData.QStartZone)
						&& quest.QuestStaticData.QStartZone != main.Properties.GetString(PropertyName.QSTARTZONETYPE))
					{
						main.Properties.SetString(PropertyName.QSTARTZONETYPE, quest.QuestStaticData.QStartZone);
						Send.ZC_OBJECT_PROPERTY(this.Character, main, PropertyName.QSTARTZONETYPE);
					}
				}
				if (quest.SessionObjectStaticData != null)
				{
					var questSessionObject = this.Character.SessionObjects.GetOrCreate(quest.SessionObjectStaticData.Id);
					if (questSessionObject != null)
					{
						this.ApplyStaticQuestSessionObjectProperties(quest, questSessionObject);
						Send.ZC_SESSION_OBJ_ADD(this.Character, questSessionObject, quest.QuestStaticData.Id);
					}
					UpdateClient_UpdateQuest(quest);
				}
			}

			var lua = "Melia.Quests.Add(" + questTable.Serialize() + ")";
			Send.ZC_EXEC_CLIENT_SCP(this.Character.Connection, lua);
			this.NotifyNativeQuestTracking(quest, true);
			this.SyncStaticQuestNpcStatesAfterDialog();

			//Log.Debug(lua);
		}

		/// <summary>
		/// Updates the quest objectives on the client.
		/// </summary>
		/// <param name="quest"></param>
		public void UpdateClient_UpdateQuest(Quest quest)
		{
			if (!this.QuestShouldBeVisibleInClientList(quest))
			{
				this.HideQuestFromClientList(quest);
				this.SyncTrackedQuestSlots();
				return;
			}

			this.SyncTrackedQuestSlots();
			var objectivesTable = this.ObjectivesToTable(quest);
			var trackingPointsTable = this.TrackingPointsToTable(quest);
			var questDataFound = ZoneServer.Instance.Data.QuestDb.TryFind((int)quest.Data.Id.Value, out var questData);

			var questTable = new LuaTable();
			questTable.Insert("ObjectId", "0x" + quest.ObjectId.ToString("X16"));
			questTable.Insert("Status", quest.Status.ToString());
			questTable.Insert("Done", quest.ObjectivesCompleted);
			questTable.Insert("Tracked", quest.Tracked);
			questTable.Insert("Objectives", objectivesTable);
			questTable.Insert("TrackingPoints", trackingPointsTable);

			if (questDataFound && !string.IsNullOrEmpty(quest.QuestStaticData?.QuestProperty))
			{
				var main = this.Character.SessionObjects.Main;

				if (!main.Properties.Has(quest.QuestStaticData.QuestProperty))
				{
					main.Properties.SetFloat(quest.QuestStaticData.QuestProperty, 1);
					Send.ZC_OBJECT_PROPERTY(this.Character, main, quest.QuestStaticData.QuestProperty);
				}
				main.Properties.SetFloat(quest.QuestStaticData.QuestProperty, (int)quest.Status);
				Send.ZC_OBJECT_PROPERTY(this.Character, main, quest.QuestStaticData.QuestProperty);
			}

			if (questDataFound && quest.SessionObjectStaticData != null)
			{
				var questSessionObject = this.Character.SessionObjects.GetOrCreate(quest.SessionObjectStaticData.Id);
				foreach (var propertyName in this.ApplyStaticQuestSessionObjectProperties(quest, questSessionObject))
					Send.ZC_OBJECT_PROPERTY(this.Character, questSessionObject, propertyName);
			}

			var lua = "Melia.Quests.Update(" + questTable.Serialize() + ")";
			Send.ZC_EXEC_CLIENT_SCP(this.Character.Connection, lua);
			this.NotifyNativeQuestTracking(quest, false);
			this.SyncStaticQuestNpcStatesAfterDialog();

			//Log.Debug(lua);
		}

		private void NotifyNativeQuestTracking(Quest quest, bool isNewQuest, int attempt = 0)
		{
			if (quest?.QuestStaticData == null || this.Character.Connection == null)
				return;

			if (!this.QuestShouldBeVisibleInClientList(quest))
				return;

			if (!this.StaticQuestShouldNotifyNativeQuestState(quest.QuestStaticData))
				return;

			this.NotifyNativeQuestStateChanged((int)quest.Data.Id.Value, isNewQuest, attempt);
		}

		private void NotifyNativeStaticQuestStateChanged(QuestStaticData questData, bool isNewQuest, int attempt = 0)
		{
			if (questData == null || this.Character.Connection == null)
				return;

			if (!this.StaticQuestShouldNotifyNativeQuestState(questData))
				return;

			this.NotifyNativeQuestStateChanged(questData.Id, isNewQuest, attempt);
		}

		private bool StaticQuestShouldNotifyNativeQuestState(QuestStaticData questData)
		{
			if (questData == null)
				return false;

			if (this.StaticQuestNativeTrackingDisabled(questData.ClassName))
				return false;

			if (this.StaticQuestDisabledForCloverFlow(questData))
				return false;

			if (this.StaticQuestIsClientHiddenPapayaBridge(questData))
				return false;

			if (this.StaticQuestIsBlockedByPapayaMainProgression(questData))
				return false;

			return true;
		}

		private bool StaticQuestIsClientHiddenPapayaBridge(QuestStaticData questData)
		{
			if (questData == null)
				return false;

			if (!this.IsPapayaCapturedMainQuest(questData, out _))
				return false;

			if (questData.Level >= 9999)
				return !string.Equals(questData.ClassName, "KLAPEDA_GO_TO_EAST", StringComparison.OrdinalIgnoreCase);

			return string.Equals(questData.ClassName, "SIAUL_EAST_REQUEST3", StringComparison.OrdinalIgnoreCase);
		}

		private void NotifyNativeQuestStateChanged(int questId, bool isNewQuest, int attempt = 0)
		{
			if (questId <= 0 || this.Character.Connection == null)
				return;

			if (this.Character.Connection.CurrentDialog != null)
			{
				if (attempt >= 12)
				{
					Log.Info("Native quest tracker: forcing deferred quest update {0} for '{1}' after dialog stayed active through {2} retries.", questId, this.Character.Name, attempt);
				}
				else
				{
					var retryDelay = Math.Min(2500, 450 + attempt * 250);

					_ = Task.Run(async () =>
					{
						await Task.Delay(retryDelay);
						if (this.Character?.Connection != null)
							this.NotifyNativeQuestStateChanged(questId, isNewQuest, attempt + 1);
					});
					return;
				}
			}

			if (isNewQuest)
				this.Character.AddonMessage("GET_NEW_QUEST", "None", questId);

			this.Character.AddonMessage("S_OBJ_UPDATE", "None", questId);
			this.Character.AddonMessage("QUEST_UPDATE", "None", questId);
			this.Character.AddonMessage(AddonMessage.ON_QUEST_UPDATED, "None", questId);
			this.Character.RestoreCoreHudState(true, true);

			// The Papaya DX11 client can crash when these Lua helpers are injected while
			// quest/session-object state is still being rebuilt after an NPC dialog.
			// Native quest/session object packets above are enough to keep progression
			// functional, so avoid forcing client-side minimap monster registration here.
		}

		private IEnumerable<string> GetActiveQuestMonsterTargets(Quest quest)
		{
			var questStaticData = quest?.QuestStaticData;
			if (questStaticData?.Objectives == null || !quest.InProgress)
				yield break;

			foreach (var objectiveData in questStaticData.Objectives)
			{
				if (objectiveData == null || !quest.TryGetProgress(objectiveData.Ident, out var progress))
					continue;

				if (progress.Done || !progress.Unlocked)
					continue;

				var isKill = string.Equals(objectiveData.Type, "Kill", StringComparison.OrdinalIgnoreCase);
				var isCollect = string.Equals(objectiveData.Type, "Collect", StringComparison.OrdinalIgnoreCase);
				if (!isKill && !isCollect)
					continue;

				var target = isCollect && !this.IsNone(objectiveData.DropTarget)
					? objectiveData.DropTarget
					: objectiveData.Target;

				if (this.IsNone(target) || string.Equals(target, "ALL", StringComparison.OrdinalIgnoreCase))
					continue;

				yield return target.Trim();
			}
		}

		private string EscapeLuaString(string value)
			=> (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'");

		private List<string> ApplyStaticQuestSessionObjectProperties(Quest quest, SessionObject sessionObject)
		{
			var changedProperties = new List<string>();
			var questData = quest.SessionObjectStaticData?.QuestData;
			if (questData == null)
				return changedProperties;

			var infoNames = this.GetQuestInfoNames(quest, questData);
			var infoViewTypes = this.GetQuestInfoViewTypes(infoNames, questData);
			var infoMaxCounts = this.GetQuestInfoMaxCounts(quest, questData, infoNames.Count);
			this.SetQuestSessionStringList(sessionObject, "QuestInfoName", infoNames, 10, changedProperties);
			this.SetQuestSessionStringList(sessionObject, "QuestInfoViewType", infoViewTypes, 10, changedProperties);
			this.SetQuestSessionNumberList(sessionObject, "QuestInfoMaxCount", infoMaxCounts, 10, changedProperties);
			this.SetQuestInfoValueDefaults(sessionObject, infoMaxCounts.Count, changedProperties);

			var mapPointGroups = this.GetQuestMapPointGroups(quest, questData);
			var mapPointViews = this.GetQuestMapPointViews(mapPointGroups, questData);
			var mapPointViewTerms = this.GetQuestMapPointViewTerms(mapPointGroups, questData);
			this.SetQuestSessionStringList(sessionObject, "QuestMapPointGroup", mapPointGroups, 10, changedProperties);
			this.SetQuestSessionNumberList(sessionObject, "QuestMapPointView", mapPointViews, 10, changedProperties);
			this.SetQuestSessionStringList(sessionObject, "QuestMapPointViewTerms", mapPointViewTerms, 10, changedProperties);
			this.LogQuestMapPointSync(quest, sessionObject, mapPointGroups, mapPointViews, changedProperties);

			var monsterNameGroups = this.GetQuestMonsterNameGroups(quest, questData);
			var monsterViews = this.GetQuestMonsterViews(monsterNameGroups, questData);
			this.SetQuestSessionStringList(sessionObject, "QuestMonNameGroup", monsterNameGroups, 10, changedProperties);
			this.SetQuestSessionNumberList(sessionObject, "QuestMonView", monsterViews, 10, changedProperties);
			this.SetQuestSessionStringList(sessionObject, "QuestMonViewTerms", questData.MonsterViewTerms, 10, changedProperties);

			return changedProperties;
		}

		private void SyncStaticQuestSessionObjects()
		{
			foreach (var quest in this.GetList().Where(quest =>
				(quest.InProgress || quest.Status == QuestStatus.Success) &&
				(quest.QuestStaticData == null ||
				 (!this.StaticQuestDisabledForCloverFlow(quest.QuestStaticData) &&
				  !this.StaticQuestIsClientHiddenPapayaBridge(quest.QuestStaticData) &&
				  !this.StaticQuestIsBlockedByPapayaMainProgression(quest.QuestStaticData)))))
				this.SyncStaticQuestSessionObject(quest);
		}

		private void SyncStaticQuestSessionObject(Quest quest)
		{
			if (quest.SessionObjectStaticData == null)
				return;

			var questSessionObject = this.Character.SessionObjects.GetOrCreate(quest.SessionObjectStaticData.Id);
			foreach (var propertyName in this.ApplyStaticQuestSessionObjectProperties(quest, questSessionObject))
				Send.ZC_OBJECT_PROPERTY(this.Character, questSessionObject, propertyName);
		}

		private bool RemoveStaticQuestSessionObject(QuestStaticData questData)
		{
			if (questData == null || string.IsNullOrWhiteSpace(questData.QuestSSN))
				return false;

			if (!ZoneServer.Instance.Data.SessionObjectDb.TryFind(questData.QuestSSN, out var sessionObjectData))
				return false;

			var removed = this.Character.SessionObjects.Remove(sessionObjectData.Id);
			if (this.Character.Connection != null)
				Send.ZC_SESSION_OBJ_REMOVE(this.Character, sessionObjectData.Id);

			return removed;
		}

		private void RemoveStaticQuestFromClientList(int questId, string questClassName)
		{
			if (this.Character?.Connection == null)
				return;

			var className = (questClassName ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'");
			var lua =
				"if Melia ~= nil and Melia.Quests ~= nil and Melia.Quests.GetAll ~= nil and Melia.Quests.Remove ~= nil then " +
				"local list = Melia.Quests.GetAll(); " +
				"for i = #list, 1, -1 do " +
				"local q = list[i]; " +
				"local id = q.ClassId; " +
				"if type(id) == 'string' then local h = string.match(id, '0x(%x+)'); if h ~= nil then id = tonumber(h, 16) else id = tonumber(id) end end; " +
				$"if id == {questId} or q.ClassName == '{className}' then Melia.Quests.Remove(q.ObjectId) end; " +
				"end; " +
				"end";
			Send.ZC_EXEC_CLIENT_SCP(this.Character.Connection, lua);
		}

		private void HideQuestFromClientList(Quest quest)
		{
			if (quest == null)
				return;

			quest.Tracked = false;

			if (quest.QuestStaticData == null || this.Character?.Connection == null)
				return;

			this.RemoveStaticQuestFromClientList((int)quest.Data.Id.Value, quest.QuestStaticData.ClassName);
		}

		private bool SetStaticQuestProperty(QuestStaticData questData, QuestStatus status)
		{
			if (questData == null || string.IsNullOrWhiteSpace(questData.QuestProperty))
				return false;

			var main = this.Character.SessionObjects.Main;
			var value = (int)status;

			if (main.Properties.Has(questData.QuestProperty) &&
				Math.Abs(main.Properties.GetFloat(questData.QuestProperty) - value) < 0.001f)
				return false;

			main.Properties.SetFloat(questData.QuestProperty, value);
			if (this.Character.Connection != null)
			{
				Send.ZC_OBJECT_PROPERTY(this.Character, main, questData.QuestProperty);
				this.NotifyNativeStaticQuestStateChanged(questData, false);
			}

			return true;
		}

		private List<string> GetQuestInfoNames(Quest quest, SessionQuestData questData)
		{
			if (quest.Status == QuestStatus.Completed || quest.Status == QuestStatus.Abandoned)
				return new List<string>();

			var questStaticData = quest.QuestStaticData;
			if (questStaticData != null && quest.Status == QuestStatus.Success && !this.IsNone(questStaticData.Name))
				return new List<string> { questStaticData.Name.Trim() };

			var result = questData.InfoName != null
				? questData.InfoName.Where(name => !this.IsNone(name)).ToList()
				: new List<string>();

			if (result.Count != 0)
				return result;

			if (questStaticData?.Objectives != null && quest.InProgress)
			{
				foreach (var objectiveData in questStaticData.Objectives)
				{
					if (!quest.TryGetProgress(objectiveData.Ident, out var progress) || progress.Done || !progress.Unlocked)
						continue;

					if (!this.IsNone(objectiveData.Text))
						result.Add(objectiveData.Text.Trim());
				}
			}

			if (result.Count == 0 && !this.IsNone(questStaticData?.Name))
				result.Add(questStaticData.Name.Trim());

			return result;
		}

		private List<string> GetQuestInfoViewTypes(List<string> infoNames, SessionQuestData questData)
		{
			var result = questData.InfoViewType != null
				? questData.InfoViewType.Where(viewType => !this.IsNone(viewType)).ToList()
				: new List<string>();

			while (result.Count < infoNames.Count)
				result.Add("None");

			return result;
		}

		private List<int> GetQuestInfoMaxCounts(Quest quest, SessionQuestData questData, int infoCount)
		{
			if (quest.Status == QuestStatus.Completed || quest.Status == QuestStatus.Abandoned)
				return new List<int>();

			var result = questData.InfoMaxCount != null
				? questData.InfoMaxCount.Where(count => count > 0).ToList()
				: new List<int>();

			if (result.Count != 0)
				return result;

			var questStaticData = quest.QuestStaticData;
			if (questStaticData?.Objectives != null && quest.InProgress)
			{
				foreach (var objectiveData in questStaticData.Objectives)
				{
					if (!quest.TryGetProgress(objectiveData.Ident, out var progress) || progress.Done || !progress.Unlocked)
						continue;

					result.Add(Math.Max(1, objectiveData.Count));
				}
			}

			while (result.Count < infoCount)
				result.Add(1);

			return result;
		}

		private List<string> GetQuestMapPointGroups(Quest quest, SessionQuestData questData)
		{
			if (quest.Status == QuestStatus.Completed || quest.Status == QuestStatus.Abandoned)
				return new List<string>();

			var questStaticData = quest.QuestStaticData;
			var mapPointTrackingDisabled = questStaticData != null &&
				this.StaticQuestMapPointTrackingDisabled(questStaticData.ClassName);

			if (mapPointTrackingDisabled)
				return new List<string>();

			if (questStaticData != null && quest.Status == QuestStatus.Success)
			{
				var successResult = new List<string>();
				var currentMap = this.Character?.Map?.ClassName ?? questStaticData.EndMap;
				this.AddResolvedQuestMapPointGroups(successResult, questStaticData.EndLocation, currentMap);
				this.AddResolvedStaticQuestRoleMapPointGroup(successResult, questStaticData, currentMap, questStaticData.EndMap, questStaticData.EndNPC);
				if (successResult.Count == 0)
					this.AddQuestMapPointGroup(successResult, questStaticData.EndLocation, questStaticData.EndMap, questStaticData.EndNPC);
				return this.FilterQuestMapPointGroupsWithFallback(quest, successResult);
			}

			if (questStaticData != null && quest.IsPossible)
			{
				var possibleResult = new List<string>();
				var currentMap = this.Character?.Map?.ClassName ?? questStaticData.StartMap;
				this.AddResolvedQuestMapPointGroups(possibleResult, questStaticData.StartLocation, currentMap);
				this.AddResolvedStaticQuestRoleMapPointGroup(possibleResult, questStaticData, currentMap, questStaticData.StartMap, questStaticData.StartNPC);
				if (possibleResult.Count == 0)
					this.AddQuestMapPointGroup(possibleResult, questStaticData.StartLocation, questStaticData.StartMap, questStaticData.StartNPC);
				return this.FilterQuestMapPointGroupsWithFallback(quest, possibleResult);
			}

			var result = questData.MapPointGroup != null
				? questData.MapPointGroup.Where(group => !this.IsNone(group)).ToList()
				: new List<string>();

			if (result.Count != 0)
			{
				var filteredQuestDataPoints = this.FilterClientSafeQuestMapPointGroups(result);
				if (filteredQuestDataPoints.Count != 0)
					return filteredQuestDataPoints;
			}

			var privateMapPointGroups = this.GetPrivateEncounterMapPointGroups(quest);
			if (privateMapPointGroups.Count != 0)
			{
				var filteredPrivatePoints = this.FilterQuestMapPointGroupsWithFallback(quest, privateMapPointGroups);
				if (filteredPrivatePoints.Count != 0)
					return filteredPrivatePoints;
			}

			if (questStaticData == null)
				return result;

			if (quest.InProgress)
			{
				var currentMap = this.Character?.Map?.ClassName ?? questStaticData.ProgMap;
				this.AddResolvedQuestMapPointGroups(result, questStaticData.ProgLocation, currentMap);
				this.AddResolvedStaticQuestRoleMapPointGroup(result, questStaticData, currentMap, questStaticData.ProgMap, questStaticData.ProgNPC);
				if (result.Count == 0)
					this.AddQuestMapPointGroup(result, questStaticData.ProgLocation, questStaticData.ProgMap, questStaticData.ProgNPC);

				var filteredProgressPoints = this.FilterQuestMapPointGroupsWithFallback(quest, result);
				if (filteredProgressPoints.Count != 0)
					return filteredProgressPoints;

				return filteredProgressPoints;
			}

			return this.FilterQuestMapPointGroupsWithFallback(quest, result);
		}

		private List<string> FilterQuestMapPointGroupsWithFallback(Quest quest, List<string> mapPointGroups)
		{
			var filtered = this.FilterClientSafeQuestMapPointGroups(mapPointGroups ?? new List<string>());
			var currentMap = this.Character?.Map?.ClassName;
			if (filtered.Count != 0 && this.MapPointGroupsReferenceCurrentMap(filtered, currentMap))
				return filtered;

			var successNextPoints = this.GetPapayaSuccessNextMapPointGroups(quest, currentMap);
			if (successNextPoints.Count != 0)
				return successNextPoints;

			var routePoints = this.GetStaticQuestRouteFallbackMapPointGroups(quest, currentMap);
			if (routePoints.Count != 0)
				return routePoints;

			return filtered;
		}

		private bool MapPointGroupsReferenceCurrentMap(List<string> mapPointGroups, string currentMap)
		{
			if (string.IsNullOrWhiteSpace(currentMap) || mapPointGroups == null || mapPointGroups.Count == 0)
				return true;

			foreach (var mapPointGroup in mapPointGroups)
			{
				if (this.IsNone(mapPointGroup))
					continue;

				var parts = mapPointGroup.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 0)
					continue;

				if (string.Equals(parts[0], currentMap, StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return false;
		}

		private List<string> GetPapayaSuccessNextMapPointGroups(Quest quest, string mapClassName)
		{
			var result = new List<string>();
			var questData = quest?.QuestStaticData;
			if (questData == null || string.IsNullOrWhiteSpace(mapClassName))
				return result;

			var questAutoData = ZoneServer.Instance.Data.QuestAutoDb.Find(questData.ClassName);
			if (questAutoData?.SuccessNextQuestNames == null)
				return result;

			foreach (var nextQuestName in questAutoData.SuccessNextQuestNames)
			{
				if (this.IsNone(nextQuestName) ||
					!ZoneServer.Instance.Data.QuestDb.TryFind(nextQuestName, out var nextQuestData) ||
					this.StaticQuestDisabledForCloverFlow(nextQuestData) ||
					!this.StaticQuestReferencesMap(nextQuestData, mapClassName))
					continue;

				this.AddResolvedQuestMapPointGroups(result, nextQuestData.StartLocation, mapClassName);
				this.AddResolvedStaticQuestRoleMapPointGroup(result, nextQuestData, mapClassName, nextQuestData.StartMap, nextQuestData.StartNPC);

				if (result.Count == 0)
					this.AddResolvedQuestMapPointGroups(result, nextQuestData.ProgLocation, mapClassName);
				if (result.Count == 0)
					this.AddResolvedStaticQuestRoleMapPointGroup(result, nextQuestData, mapClassName, nextQuestData.ProgMap, nextQuestData.ProgNPC);
				if (result.Count == 0)
					this.AddResolvedQuestMapPointGroups(result, nextQuestData.EndLocation, mapClassName);
				if (result.Count == 0)
					this.AddResolvedStaticQuestRoleMapPointGroup(result, nextQuestData, mapClassName, nextQuestData.EndMap, nextQuestData.EndNPC);

				if (result.Count != 0)
					break;
			}

			return this.FilterClientSafeQuestMapPointGroups(result)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		private List<string> GetStaticQuestRouteFallbackMapPointGroups(Quest quest, string mapClassName)
		{
			var result = new List<string>();
			var questData = quest?.QuestStaticData;
			if (questData == null || string.IsNullOrWhiteSpace(mapClassName) || this.Character?.Map == null)
				return result;

			var targetMaps = this.GetStaticQuestTargetMapsForStatus(quest, questData)
				.Where(targetMap => !string.Equals(targetMap, mapClassName, StringComparison.OrdinalIgnoreCase))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);
			if (targetMaps.Count == 0)
				return result;

			foreach (var warp in this.Character.Map.GetWarps(warp => targetMaps.Contains(warp.DestinationMapName)))
			{
				result.Add(string.Format(
					CultureInfo.InvariantCulture,
					"{0} {1:0.###} {2:0.###} {3:0.###} 125",
					mapClassName,
					warp.Position.X,
					warp.Position.Y,
					warp.Position.Z
				));
			}

			return this.FilterClientSafeQuestMapPointGroups(result)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		private IEnumerable<string> GetStaticQuestTargetMapsForStatus(Quest quest, QuestStaticData questData)
		{
			if (quest.Status == QuestStatus.Success)
			{
				foreach (var mapName in this.GetStaticQuestMapNames(questData.EndMap, questData.EndLocation))
					yield return mapName;
				yield break;
			}

			if (quest.IsPossible)
			{
				foreach (var mapName in this.GetStaticQuestMapNames(questData.StartMap, questData.StartLocation))
					yield return mapName;
				yield break;
			}

			foreach (var mapName in this.GetStaticQuestMapNames(questData.ProgMap, questData.ProgLocation))
				yield return mapName;
			foreach (var mapName in this.GetStaticQuestMapNames(questData.EndMap, questData.EndLocation))
				yield return mapName;
		}

		private IEnumerable<string> GetStaticQuestMapNames(string mapName, string location)
		{
			if (!string.IsNullOrWhiteSpace(mapName) && ZoneServer.Instance.Data.MapDb.Contains(mapName))
				yield return mapName;

			if (string.IsNullOrWhiteSpace(location))
				yield break;

			foreach (var part in location.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			{
				if (ZoneServer.Instance.Data.MapDb.Contains(part))
					yield return part;
			}
		}

		private List<string> FilterClientSafeQuestMapPointGroups(List<string> mapPointGroups)
		{
			return mapPointGroups
				.Where(group => !this.IsUnsafeClientQuestMapPointGroup(group))
				.ToList();
		}

		private bool IsUnsafeClientQuestMapPointGroup(string mapPointGroup)
		{
			if (this.IsNone(mapPointGroup))
				return false;

			var parts = mapPointGroup.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 2)
				return false;

			return !double.TryParse(parts[1], out _);
		}

		private void LogQuestMapPointSync(Quest quest, SessionObject sessionObject, List<string> mapPointGroups, List<int> mapPointViews, List<string> changedProperties)
		{
			if (quest?.QuestStaticData == null || changedProperties == null)
				return;

			if (!changedProperties.Any(propertyName => propertyName.StartsWith("QuestMapPoint", StringComparison.OrdinalIgnoreCase)))
				return;

			Log.Info(
				"Static quest map sync: quest '{0}' status {1}, session object {2}, first point '{3}', first view {4}.",
				quest.QuestStaticData.ClassName,
				quest.Status,
				sessionObject.Id,
				mapPointGroups.FirstOrDefault() ?? "None",
				mapPointViews.FirstOrDefault()
			);
		}

		private List<string> GetPrivateEncounterMapPointGroups(Quest quest)
		{
			if (quest?.QuestStaticData == null || !quest.InProgress)
				return new List<string>();

			var mapName = !this.IsNone(quest.QuestStaticData.ProgMap)
				? quest.QuestStaticData.ProgMap
				: this.Character.Map?.ClassName;

			return ZoneServer.Instance.Data.PrivateEncounterDb
				.FindByQuestAndMap(quest.QuestStaticData.ClassName, mapName)
				.SelectMany(encounter => encounter.MapPointGroup ?? Enumerable.Empty<string>())
				.Where(group => !this.IsNone(group))
				.ToList();
		}

		private bool UsesMonsterObjectiveTracking(Quest quest)
		{
			if (quest?.QuestStaticData?.Objectives == null || !quest.InProgress)
				return false;

			foreach (var objectiveData in quest.QuestStaticData.Objectives)
			{
				if (objectiveData == null || !quest.TryGetProgress(objectiveData.Ident, out var progress))
					continue;

				if (progress.Done || !progress.Unlocked)
					continue;

				var target = this.GetStaticObjectiveMonsterTarget(objectiveData);
				if (!this.IsNone(target) && !string.Equals(target, "ALL", StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return false;
		}

		private List<int> GetQuestMapPointViews(List<string> mapPointGroups, SessionQuestData questData)
		{
			var result = new List<int>();
			for (var i = 0; i < mapPointGroups.Count; i++)
			{
				var view = questData.MapPointView != null && i < questData.MapPointView.Count
					? questData.MapPointView[i]
					: 1;
				result.Add(view == 0 ? 0 : 1);
			}
			return result;
		}

		private List<string> GetQuestMapPointViewTerms(List<string> mapPointGroups, SessionQuestData questData)
		{
			var result = questData.MapPointViewTerms != null
				? questData.MapPointViewTerms.Where(terms => !this.IsNone(terms)).ToList()
				: new List<string>();

			while (result.Count < mapPointGroups.Count)
				result.Add("None");

			return result;
		}

		private void AddQuestMapPointGroup(List<string> result, string location, string mapName, string npcName)
		{
			if (!this.IsNone(location))
			{
				result.Add(location.Trim());
				return;
			}

			if (!this.IsNone(mapName) && !this.IsNone(npcName))
				result.Add($"{mapName.Trim()} {npcName.Trim()} 100");
		}

		private void AddResolvedQuestMapPointGroups(List<string> result, string location, string mapClassName)
		{
			if (result == null || string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(mapClassName))
				return;

			foreach (var point in this.GetStaticQuestLocationPoints(location, mapClassName))
			{
				result.Add(string.Format(
					CultureInfo.InvariantCulture,
					"{0} {1:0.###} {2:0.###} {3:0.###} {4:0.###}",
					mapClassName,
					point.X,
					point.Y,
					point.Z,
					point.Range
				));
			}
		}

		private void AddResolvedStaticQuestRoleMapPointGroup(List<string> result, QuestStaticData questData, string mapClassName, string roleMap, string roleNpc)
		{
			if (result == null ||
				questData == null ||
				!this.StaticQuestMapMatches(roleMap, mapClassName) ||
				this.IsNone(roleNpc))
				return;

			if (!this.TryResolveStaticNpcPosition(questData, roleNpc, mapClassName, out var x, out var y, out var z, out var range))
				return;

			result.Add(string.Format(
				CultureInfo.InvariantCulture,
				"{0} {1:0.###} {2:0.###} {3:0.###} {4:0.###}",
				mapClassName,
				x,
				y,
				z,
				range > 0 ? range : 100
			));
		}

		private List<string> GetQuestMonsterNameGroups(Quest quest, SessionQuestData questData)
		{
			if (quest.Status == QuestStatus.Completed || quest.Status == QuestStatus.Abandoned)
				return new List<string>();

			var privateMonsterNameGroups = this.GetPrivateEncounterMonsterNameGroups(quest);
			if (privateMonsterNameGroups.Count != 0)
				return privateMonsterNameGroups;

			var result = questData.MonsterNameGroup != null
				? questData.MonsterNameGroup.Where(group => !this.IsNone(group)).ToList()
				: new List<string>();

			if (result.Count != 0 || !quest.InProgress)
				return result;

			var questStaticData = quest.QuestStaticData;
			if (questStaticData?.Objectives == null)
				return result;

			foreach (var objectiveData in questStaticData.Objectives)
			{
				var monsterNameGroup = objectiveData.Target;
				if (string.Equals(objectiveData.Type, "Collect", StringComparison.OrdinalIgnoreCase) && !this.IsNone(objectiveData.DropTarget))
					monsterNameGroup = objectiveData.DropTarget;

				if (!string.Equals(objectiveData.Type, "Kill", StringComparison.OrdinalIgnoreCase)
					&& !string.Equals(objectiveData.Type, "Collect", StringComparison.OrdinalIgnoreCase))
					continue;

				if (this.IsNone(monsterNameGroup) || string.Equals(monsterNameGroup, "ALL", StringComparison.OrdinalIgnoreCase))
					continue;

				result.Add(monsterNameGroup.Trim());
			}

			return result;
		}

		private List<string> GetPrivateEncounterMonsterNameGroups(Quest quest)
		{
			if (quest?.QuestStaticData == null || !quest.InProgress)
				return new List<string>();

			var mapName = !this.IsNone(quest.QuestStaticData.ProgMap)
				? quest.QuestStaticData.ProgMap
				: this.Character.Map?.ClassName;

			return ZoneServer.Instance.Data.PrivateEncounterDb
				.FindByQuestAndMap(quest.QuestStaticData.ClassName, mapName)
				.Select(encounter => encounter.Target)
				.Where(target => !this.IsNone(target))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		private List<int> GetQuestMonsterViews(List<string> monsterNameGroups, SessionQuestData questData)
		{
			var result = new List<int>();
			for (var i = 0; i < monsterNameGroups.Count; i++)
			{
				var view = questData.MonsterView != null && i < questData.MonsterView.Count
					? questData.MonsterView[i]
					: 1;
				result.Add(view == 0 ? 0 : 1);
			}
			return result;
		}

		private void SetQuestInfoValueDefaults(SessionObject sessionObject, int count, List<string> changedProperties)
		{
			for (var i = 1; i <= count && i <= 10; i++)
			{
				var propertyName = $"QuestInfoValue{i}";
				if (!sessionObject.Properties.Has(propertyName))
					this.SetQuestSessionNumber(sessionObject, propertyName, 0, changedProperties);
			}
		}

		private void SetQuestSessionStringList(SessionObject sessionObject, string propertyPrefix, List<string> values, int maxCount, List<string> changedProperties)
		{
			for (var i = 1; i <= maxCount; i++)
			{
				var propertyName = $"{propertyPrefix}{i}";
				var value = values != null && i <= values.Count && !this.IsNone(values[i - 1])
					? values[i - 1]
					: "None";

				this.SetQuestSessionString(sessionObject, propertyName, value, changedProperties);
			}
		}

		private void SetQuestSessionNumberList(SessionObject sessionObject, string propertyPrefix, List<int> values, int maxCount, List<string> changedProperties)
		{
			for (var i = 1; i <= maxCount; i++)
			{
				var propertyName = $"{propertyPrefix}{i}";
				var value = values != null && i <= values.Count ? values[i - 1] : 0;
				this.SetQuestSessionNumber(sessionObject, propertyName, value, changedProperties);
			}
		}

		private void SetQuestSessionString(SessionObject sessionObject, string propertyName, string value, List<string> changedProperties)
		{
			if (!PropertyTable.Exists("SessionObject", propertyName))
				return;

			value ??= "None";
			if (sessionObject.Properties.Has(propertyName) && sessionObject.Properties.GetString(propertyName) == value)
				return;

			sessionObject.Properties.SetString(propertyName, value);
			changedProperties.Add(propertyName);
		}

		private void SetQuestSessionNumber(SessionObject sessionObject, string propertyName, float value, List<string> changedProperties)
		{
			if (!PropertyTable.Exists("SessionObject", propertyName))
				return;

			if (sessionObject.Properties.Has(propertyName) && Math.Abs(sessionObject.Properties.GetFloat(propertyName) - value) < 0.001f)
				return;

			sessionObject.Properties.SetFloat(propertyName, value);
			changedProperties.Add(propertyName);
		}

		private bool IsNone(string value)
			=> string.IsNullOrWhiteSpace(value) || value.Equals("None", StringComparison.OrdinalIgnoreCase);

		/// <summary>
		/// Removes the quest from the client's quest log.
		/// </summary>
		/// <param name="quest"></param>
		private void UpdateClient_RemoveQuest(Quest quest)
		{
			var questDataFound = ZoneServer.Instance.Data.QuestDb.TryFind((int)quest.Data.Id.Value, out var questData);
			if (questDataFound && quest.SessionObjectStaticData != null)
			{
				this.Character.SessionObjects.Remove(quest.SessionObjectStaticData.Id);
				Send.ZC_SESSION_OBJ_REMOVE(this.Character, quest.SessionObjectStaticData.Id);
			}

			var lua = $"Melia.Quests.Remove('{quest.ObjectIdStr}')";
			Send.ZC_EXEC_CLIENT_SCP(this.Character.Connection, lua);
			if (questDataFound)
			{
				this.RemoveStaticQuestFromClientList((int)quest.Data.Id.Value, questData.ClassName);
				this.NotifyNativeStaticQuestStateChanged(questData, false);
			}
		}

		/// <summary>
		/// Notifies the client that the quest was completed.
		/// </summary>
		/// <param name="quest"></param>
		private void UpdateClient_CompleteQuest(Quest quest)
		{
			if (ZoneServer.Instance.Data.QuestDb.TryFind((int)quest.Data.Id.Value, out var questData) && !string.IsNullOrEmpty(questData.QuestProperty))
			{
				var main = this.Character.SessionObjects.Main;
				var propertyName = questData.QuestProperty;

				main.Properties.SetFloat(propertyName, (float)QuestStatus.Completed);
				Send.ZC_OBJECT_PROPERTY(this.Character, main, propertyName);
			}

			var lua = $"Melia.Quests.Remove('{quest.ObjectIdStr}')";
			Send.ZC_EXEC_CLIENT_SCP(this.Character.Connection, lua);
			if (ZoneServer.Instance.Data.QuestDb.TryFind((int)quest.Data.Id.Value, out var completedQuestData))
			{
				this.RemoveStaticQuestFromClientList((int)quest.Data.Id.Value, completedQuestData.ClassName);
				this.NotifyNativeStaticQuestStateChanged(completedQuestData, false);
			}
		}

		/// <summary>
		/// Returns all information about the quest as a Lua table.
		/// </summary>
		/// <param name="quest"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		private LuaTable QuestToTable(Quest quest)
		{
			/// Quest
			/// {
			///		string ObjectId
			///		int ClassId
			///		string Name
			///		string Description
			///		string Location
			///		int Level
			///		string Status
			///		bool Done
			///		bool Cancelable
			///		bool Tracked
			///		
			///		Objectives[]
			///		{
			///			string Text
			///			bool Unlocked
			///			bool Done
			///			int Count
			///			int TargetCount
			///		}
			///		
			///		Rewards[]
			///		{
			///			string Text
			///			string Icon
			///		}
			/// }

			var objectivesTable = this.ObjectivesToTable(quest);
			var trackingPointsTable = this.TrackingPointsToTable(quest);

			var rewardsTable = new LuaTable();
			foreach (var reward in quest.Data.Rewards)
			{
				var rewardTable = new LuaTable();
				rewardTable.Insert("Text", reward.ToString());
				rewardTable.Insert("Icon", reward.Icon);

				rewardsTable.Insert(rewardTable);
			}

			var questTable = new LuaTable();

			// Convert map class name(s) to display name(s)
			string locationName = null;
			if (!string.IsNullOrEmpty(quest.Data.Location))
			{
				var mapClassNames = quest.Data.Location.Split(',');
				var mapNames = new List<string>();

				foreach (var mapClassName in mapClassNames)
				{
					var trimmedClassName = mapClassName.Trim();
					if (ZoneServer.Instance.World.TryGetMap(trimmedClassName, out var map))
						mapNames.Add(map.Data.Name);
					else
						mapNames.Add(trimmedClassName);
				}

				locationName = string.Join(", ", mapNames);
			}

			// Convert quest giver map class name to display name
			string questGiverLocationName = null;
			if (!string.IsNullOrEmpty(quest.Data.QuestGiverLocation))
			{
				if (ZoneServer.Instance.World.TryGetMap(quest.Data.QuestGiverLocation, out var map))
					questGiverLocationName = map.Data.Name;
				else
					questGiverLocationName = quest.Data.QuestGiverLocation;
			}

			questTable.Insert("ObjectId", "0x" + quest.ObjectId.ToString("X16"));
			questTable.Insert("ClassId", "0x" + quest.Data.Id.Value.ToString("X16"));
			questTable.Insert("ClassName", quest.QuestStaticData?.ClassName ?? quest.Data.Name);
			questTable.Insert("Name", quest.Data.Name);
			questTable.Insert("Description", quest.Data.Description);
			questTable.Insert("Location", locationName);
			questTable.Insert("Level", quest.Data.Level);
			questTable.Insert("Type", quest.Data.Type.ToString());
			questTable.Insert("Status", quest.Status.ToString());
			questTable.Insert("Done", quest.ObjectivesCompleted);
			questTable.Insert("Cancelable", quest.Data.Cancelable);
			questTable.Insert("Tracked", quest.Tracked);
			questTable.Insert("Objectives", objectivesTable);
			questTable.Insert("TrackingPoints", trackingPointsTable);
			questTable.Insert("Rewards", rewardsTable);

			// Add quest giver information if available
			if (!string.IsNullOrEmpty(quest.Data.StartNpcUniqueName))
				questTable.Insert("QuestGiver", quest.Data.StartNpcUniqueName);

			// Add quest giver location if available
			if (!string.IsNullOrEmpty(questGiverLocationName))
				questTable.Insert("QuestGiverLocation", questGiverLocationName);

			return questTable;
		}

		private LuaTable TrackingPointsToTable(Quest quest)
		{
			var result = new LuaTable();
			if (quest?.QuestStaticData == null)
				return result;

			var questData = quest.SessionObjectStaticData?.QuestData ?? new SessionQuestData();
			var groups = this.GetQuestMapPointGroups(quest, questData);
			var views = this.GetQuestMapPointViews(groups, questData);

			for (var i = 0; i < groups.Count; i++)
			{
				if (i < views.Count && views[i] == 0)
					continue;

				var point = new LuaTable();
				point.Insert("Group", groups[i]);
				result.Insert(point);
			}

			return result;
		}

		/// <summary>
		/// Returns information about the quests objectives and their
		/// progress as a Lua table.
		/// </summary>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		private LuaTable ObjectivesToTable(Quest quest)
		{
			var objectivesTable = new LuaTable();
			foreach (var objective in quest.Data.Objectives)
			{
				if (!quest.TryGetProgress(objective.Ident, out var progress))
					throw new InvalidOperationException($"Missing progress for objective '{objective.Ident}'.");

				var objectiveTable = new LuaTable();
				objectiveTable.Insert("Text", objective.Text);
				objectiveTable.Insert("Unlocked", progress.Unlocked);
				objectiveTable.Insert("Done", progress.Done);
				objectiveTable.Insert("Count", progress.Count);
				objectiveTable.Insert("TargetCount", objective.TargetCount);
				objectiveTable.Insert("Unlimited", objective is UnlimitedKillObjective);

				// Add monster names for collection objectives with drop modifiers
				if (objective is CollectItemObjective collectObjective)
				{
					var monsterNames = new List<string>();
					foreach (var modifier in quest.Data.Modifiers)
					{
						if (modifier is ItemDropModifier dropModifier && dropModifier.ItemId == collectObjective.ItemId)
						{
							foreach (var monsterId in dropModifier.MonsterIds)
							{
								if (ZoneServer.Instance.Data.MonsterDb.TryFind(monsterId, out var monsterData))
									monsterNames.Add(monsterData.Name);
							}
						}
					}

					if (monsterNames.Count > 0)
					{
						var monstersTable = new LuaTable();
						foreach (var monsterName in monsterNames)
							monstersTable.Insert(monsterName);
						objectiveTable.Insert("Monsters", monstersTable);
					}
				}

				objectivesTable.Insert(objectiveTable);
			}

			return objectivesTable;
		}

		/// <summary>
		/// Checks if the quest is completable
		/// </summary>
		/// <param name="questId"></param>
		/// <returns></returns>
		[Obsolete("Use IsCompletable(QuestId questId)")]
		public bool IsCompletable(long questId)
		{
			lock (_syncLock)
			{
				for (var i = 0; i < _quests.Count; i++)
				{
					var quest = _quests[i];

					if (!quest.InProgress || quest.Data.Id.Value != questId)
						continue;

					return quest.ObjectivesCompleted;
				}
			}

			return false;
		}

		/// <summary>
		/// Checks if the quest is completable
		/// </summary>
		/// <param name="questId"></param>
		/// <returns></returns>
		public bool IsCompletable(QuestId questId)
		{
			lock (_syncLock)
			{
				for (var i = 0; i < _quests.Count; i++)
				{
					var quest = _quests[i];

					if (!quest.InProgress || quest.Data.Id != questId)
						continue;

					return quest.ObjectivesCompleted && quest.Status != QuestStatus.Completed;
				}
			}

			return false;
		}

		public QuestStatus GetStatus(int questId)
		{
			lock (_syncLock)
			{
				for (var i = 0; i < _quests.Count; i++)
				{
					var quest = _quests[i];

					if ((int)quest.Data.Id.Value != questId)
						continue;

					return quest.Status;
				}
			}
			return QuestStatus.Possible;
		}

		/// <summary>
		/// Update quest progress
		/// </summary>
		/// <param name="questId"></param>
		/// <param name="objectiveId"></param>
		public void UpdateQuestProgress(long questId, int objectiveId)
		{
			if (this.TryGetById(questId, out var quest))
			{
				var character = this.Character;
				var progress = quest.Progresses[objectiveId];
				if (quest.QuestStaticData != null)
				{
					var mainSessionObject = character.SessionObjects.Get(SessionObjectId.Main);
					// In case quest doesn't exist, set it's state to started (1)
					if (!mainSessionObject.Properties.Has(quest.QuestStaticData.QuestProperty))
					{
						mainSessionObject.Properties.SetFloat(quest.QuestStaticData.QuestProperty, 1);
						Send.ZC_OBJECT_PROPERTY(character, mainSessionObject, quest.QuestStaticData.QuestProperty);
					}

					var questSessionObject = quest.SessionObjectStaticData != null
						? character.SessionObjects.GetOrCreate(quest.SessionObjectStaticData.Id)
						: null;
					if (questSessionObject != null)
					{
						string propertyName;
						if (quest.Progresses[objectiveId].Objective is KillObjective)
							propertyName = $"KillMonster{objectiveId + 1}";
						else
							propertyName = $"QuestInfoValue{objectiveId + 1}";

						questSessionObject.Properties.SetFloat(propertyName, quest.ProgressValue(objectiveId));
						Send.ZC_OBJECT_PROPERTY(character, questSessionObject, propertyName);
						if (progress.Done)
						{
							var goalPropertyName = $"Goal{objectiveId + 1}";
							questSessionObject.Properties.SetFloat(goalPropertyName, 1);
							Send.ZC_OBJECT_PROPERTY(character, questSessionObject, goalPropertyName);
						}
					}
				}
				if (QuestScript.TryGet(quest.Data.Id, out var questScript))
					questScript.OnProgress(this.Character, quest, progress.Objective.Id, quest.ProgressValue(objectiveId));
				if (quest.IsCompletable)
				{
					quest.Status = QuestStatus.Success;
					questScript?.OnSuccess(this.Character, quest);
					this.StopStaticQuestLayerIfDone(quest);
					if (this.TryAutoCompleteStaticQuestOnSuccess(quest))
						return;
				}
			}
		}

		/// <summary>
		/// Update quest progress
		/// </summary>
		/// <param name="questId"></param>
		/// <param name="objectiveId"></param>
		public void UpdateQuestProgress(QuestId questId, int objectiveId)
		{
			if (this.TryGetById(questId, out var quest))
			{
				var character = this.Character;
				var progress = quest.Progresses[objectiveId];
				if (quest.QuestStaticData != null)
				{
					var mainSessionObject = character.SessionObjects.Get(SessionObjectId.Main);
					// In case quest doesn't exist, set it's state to started (1)
					if (!mainSessionObject.Properties.Has(quest.QuestStaticData.QuestProperty))
					{
						mainSessionObject.Properties.SetFloat(quest.QuestStaticData.QuestProperty, 1);
						Send.ZC_OBJECT_PROPERTY(character, mainSessionObject, quest.QuestStaticData.QuestProperty);
					}

					var questSessionObject = quest.SessionObjectStaticData != null
						? character.SessionObjects.GetOrCreate(quest.SessionObjectStaticData.Id)
						: null;
					if (questSessionObject != null)
					{
						string propertyName;
						if (quest.Progresses[objectiveId].Objective is KillObjective)
							propertyName = $"KillMonster{objectiveId + 1}";
						else
							propertyName = $"QuestInfoValue{objectiveId + 1}";

						questSessionObject.Properties.SetFloat(propertyName, quest.ProgressValue(objectiveId));
						Send.ZC_OBJECT_PROPERTY(character, questSessionObject, propertyName);
						if (progress.Done)
						{
							var goalPropertyName = $"Goal{objectiveId + 1}";
							questSessionObject.Properties.SetFloat(goalPropertyName, 1);
							Send.ZC_OBJECT_PROPERTY(character, questSessionObject, goalPropertyName);
						}
					}
				}
				if (QuestScript.TryGet(quest.Data.Id, out var questScript))
					questScript.OnProgress(this.Character, quest, progress.Objective.Id, quest.ProgressValue(objectiveId));
				if (quest.IsCompletable)
				{
					quest.Status = QuestStatus.Success;
					questScript?.OnSuccess(this.Character, quest);
					this.StopStaticQuestLayerIfDone(quest);
					if (this.TryAutoCompleteStaticQuestOnSuccess(quest))
						return;
				}
			}
		}

		private bool TryAutoCompleteStaticQuestOnSuccess(Quest quest)
		{
			if (quest?.QuestStaticData == null)
				return false;

			if (!string.Equals(quest.QuestStaticData.QuestEndMode, "SYSTEM", StringComparison.OrdinalIgnoreCase) &&
				!this.StaticQuestIsClientHiddenPapayaBridge(quest.QuestStaticData))
				return false;

			Log.Info("Static quest chain: auto-completing quest '{0}' for '{1}' after objective success.", quest.QuestStaticData.ClassName, this.Character.Name);
			this.Complete(quest);
			return true;
		}

		public IList<Quest> GetCompletedQuests()
		{
			lock (_quests)
				return _quests.Where(a => a.Status == QuestStatus.Completed).ToList();
		}

		/// <summary>
		/// Internal method to check for VisitLocationObjective completion.
		/// Called by Update, assumes _syncLock is already held if needed for quest list access.
		/// </summary>
		private void CheckVisitLocationObjectivesInternal()
		{
			if (this.Character.Map == null || this.Character.Map == Maps.Map.Limbo || _quests.Count == 0)
				return;

			// Iterate over a copy if modifications can happen, though SetDone/UpdateUnlock should be safe within the loop
			// if QuestComponent's other methods are also correctly locked.
			// For safety and clarity, let's iterate a copy.
			var questsInProgress = _quests.Where(q => q.InProgress).ToList();

			foreach (var quest in questsInProgress)
			{
				var questModifiedInThisIteration = false;
				foreach (var progress in quest.Progresses)
				{
					if (progress.Objective is VisitLocationObjective visitObjective
						&& progress.Unlocked
						&& !progress.Done)
					{
						if (this.Character.Map.Id != visitObjective.TargetMapId) continue;
						if (visitObjective.IsPositionWithinObjective(this.Character.Position))
						{
							Log.Info($"Character {this.Character.Name} completed VisitLocationObjective '{visitObjective.Ident}' " +
									 $"for Quest {quest.Data.Id.Value} by reaching {visitObjective.TargetPosition} (Radius: {visitObjective.TargetRadius}).");

							progress.SetDone();
							quest.UpdateUnlock(); // Potentially unlocks next objective
							questModifiedInThisIteration = true; // Mark that quest state changed

							// --- Handle OnProgress/OnSuccess Callbacks ---
							// This logic is similar to what's in UpdateQuestProgress in QuestComponent
							// We should ideally call a unified method for this.
							// For now, replicate parts of it.

							// Try to get the runtime script first (for procedural quests)
							// This relies on the runtime script being registered in QuestScript.Scripts
							if (QuestScript.TryGet(quest.Data.Id, out var callbackScript))
							{
								// For VisitLocationObjective, what are key/progress?
								// Let's use objective.Id and progress.Count (which would be 1 for visit).
								callbackScript.OnProgress(this.Character, quest, progress.Objective.Id, progress.Count);
							}
							else if (quest.Data.Id.NamespaceId != 0) // It's a procedural ID but script not found
							{
								Log.Warning($"No QuestScript found for procedural quest {quest.Data.Id.Value} during VisitLocationObjective completion.");
							}


							if (quest.IsCompletable && quest.Status < QuestStatus.Success)
							{
								quest.Status = QuestStatus.Success;
								Log.Debug($"Quest {quest.Data.Id.Value} now in Success state after visit.");
								callbackScript?.OnSuccess(this.Character, quest);
							}

							// Optimization: if this quest is now fully done (all objectives), no need to check its other objectives in this pass.
							// Note: This doesn't complete the quest; HandleProceduralTurnIn or another mechanism does that.
							if (quest.ObjectivesCompleted) break; // Break from inner (progress) loop
						}
					}
				}

				if (questModifiedInThisIteration)
				{
					this.UpdateClient_UpdateQuest(quest); // Send update to client if any objective in this quest changed
				}
			}
		}

		/// <summary>
		/// Internal method to check for VariableCheckObjective completion.
		/// Called by Update, assumes _syncLock is already held.
		/// </summary>
		private void CheckVariableCheckObjectivesInternal()
		{
			if (_quests.Count == 0)
				return;

			// Iterate over a copy to avoid modification issues
			var questsInProgress = _quests.Where(q => q.InProgress).ToList();

			foreach (var quest in questsInProgress)
			{
				var questModifiedInThisIteration = false;
				foreach (var progress in quest.Progresses)
				{
					if (progress.Objective is VariableCheckObjective variableObjective
						&& progress.Unlocked
						&& !progress.Done)
					{
						var currentValue = variableObjective.GetVariableValue(this.Character);
						if (currentValue != progress.Count)
						{
							progress.Count = Math.Min(variableObjective.TargetCount, currentValue);

							if (progress.Count >= variableObjective.TargetCount)
							{
								progress.SetDone();
								quest.UpdateUnlock(); // Potentially unlocks next objective
								questModifiedInThisIteration = true; // Mark that quest state changed

								// Handle OnProgress/OnSuccess Callbacks
								if (QuestScript.TryGet(quest.Data.Id, out var callbackScript))
								{
									callbackScript.OnProgress(this.Character, quest, progress.Objective.Id, progress.Count);
								}
								else if (quest.Data.Id.NamespaceId != 0)
								{
									Log.Warning($"No QuestScript found for procedural quest {quest.Data.Id.Value} during VariableCheckObjective completion.");
								}

								if (quest.IsCompletable && quest.Status < QuestStatus.Success)
								{
									quest.Status = QuestStatus.Success;
									Log.Debug($"Quest {quest.Data.Id.Value} now in Success state after variable check.");
									callbackScript?.OnSuccess(this.Character, quest);
								}

								// If this quest is now fully done, no need to check its other objectives in this pass
								if (quest.ObjectivesCompleted) break;
							}
							else
							{
								// Value changed but not complete yet - still need to update the client
								questModifiedInThisIteration = true;
							}
						}
					}
				}

				if (questModifiedInThisIteration)
				{
					this.UpdateClient_UpdateQuest(quest); // Send update to client if any objective in this quest changed
				}
			}
		}
	}
}
