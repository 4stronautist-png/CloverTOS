using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.ObjectProperties;
using Melia.Shared.Scripting;
using Melia.Shared.Game.Const;
using Melia.Shared.Game.Properties;
using Melia.Zone.Events.Arguments;
using Melia.Zone.Network;
using Melia.Zone.Scripting;
using Melia.Zone.World.Actors.Monsters;
using Melia.Zone.World.Quests;
using Melia.Zone.World.Quests.Modifiers;
using Melia.Zone.World.Quests.Objectives;
using Melia.Zone.World.Quests.Rewards;
using Yggdrasil.Scheduling;
using Yggdrasil.Util;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Yggdrasil.Logging;
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
		private readonly static object StaticObjectiveLoadLock = new();
		private readonly static HashSet<Type> LoadedStaticObjectiveTypes = new();
		private readonly static object StaticModifierLoadLock = new();
		private readonly static HashSet<Type> LoadedStaticModifierTypes = new();

		private readonly object _syncLock = new();
		private readonly List<Quest> _quests = new();
		private readonly List<long> _disabledQuests = new();

		private TimeSpan _autoReceiveDelay = AutoReceiveDelay;
		private TimeSpan _timeSinceLastLocationCheck = TimeSpan.Zero;

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
				foreach (var quest in _quests)
				{
					if (quest.Status != QuestStatus.InProgress && quest.Status != QuestStatus.Success)
						continue;

					quest.UpdateObjectives(updater);

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
				for (var i = 0; i < _quests.Count; i++)
				{
					var quest = _quests[i];
					if (quest.Status != QuestStatus.InProgress)
						continue;

					quest.UpdateModifiers(updater);

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

			if (!QuestScript.Exists(new QuestId("Laima.Quest", questData.Id)) && !QuestScript.Exists(new QuestId(questData.Id)))
				return this.StartStaticQuest(questData, TimeSpan.Zero);

			return this.Start(new QuestId("Laima.Quest", questData.Id), TimeSpan.Zero);
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
			}
			else
			{
				quest.StartTime = DateTime.Now.Add(delay);
				this.AddSilent(quest);
			}

			return Task.Yield();
		}

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
			if (questStaticData.Level > 0 && this.Character.Level < questStaticData.Level)
				return false;

			if (!this.MeetsStaticCheckScripts(questStaticData))
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

			var anythingChanged = false;

			for (var pass = 0; pass < 20; pass++)
			{
				var changedThisPass = false;
				var quests = this.GetList();

				foreach (var quest in quests)
				{
					if (quest.QuestStaticData == null)
						continue;

					if (QuestScript.Exists(new QuestId("Laima.Quest", quest.Data.Id.Value)) || QuestScript.Exists(quest.Data.Id))
						continue;

					if (quest.InProgress &&
						quest.ObjectivesCompleted &&
						this.StaticQuestShouldCompleteFromNpcDialog(quest.QuestStaticData, npcDialogName))
					{
						this.Complete(quest);
						anythingChanged = true;
						changedThisPass = true;
						break;
					}

					if (quest.InProgress && this.TryAdvanceStaticQuestFromNpcDialog(quest, npcDialogName))
					{
						anythingChanged = true;
						changedThisPass = true;
						break;
					}

					if (quest.Status == QuestStatus.Success && this.TryTurnInStaticQuestFromNpcDialog(quest, npcDialogName))
					{
						anythingChanged = true;
						changedThisPass = true;
						break;
					}
				}

				if (changedThisPass)
					continue;

				var startableQuest = ZoneServer.Instance.Data.QuestDb.GetList()
					.OrderBy(a => a.Id)
					.FirstOrDefault(a =>
						string.Equals(a.QuestStartMode, "NPCDIALOG", StringComparison.OrdinalIgnoreCase) &&
						this.StaticQuestCanStartFromNpcDialog(a, npcDialogName) &&
						!this.StaticQuestIsBlockedByPriorityQuest(a, npcDialogName) &&
						!this.Has(new QuestId(a.Id)) &&
						this.MeetsStaticPrerequisites(a));

				if (startableQuest == null)
					break;

				this.StartStaticQuest(startableQuest, TimeSpan.Zero);
				anythingChanged = true;
				changedThisPass = true;
			}

			if (anythingChanged)
				this.SyncStaticQuestNpcStates();

			return anythingChanged;
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
			this.EnsureStaticQuestLayerState();
			this.SyncStaticQuestSessionObjects();
			this.EnsureStaticQuestNpcActors(mapClassName);
			this.EnsureStaticQuestObjectiveMonsters(mapClassName);

			var npcs = this.Character.Map.GetNpcs(a => a is Npc npc && !string.IsNullOrWhiteSpace(npc.DialogName));

			foreach (var minMon in npcs)
			{
				if (minMon is not Npc npc)
					continue;

				if (this.IsTechnicalStaticQuestNpc(npc.DialogName))
				{
					if (this.Character.GetMapNPCState(npc) != NpcState.Invisible)
						this.Character.SetMapNPCState(npc, NpcState.Invisible);
					continue;
				}

				if (!this.StaticNpcIsRelevantForCurrentQuestState(npc.DialogName, mapClassName))
					continue;

				var currentState = this.Character.GetMapNPCState(npc);
				if (currentState == NpcState.Invisible || npc.State == NpcState.Invisible)
					this.Character.SetMapNPCState(npc, NpcState.Normal);
			}
		}

		private bool StaticNpcIsRelevantForCurrentQuestState(string npcDialogName, string mapClassName)
		{
			foreach (var questData in ZoneServer.Instance.Data.QuestDb.GetList())
			{
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
					this.MeetsStaticPrerequisites(questData))
					return true;
			}

			return false;
		}

		private void EnsureStaticQuestObjectiveMonsters(string mapClassName)
		{
			foreach (var request in this.GetRelevantStaticMonsterSpawnRequests(mapClassName))
			{
				var existingCount = this.Character.Map
					.GetMonsters(monster => monster.Id == request.MonsterId && monster.Hp > 0 && monster.Layer == this.Character.Layer)
					.Count;

				if (existingCount >= request.Count)
					continue;

				var spawnCount = Math.Min(request.Count - existingCount, 3);
				for (var i = 0; i < spawnCount; i++)
				{
					var offset = i * 35;
					var monster = Shortcuts.AddMonster(0, request.MonsterId, request.Name, mapClassName, request.X + offset, request.Y, request.Z + offset, 0, "Monster");
					monster.Layer = this.Character.Layer;
					if (this.Character.Connection != null)
						Send.ZC_ENTER_MONSTER(this.Character.Connection, monster);
				}

				Log.Info("Static quest chain: spawned {0} fallback objective monster(s) '{1}' for quest '{2}' on map '{3}' at {4:0.##}/{5:0.##}/{6:0.##}.", spawnCount, request.ClassName, request.QuestClassName, mapClassName, request.X, request.Y, request.Z);
			}
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

			if (this.Character.Layer == 0)
			{
				var layer = this.Character.StartLayer();
				Log.Info("Static quest layer: moved '{0}' to layer {1} for quest '{2}'.", this.Character.Name, layer, quest.QuestStaticData.ClassName);
			}
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

			return false;
		}

		private void StopStaticQuestLayerIfDone(Quest quest)
		{
			if (quest?.QuestStaticData?.Objectives == null || this.Character.Layer == 0)
				return;

			if (!quest.QuestStaticData.Objectives.Any(objective => objective.Layer))
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

					if (!this.TryResolveStaticObjectivePosition(questData, mapClassName, out var x, out var y, out var z, out _))
					{
						x = this.Character.Position.X;
						y = this.Character.Position.Y;
						z = this.Character.Position.Z;
					}

					foreach (var monsterData in this.ResolveStaticObjectiveMonsterTargets(target))
					{
						var missingCount = Math.Max(1, Math.Min(objectiveData.Count - progress.Count, 3));
						yield return new StaticQuestMonsterSpawnRequest(monsterData.Id, monsterData.ClassName, monsterData.Name, questData.ClassName, x, y, z, missingCount);
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

		private IEnumerable<Melia.Shared.Data.Database.MonsterData> ResolveStaticObjectiveMonsterTargets(string target)
		{
			foreach (var className in target.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			{
				if (string.Equals(className, "ALL", StringComparison.OrdinalIgnoreCase))
					continue;

				if (ZoneServer.Instance.Data.MonsterDb.TryFind(className, out var monsterData))
					yield return monsterData;
			}
		}

		private void EnsureStaticQuestNpcActors(string mapClassName)
		{
			var existingDialogNames = this.Character.Map
				.GetNpcs(a => a is Npc npc && !string.IsNullOrWhiteSpace(npc.DialogName))
				.OfType<Npc>()
				.Select(npc => npc.DialogName)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			foreach (var request in this.GetRelevantStaticNpcSpawnRequests(mapClassName))
			{
				if (existingDialogNames.Contains(request.DialogName))
					continue;

				var modelId = this.ResolveStaticQuestNpcMonsterId(request.DialogName);
				var npc = Shortcuts.AddNpc(0, modelId, request.Name, mapClassName, request.X, request.Y, request.Z, 0, request.DialogName, state: (int)NpcState.Invisible, range: request.Range);
				this.Character.SetMapNPCState(npc, NpcState.Normal);
				existingDialogNames.Add(request.DialogName);

				Log.Info("Static quest chain: spawned fallback NPC '{0}' for quest '{1}' on map '{2}' at {3:0.##}/{4:0.##}/{5:0.##}.", request.DialogName, request.QuestClassName, mapClassName, request.X, request.Y, request.Z);
			}
		}

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
					!this.Has(questId) &&
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

			if (this.IsTechnicalStaticQuestNpc(dialogName))
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

			request = new StaticQuestNpcSpawnRequest(dialogName, questData.ClassName, dialogName, x, y, z, range);
			return true;
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
				this.StaticQuestLocationReferencesMap(roleLocation, mapClassName);
		}

		private bool TryResolveStaticNpcPosition(QuestStaticData questData, string dialogName, string mapClassName, out double x, out double y, out double z, out double range)
		{
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
			}

			return false;
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

			if (dialogName.Contains("BOX", StringComparison.OrdinalIgnoreCase) ||
				dialogName.Contains("CHEST", StringComparison.OrdinalIgnoreCase))
				return 147392;

			if (dialogName.Contains("BOOK", StringComparison.OrdinalIgnoreCase) ||
				dialogName.Contains("MAIL", StringComparison.OrdinalIgnoreCase))
				return 155005;

			if (dialogName.Contains("STONE", StringComparison.OrdinalIgnoreCase) ||
				dialogName.Contains("ROCK", StringComparison.OrdinalIgnoreCase) ||
				dialogName.Contains("CRYSTAL", StringComparison.OrdinalIgnoreCase))
				return 12080;

			if (dialogName.Contains("TRIGGER", StringComparison.OrdinalIgnoreCase) ||
				dialogName.Contains("HIDDEN", StringComparison.OrdinalIgnoreCase))
				return 20041;

			return 20117;
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
		private readonly record struct StaticQuestMonsterSpawnRequest(int MonsterId, string ClassName, string Name, string QuestClassName, double X, double Y, double Z, int Count);

		private bool StaticQuestReferencesMap(QuestStaticData questData, string mapClassName)
		{
			return this.StaticQuestMapMatches(questData.StartMap, mapClassName) ||
				this.StaticQuestMapMatches(questData.ProgMap, mapClassName) ||
				this.StaticQuestMapMatches(questData.EndMap, mapClassName) ||
				this.StaticQuestLocationReferencesMap(questData.StartLocation, mapClassName) ||
				this.StaticQuestLocationReferencesMap(questData.ProgLocation, mapClassName) ||
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

		private bool StaticQuestCanStartFromNpcDialog(QuestStaticData questData, string npcDialogName)
		{
			if (this.StaticNpcDialogMatches(questData.StartNPC, npcDialogName))
				return true;

			return questData.Id == 1014 &&
				this.StaticNpcDialogMatches("SIALUL_WEST_DRASIUS", npcDialogName);
		}

		private bool StaticQuestIsBlockedByPriorityQuest(QuestStaticData questData, string npcDialogName)
		{
			if (questData == null)
				return false;

			if (!string.Equals(questData.QuestMode, "SUB", StringComparison.OrdinalIgnoreCase) &&
				!string.Equals(questData.QuestMode, "REPEAT", StringComparison.OrdinalIgnoreCase))
				return false;

			if (questData.Id == 1023 && !this.HasCompleted(1014))
				return true;

			return ZoneServer.Instance.Data.QuestDb.GetList().Any(candidate =>
				candidate.Id != questData.Id &&
				string.Equals(candidate.QuestMode, "MAIN", StringComparison.OrdinalIgnoreCase) &&
				string.Equals(candidate.QuestStartMode, "NPCDIALOG", StringComparison.OrdinalIgnoreCase) &&
				this.StaticQuestCanStartFromNpcDialog(candidate, npcDialogName) &&
				!this.Has(new QuestId(candidate.Id)) &&
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

		private bool TryTurnInStaticQuestFromNpcDialog(Quest quest, string npcDialogName)
		{
			if (!this.StaticQuestShouldCompleteFromNpcDialog(quest.QuestStaticData, npcDialogName))
				return false;

			this.Complete(quest);
			return true;
		}

		private bool StaticQuestCanAdvanceFromNpcDialog(QuestStaticData questData, string npcDialogName)
		{
			if (this.StaticNpcDialogMatches(questData.ProgNPC, npcDialogName))
				return true;

			if (this.StaticNpcDialogMatches(questData.EndNPC, npcDialogName))
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
				if (QuestScript.TryGet(new QuestId("Laima.Quest", questId), out var questScript))
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
				foreach (var quest in _quests)
				{
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
				foreach (var quest in _quests)
				{
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

			this.UpdateClient_RemoveQuest(quest);
			this.UpdateClient_CompleteQuest(quest);
			this.StartStaticSystemFollowUpQuests(quest);
			this.SyncStaticQuestNpcStates();
		}

		private void StartStaticSystemFollowUpQuests(Quest quest)
		{
			var completedQuestData = quest?.QuestStaticData;
			if (completedQuestData == null || string.IsNullOrWhiteSpace(completedQuestData.ClassName))
				return;

			var nextQuests = ZoneServer.Instance.Data.QuestDb.GetList()
				.Where(candidate =>
					string.Equals(candidate.QuestStartMode, "SYSTEM", StringComparison.OrdinalIgnoreCase) &&
					candidate.RequiredQuests != null &&
					candidate.RequiredQuests.Any(requiredQuest => string.Equals(requiredQuest, completedQuestData.ClassName, StringComparison.OrdinalIgnoreCase)) &&
					!this.Has(new QuestId(candidate.Id)) &&
					this.MeetsStaticPrerequisites(candidate))
				.OrderBy(candidate => candidate.Id)
				.ToList();

			foreach (var nextQuest in nextQuests)
				this.StartStaticQuest(nextQuest, TimeSpan.Zero);
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

			this.UpdateClient_RemoveQuest(quest);
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
				quest = Quest.Create(new QuestId("Laima.Quest", questId));
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

		/// <summary>
		/// Sends a list of all quests to the client to update it.
		/// </summary>
		public void UpdateClient()
		{
			var quests = this.GetList();
			foreach (var quest in quests.Where(a => a.InProgress))
			{
				// Re-check quest objectives to sync with current state (e.g., collection items in inventory)
				this.InitialChecks(quest);
				this.SyncStaticQuestSessionObject(quest);

				var questTable = this.QuestToTable(quest);

				var lua = "Melia.Quests.Restore(" + questTable.Serialize() + ")";
				Send.ZC_EXEC_CLIENT_SCP(this.Character.Connection, lua);
			}
		}

		/// <summary>
		/// Adds the quest to the client's quest log.
		/// </summary>
		/// <param name="quest"></param>
		private void UpdateClient_AddQuest(Quest quest)
		{
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
			this.SyncStaticQuestNpcStates();

			//Log.Debug(lua);
		}

		/// <summary>
		/// Updates the quest objectives on the client.
		/// </summary>
		/// <param name="quest"></param>
		public void UpdateClient_UpdateQuest(Quest quest)
		{
			var objectivesTable = this.ObjectivesToTable(quest);
			var questDataFound = ZoneServer.Instance.Data.QuestDb.TryFind((int)quest.Data.Id.Value, out var questData);

			var questTable = new LuaTable();
			questTable.Insert("ObjectId", "0x" + quest.ObjectId.ToString("X16"));
			questTable.Insert("Status", quest.Status.ToString());
			questTable.Insert("Done", quest.ObjectivesCompleted);
			questTable.Insert("Objectives", objectivesTable);

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
			this.SyncStaticQuestNpcStates();

			//Log.Debug(lua);
		}

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

			var monsterNameGroups = this.GetQuestMonsterNameGroups(quest, questData);
			var monsterViews = this.GetQuestMonsterViews(monsterNameGroups, questData);
			this.SetQuestSessionStringList(sessionObject, "QuestMonNameGroup", monsterNameGroups, 10, changedProperties);
			this.SetQuestSessionNumberList(sessionObject, "QuestMonView", monsterViews, 10, changedProperties);
			this.SetQuestSessionStringList(sessionObject, "QuestMonViewTerms", questData.MonsterViewTerms, 10, changedProperties);

			return changedProperties;
		}

		private void SyncStaticQuestSessionObjects()
		{
			foreach (var quest in this.GetList().Where(quest => quest.InProgress || quest.Status == QuestStatus.Success))
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

		private List<string> GetQuestInfoNames(Quest quest, SessionQuestData questData)
		{
			var result = questData.InfoName != null
				? questData.InfoName.Where(name => !this.IsNone(name)).ToList()
				: new List<string>();

			if (result.Count != 0)
				return result;

			var questStaticData = quest.QuestStaticData;
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
			var result = questData.MapPointGroup != null
				? questData.MapPointGroup.Where(group => !this.IsNone(group)).ToList()
				: new List<string>();

			if (result.Count != 0)
				return result;

			var questStaticData = quest.QuestStaticData;
			if (questStaticData == null)
				return result;

			if (quest.Status == QuestStatus.Success)
			{
				this.AddQuestMapPointGroup(result, questStaticData.EndLocation, questStaticData.EndMap, questStaticData.EndNPC);
				return result;
			}

			if (quest.InProgress)
				this.AddQuestMapPointGroup(result, questStaticData.ProgLocation, questStaticData.ProgMap, questStaticData.ProgNPC);

			return result;
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

		private List<string> GetQuestMonsterNameGroups(Quest quest, SessionQuestData questData)
		{
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
			if (ZoneServer.Instance.Data.QuestDb.TryFind((int)quest.Data.Id.Value, out var questData) && quest.SessionObjectStaticData != null)
			{
				this.Character.SessionObjects.Remove(quest.SessionObjectStaticData.Id);
				Send.ZC_SESSION_OBJ_REMOVE(this.Character, quest.SessionObjectStaticData.Id);
			}

			var lua = $"Melia.Quests.Remove('{quest.ObjectIdStr}')";
			Send.ZC_EXEC_CLIENT_SCP(this.Character.Connection, lua);
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
			questTable.Insert("Rewards", rewardsTable);

			// Add quest giver information if available
			if (!string.IsNullOrEmpty(quest.Data.StartNpcUniqueName))
				questTable.Insert("QuestGiver", quest.Data.StartNpcUniqueName);

			// Add quest giver location if available
			if (!string.IsNullOrEmpty(questGiverLocationName))
				questTable.Insert("QuestGiverLocation", questGiverLocationName);

			return questTable;
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
				}
			}
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
