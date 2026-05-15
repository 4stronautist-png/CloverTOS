using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using Melia.Shared;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.IES;
using Melia.Shared.L10N;
using Melia.Shared.Network;
using Melia.Shared.Network.Inter.Messages;
using Melia.Zone.Abilities;
using Melia.Zone.Buffs;
using Melia.Zone.Commands;
using Melia.Zone.Data;
using Melia.Zone.Database;
using Melia.Zone.Events;
using Melia.Zone.Network;
using Melia.Zone.Pads.Handlers;
using Melia.Zone.Scripting;
using Melia.Zone.Scripting.Shared;
using Melia.Zone.Services;
using Melia.Zone.Skills.Handlers;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.Spawning;
using Melia.Zone.World.Spawning;
using Melia.Zone.Util;
using Melia.Zone.World;
using Melia.Zone.World.Actors.Characters;
using Yggdrasil.Network.Communication;
using Yggdrasil.Network.TCP;
using Yggdrasil.Util;
using Log = Yggdrasil.Logging.Log;

namespace Melia.Zone
{
	/// <summary>
	/// Represents a zone server.
	/// </summary>
	public class ZoneServer : Server
	{
		public readonly static ZoneServer Instance = new();

		private TcpConnectionAcceptor<ZoneConnection> _acceptor;
		public bool BlockNewConnections { get; set; }
		internal AutoSaveService AutoSave => _autoSaveService;
		private AutoSaveService _autoSaveService;
		private OrphanCleanupService _orphanCleanupService;
		private LogCleanupService _logCleanupService;
		private DeadConnectionSweepService _deadConnectionSweepService;

		public override ServerType Type => ServerType.Zone;

		/// <summary>
		/// Returns a reference to the server's packet handlers.
		/// </summary>
		public PacketHandler PacketHandler { get; } = new();

		/// <summary>
		/// Returns reference to the server's database interface.
		/// </summary>
		public ZoneDb Database { get; } = new();

		/// <summary>
		/// Returns reference to the server's world manager.
		/// </summary>
		public WorldManager World { get; } = new();

		/// <summary>
		/// Returns reference to the server's skill handlers.
		/// </summary>
		public SkillHandlers SkillHandlers { get; } = new();

		/// <summary>
		/// Returns reference to the server's buff handlers.
		/// </summary>
		public BuffHandlers BuffHandlers { get; } = new();

		/// <summary>
		/// Returns reference to the server's ability handlers.
		/// </summary>
		public AbilityHandlers AbilityHandlers { get; } = new();

		/// <summary>
		/// Returns reference to the server's item handlers.
		/// </summary>
		//public ItemHandlers ItemHandlers { get; } = new();

		/// <summary>
		/// Returns reference to the server's pad handlers.
		/// </summary>
		public PadHandlers PadHandlers { get; } = new();

		/// <summary>
		/// Returns reference to the server's chat command manager.
		/// </summary>
		public ChatCommands ChatCommands { get; } = new();

		/// <summary>
		/// Returns a reference to the server's event manager.
		/// </summary>
		public ServerEvents ServerEvents { get; } = new();

		/// <summary>
		/// Returns a reference to the server's game event manager.
		/// </summary>
		public GameEventManager GameEvents { get; } = new GameEventManager();

		/// <summary>
		/// Manager for achievements and point tracking.
		/// </summary>
		public AchievementService Achievements { get; } = new AchievementService();

		/// <summary>
		/// Manager for periodic dungeon reset tasks.
		/// </summary>
		public DungeonResetService DungeonReset { get; } = new DungeonResetService();

		/// <summary>
		/// Returns the dialog function handlers.
		/// </summary>
		public DialogFunctions DialogFunctions { get; } = new DialogFunctions();

		/// <summary>
		/// Returns the trigger function handlers.
		/// </summary>
		public TriggerFunctions TriggerFunctions { get; } = new TriggerFunctions();

		/// <summary>
		/// Returns reference to the server's IES mods.
		/// </summary>
		public IesModList IesMods { get; } = new();

		public Stopwatch ServerTime { get; } = new Stopwatch();


		/// <summary>
		/// Runs the server.
		/// </summary>
		/// <param name="args"></param>
		public override void Run(string[] args)
		{
			this.GetServerId(args, out var groupId, out var serverId);
			var title = string.Format("Zone ({0}, {1})", groupId, serverId);

			ConsoleUtil.WriteHeader(ConsoleHeader.ProjectName, title, ConsoleColor.DarkGreen, ConsoleHeader.Logo, ConsoleHeader.Credits);
			ConsoleUtil.LoadingTitle();

			// Set up zone server specific logging or we might run into
			// issues with multiple servers trying to write files at the
			// same time.
			Log.Init($"ZoneServer_{groupId}_{serverId}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}");

			this.NavigateToRoot();

			this.LoadConf();
			this.LoadPackages();
			this.LoadVersionInfo();
			if (this.Data.OpDb != null)
				this.PacketHandler.LoadMethods();
			this.LoadLocalization(this.Conf);
			this.LoadData(this.Type);
			this.LoadServerList(this.Data.ServerDb, this.Type, groupId, serverId);
			this.InitDatabase(this.Database, this.Conf);
			this.InitSkills();
			this.InitWorld();
			this.LoadDialogFunctions();
			this.LoadTriggerFunctions();
			this.LoadScripts("zone");
			this.LoadIesMods();
			this.PrepareWorld();
			this.StartWorld();

			this.StartAutoSaveService();
			this.StartOrphanCleanupService();
			this.StartLogCleanupService();
			this.StartDeadConnectionSweepService();
			if (this.Conf.World.EnableProceduralQuests)
			{
				NpcSpawnManager.LoadSpawnData();
				ResourceNodeSpawnManager.LoadAllNodeData();
				PoiDataManager.LoadPoiData();
			}

			this.StartCommunicator();
			this.StartAcceptor();

			ConsoleUtil.RunningTitle();
			new ZoneConsoleCommands().Wait();
		}

		/// <summary>
		/// Starts accepting connections.
		/// </summary>
		private void StartAcceptor()
		{
			_acceptor = new TcpConnectionAcceptor<ZoneConnection>(this.ServerInfo.Port);
			_acceptor.ConnectionChecker = (conn) =>
			{
				if (this.BlockNewConnections)
					return new ConnectionCheck(ConnectionCheckResult.Reject, "Server is shutting down");
				return this.CheckConnection(conn, this.Database);
			};
			_acceptor.ConnectionAccepted += this.OnConnectionAccepted;
			_acceptor.ConnectionRejected += this.OnConnectionRejected;
			_acceptor.Listen();

			Log.Status("Server ready, listening on {0}.", _acceptor.Address);
		}

		/// <summary>
		/// Starts the communicator and attempts to connect to the
		/// coordinator.
		/// </summary>
		private void StartCommunicator()
		{
			Log.Info("Attempting to connect to coordinator...");

			var commName = $"{this.ServerInfo.Type}:{this.ServerList.GroupData.Id}:{this.ServerInfo.Id}";

			this.Communicator = new Communicator(commName);
			this.Communicator.Disconnected += this.Communicator_OnDisconnected;
			this.Communicator.MessageReceived += this.Communicator_OnMessageReceived;

			this.ConnectToCoordinator();
		}

		/// <summary>
		/// Attempts to establish a connection to the coordinator.
		/// </summary>
		private void ConnectToCoordinator()
		{
			var barracksServerInfo = this.GetServerInfo(ServerType.Barracks, 1);
			var authentication = this.Conf.Inter.Authentication;

			try
			{
				this.Communicator.Connect("Coordinator", authentication, barracksServerInfo.InterHost, barracksServerInfo.InterPort);

				this.Communicator.Subscribe("Coordinator", "ServerUpdates");
				this.Communicator.Subscribe("Coordinator", "AllServers");
				this.Communicator.Subscribe("Coordinator", "AllZones");

				this.ServerInfo.Status = ServerStatus.Online;
				this.UpdateServerInfo();

				Log.Info("Successfully connected to coordinator.");
			}
			catch (Exception ex)
			{
				Log.Error("Failed to connect to coordinator, trying again in 5 seconds...");
				Log.Error(ex.Message);
				Thread.Sleep(5000);

				this.ConnectToCoordinator();
			}
		}

		/// <summary>
		/// Called when the connection to the coordinator was lost.
		/// </summary>
		/// <param name="commName"></param>
		private void Communicator_OnDisconnected(string commName)
		{
			Log.Error("Lost connection to coordinator, will try to reconnect in 5 seconds...");
			Thread.Sleep(5000);

			this.ConnectToCoordinator();
		}

		/// <summary>
		/// Called when a message was received from the coordinator.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="message"></param>
		private void Communicator_OnMessageReceived(string sender, ICommMessage message)
		{
			//Log.Debug("Message received from '{0}': {1}", sender, message);

			switch (message)
			{
				case ShutdownMessage shutdownMessage:
				{
					// Delegate to the ServerShutdownManager for graceful shutdown handling
					// This supports immediate, graceful (with countdown), and cancel operations
					ServerShutdownManager.Instance.HandleShutdownMessage(shutdownMessage);
					break;
				}
				case ServerUpdateMessage serverUpdateMessage:
				{
					this.ServerList.Update(serverUpdateMessage);
					break;
				}
				case NoticeTextMessage noticeTextMessage:
				{
					Send.ZC_TEXT(noticeTextMessage.Type, noticeTextMessage.Text);
					break;
				}
				case KickMessage kickMessage:
				{
					IEnumerable<Character> characters;

					if (kickMessage.TargetType == KickTargetType.Player)
					{
						var targetCharacter = this.World.GetCharacterByTeamName(kickMessage.TargetName);
						if (targetCharacter == null)
							break;

						characters = [targetCharacter];
					}
					else if (kickMessage.TargetType == KickTargetType.Map)
					{
						if (!this.World.TryGetMap(kickMessage.TargetName, out var map))
							break;

						characters = map.GetCharacters();
					}
					else
					{
						throw new InvalidDataException($"Invalid kick target type '{kickMessage.TargetType}'.");
					}

					foreach (var character in characters)
					{
						character.MsgBox(Localization.Get("You were kicked by {0}."), kickMessage.OriginName);
						character.Connection.Close(this.Conf.World.ConnectionCloseDelay);
					}
					break;
				}
				case ForceLogOutMessage logoutMessage:
				{
					var character = this.World.GetCharacter(c => c.Connection?.Account?.Id == logoutMessage.AccountId && !c.IsAutoTrading);
					character?.Connection?.Close();
					break;
				}
				case TeamNameChangedMessage teamNameChangedMessage:
				{
					var characters = this.World.GetCharacters(c => c.Connection?.Account?.Id == teamNameChangedMessage.AccountId);
					foreach (var character in characters)
					{
						if (character.Connection?.Account != null)
							character.Connection.Account.TeamName = teamNameChangedMessage.NewTeamName;

						character.TeamName = teamNameChangedMessage.NewTeamName;
					}
					break;
				}
				case GuildUpdateMessage guildUpdateMessage:
				{
					Log.Debug("Received guild update from '{0}': Type={1}, GuildId={2}, CharacterId={3}",
						sender, guildUpdateMessage.UpdateType, guildUpdateMessage.GuildId, guildUpdateMessage.CharacterId);
					break;
				}
				case MarketUpdateMessage marketUpdateMessage:
				{
					// Zone Server ignores market updates
					break;
				}
				case ForceLogOutEveryoneMessage logoutMessage:
				{
					var characters = this.World.GetCharacters();
					foreach (var character in characters)
					{
						character.MsgBox(Localization.Get("Logging Out: {0}."), logoutMessage.Reason);
						character.Connection?.Close(this.Conf.World.ConnectionCloseDelay);
					}
					break;
				}
				case InitAutoMatchContentMessage initAutoMatchContentMessage:
				{
					this.World.AutoMatch.HandleInitAutoMatchContent(initAutoMatchContentMessage);
					break;
				}
				case AutoMatchMembersUpdateMessage autoMatchMembersUpdateMessage:
				{
					this.World.AutoMatch.HandleMembersUpdate(autoMatchMembersUpdateMessage);
					break;
				}
			}
		}

		/// <summary>
		/// Sends an update about the server's status to the coordinator.
		/// </summary>
		public void UpdateServerInfo()
		{
			var playerCount = this.World.GetCharacterCount();

			var rates = new ServerRates
			{
				ExpRate = this.Conf.World.ExpRate,
				JobExpRate = this.Conf.World.JobExpRate,
				DropRate = this.Conf.World.GeneralDropRate,
				EquipRate = this.Conf.World.EquipmentDropRate,
				GemRate = this.Conf.World.GemDropRate,
				RecipeRate = this.Conf.World.RecipeDropRate,
			};

			//var message = new ServerUpdateMessage(this.Type, serverId, playerCount, ServerStatus.Online, rates);
			//zoneServer.Communicator.Send("Coordinator", message);
			base.UpdateServerInfo(this.ServerInfo.Status, playerCount, rates);
		}

		/// <summary>
		/// Loads skill handlers.
		/// </summary>
		private void InitSkills()
		{
			Log.Info("Initializing handlers...");
			this.SkillHandlers.Init(this.Packages);
			this.BuffHandlers.Init(this.Packages);
			this.PadHandlers.Init(this.Packages);
			this.AbilityHandlers.Init(this.Packages);
		}

		/// <summary>
		/// Loads maps and initializes them.
		/// </summary>
		private void InitWorld()
		{
			Log.Info("Initializing world...");
			this.World.Initialize();

			Log.Info("Initializing game events...");
			this.GameEvents.Initialize();

			Log.Info("Initializing dungeon reset service...");
			this.DungeonReset.Initialize();

			Log.Info("Initializing achievement service...");
			this.Achievements.Initialize();
			Log.Info("  done loading {0} maps.", this.World.Count);
		}

		/// <summary>
		/// Prepares world before it's started.
		/// </summary>
		private void PrepareWorld()
		{
			Log.Info("Prepairing world...");

			// Removes spawners that have no spawn areas, as they would
			// unnecessarily consume resources. This may happen naturally
			// if the server loads spawners for maps it doesn't serve.
			var spawners = this.World.GetSpawners();
			foreach (var spawner in spawners)
			{
				if (spawner is MonsterSpawner ms && (!this.World.TryGetSpawnAreas(ms.SpawnPointsIdent, out var areas) || areas.Count == 0))
					this.World.RemoveSpawner(spawner);
			}
		}

		/// <summary>
		/// Starts the world's update loop, aka the hearbeat.
		/// </summary>
		private void StartWorld()
		{
			this.ServerTime.Start();
			Log.Info("Starting world update...");
			this.World.Start();
		}

		/// <summary>
		/// Sets up IES mods.
		/// </summary>
		private void LoadIesMods()
		{
			// This method is temporary until we have a more proper way
			// way of handling IES mods.

			// Centurion was apparently disabled during the beta phase
			// in 2015 and replaced with Fencer, and while it was supposed
			// get added back in on a higher rank, that never happened (?).
			// To enable it, we need to adjust the job rank to make it
			// selectable and give the skills a max level.
			if (!Feature.IsEnabled(FeatureId.CenturionRemoved))
			{
				this.IesMods.Add("Job", 1005, "Rank", 2);
				this.IesMods.Add("SkillTree", 10502, "MaxLevel", 5);
				this.IesMods.Add("SkillTree", 10503, "MaxLevel", 5);
				this.IesMods.Add("SkillTree", 10504, "MaxLevel", 5);
				this.IesMods.Add("SkillTree", 10505, "MaxLevel", 5);
				this.IesMods.Add("SkillTree", 10506, "MaxLevel", 5);
				this.IesMods.Add("SkillTree", 10507, "MaxLevel", 5);
				this.IesMods.Add("SkillTree", 10508, "MaxLevel", 5);
				this.IesMods.Add("SkillTree", 10509, "MaxLevel", 5);
			}

			foreach (var skillTreeData in this.Data.SkillTreeDb.Entries)
			{
				if (skillTreeData.MaxLevel <= 0)
					continue;

				this.IesMods.Add("SkillTree", (int)skillTreeData.SkillId, "MaxLevel", skillTreeData.MaxLevel);
				this.IesMods.Add("SkillTree", GetSkillTreeClassId(skillTreeData), "MaxLevel", skillTreeData.MaxLevel);
			}

			// The client still has the old Stegreifspiel attributes in its
			// local IES, so detach them from Pied Piper instead of merely
			// renaming them. The old custom Pied Piper slots are also cleared
			// after compacting the visible rows below.
			foreach (var classId in new[] { 303, 402, 403, 702, 801, 901, 902, 903, 904, 905, 1001 })
			{
				this.IesMods.Add("AbilityTree", classId, "Category", "None");
				this.IesMods.Add("AbilityTree", classId, "ClassName", "RemovedSymphonyAttribute");
				this.IesMods.Add("AbilityTree", classId, "Job", 0);
				this.IesMods.Add("AbilityTree", classId, "JobID", 0);
				this.IesMods.Add("AbilityTree", classId, "JobId", 0);
				this.IesMods.Add("AbilityTree", classId, "MaxLevel", 0);
				this.IesMods.Add("AbilityTree", classId, "UnlockScript", "None");
				this.IesMods.Add("AbilityTree", classId, "PriceTimeScript", "None");
			}

			foreach (var abilityId in new[] { 318016, 318017, 318018, 318019, 318020, 318025 })
			{
				this.IesMods.Add("Ability", abilityId, "ClassName", "RemovedSymphonyAttribute");
				this.IesMods.Add("Ability", abilityId, "Name", "");
				this.IesMods.Add("Ability", abilityId, "SkillCategory", "None");
				this.IesMods.Add("Ability", abilityId, "SkillName", "None");
				this.IesMods.Add("Ability", abilityId, "MaxLevel", 0);
			}

			foreach (var abilityTree in new[]
			{
				(ClassId: 101, AbilityId: 318006, ClassName: "PiedPiper6", MaxLevel: 1, UnlockScript: "UNLOCK_ABIL_SKILL", PriceTimeScript: "ABIL_COMMON_PRICE_100LV"),
				(ClassId: 201, AbilityId: 318011, ClassName: "PiedPiper11", MaxLevel: 100, UnlockScript: "UNLOCK_ABIL_SKILL", PriceTimeScript: "ABIL_REINFORCE_PRICE"),
				(ClassId: 301, AbilityId: 318012, ClassName: "PiedPiper12", MaxLevel: 10, UnlockScript: "UNLOCK_ABIL_SKILL", PriceTimeScript: "ABIL_ABOVE_NORMAL_PRICE"),
				(ClassId: 401, AbilityId: 318013, ClassName: "PiedPiper13", MaxLevel: 10, UnlockScript: "UNLOCK_ABIL_SKILL", PriceTimeScript: "ABIL_ABOVE_NORMAL_PRICE"),
				(ClassId: 501, AbilityId: 318015, ClassName: "PiedPiper15", MaxLevel: 5, UnlockScript: "UNLOCK_BASE_LEVEL", PriceTimeScript: "ABIL_COMMON_PRICE_100LV"),
				(ClassId: 601, AbilityId: 318021, ClassName: "PiedPiper21", MaxLevel: 30, UnlockScript: "UNLOCK_ABIL_OTHERABILITY", PriceTimeScript: "HIDDENABIL_PRICE_COND_REINFORCE"),
				(ClassId: 701, AbilityId: 318022, ClassName: "PiedPiper22", MaxLevel: 1, UnlockScript: "UNLOCK_ABIL_JOB_LEVEL", PriceTimeScript: "HIDDENABIL_PRICE_COND_JOBLEVEL"),
			})
			{
				this.IesMods.Add("AbilityTree", abilityTree.ClassId, "Category", "PiedPiper");
				this.IesMods.Add("AbilityTree", abilityTree.ClassId, "ClassName", abilityTree.ClassName);
				this.IesMods.Add("AbilityTree", abilityTree.ClassId, "AbilityID", abilityTree.AbilityId);
				this.IesMods.Add("AbilityTree", abilityTree.ClassId, "AbilityId", abilityTree.AbilityId);
				this.IesMods.Add("AbilityTree", abilityTree.ClassId, "Ability", abilityTree.AbilityId);
				this.IesMods.Add("AbilityTree", abilityTree.ClassId, "Job", 3012);
				this.IesMods.Add("AbilityTree", abilityTree.ClassId, "JobID", 3012);
				this.IesMods.Add("AbilityTree", abilityTree.ClassId, "JobId", 3012);
				this.IesMods.Add("AbilityTree", abilityTree.ClassId, "MaxLevel", abilityTree.MaxLevel);
				this.IesMods.Add("AbilityTree", abilityTree.ClassId, "UnlockScript", abilityTree.UnlockScript);
				this.IesMods.Add("AbilityTree", abilityTree.ClassId, "PriceTimeScript", abilityTree.PriceTimeScript);
			}

			this.IesMods.Add("Skill", 31704, "Name", "Best Friend");
			this.IesMods.Add("Skill", 31704, "Caption", "{#005060}Applied upon learning{/}{nl}Positive Symphony of Fate results grant Nagetier? stacks. At 3 stacks, summons Nagetier to mark enemies with A RAT!?. Negative results remove all stacks.");
			this.IesMods.Add("Skill", 31704, "Desc", "{#005060}Applied upon learning{/}");
			this.IesMods.Add("Skill", 31704, "Description", "{#005060}Applied upon learning{/}");
			this.IesMods.Add("Skill", 31704, "SkillType", "Passive");
			this.IesMods.Add("Skill", 31704, "UseType", "Self");
			this.IesMods.Add("Skill", 31704, "ClassType", "None");
			this.IesMods.Add("Skill", 31704, "AttackType", "None");
			this.IesMods.Add("Skill", 31704, "BasicSP", 0);
			this.IesMods.Add("Skill", 31704, "SpendSP", 0);
			this.IesMods.Add("Skill", 31704, "CoolDown", 0);
			this.IesMods.Add("Skill", 31704, "BasicCoolDown", 0);
			this.IesMods.Add("Skill", 31704, "SklFactor", 300);
			this.IesMods.Add("Skill", 31704, "SklFactorByLevel", "355.5556");

			var bestFriendTooltips = new (int Id, string ClassName, string Name, string Description)[]
			{
				(2174, "HamelnNagetier_Buff", "Nagetier?", "Symphony of Fate positive result stack. At 3 stacks, summons your loyal Nagetier to fight by your side."),
				(2175, "HamelnNagetier_Debuff", "A RAT!?", "Marked by Nagetier. The summoning Pied Piper's next direct hit consumes the mark."),
				(697416, "BestFriend_Duration_Buff", "Best Friend", "Nagetier is fighting by your side."),
			};

			foreach (var buffTooltip in bestFriendTooltips)
			{
				this.IesMods.Add("Buff", buffTooltip.Id, "ClassName", buffTooltip.ClassName);
				this.IesMods.Add("Buff", buffTooltip.Id, "Name", buffTooltip.Name);
				this.IesMods.Add("Buff", buffTooltip.Id, "Caption", buffTooltip.Description);
				this.IesMods.Add("Buff", buffTooltip.Id, "Desc", buffTooltip.Description);
				this.IesMods.Add("Buff", buffTooltip.Id, "Description", buffTooltip.Description);
				this.IesMods.Add("Buff", buffTooltip.Id, "ToolTip", buffTooltip.Description);
				this.IesMods.Add("Buff", buffTooltip.Id, "Tooltip", buffTooltip.Description);
			}

			this.IesMods.Add("Ability", 318012, "Name", "Best Friend: A RAT!? Burst");
			this.IesMods.Add("Ability", 318012, "Caption", "When you directly hit an enemy affected by A RAT!? from your own Nagetier, consumes the mark and deals Best Friend damage. Damage cannot critically hit.");
			this.IesMods.Add("Ability", 318013, "Name", "Best Friend: Rare Species");
			this.IesMods.Add("Ability", 318013, "Caption", "When Best Friend summons Nagetier, has a 25% chance to summon a white Nagetier instead. White Nagetier lasts 50 seconds and makes A RAT!? burst for 1.5% more damage.");

			this.IesMods.Add("Skill", 31708, "CoolDown", 35000);
			this.IesMods.Add("Skill", 31708, "BasicCoolDown", 35000);

			this.IesMods.Add("Skill", 30504, "CoolDown", 1000);
			this.IesMods.Add("Skill", 30504, "BasicCoolDown", 1000);
			this.IesMods.Add("Skill", 30504, "ShootTime", 0);
			this.IesMods.Add("Skill", 30504, "DelayTime", 0);
			this.IesMods.Add("Skill", 30504, "CancelTime", 0);
			this.IesMods.Add("Skill", 30504, "HoldTime", "0");

			this.IesMods.Add("Skill", 30509, "Caption", "{#339999}{ol}[Stealth]{/}{/} Duration and movement speed scale with Poison Mastery.{nl}Any damage you deal or any skill use removes Stealth.{nl}{#339999}{ol}[Hemotoxic Miasma]{/}{/} Buff Duration: 5 seconds{nl}Wugushi skills that hit bleeding enemies inflict 60% healing reduction for 8 seconds.");

			this.IesMods.Add("Buff", 1065, "ApplyTime", 100000);
			this.IesMods.Add("Buff", 1065, "UpdateTime", 1000);
			this.IesMods.Add("Buff", 1065, "CT_OverBuff", 1);
			this.IesMods.Add("Buff", 1126, "ClassName", "Hemotoxic_Miasma_Buff");
			this.IesMods.Add("Buff", 1126, "Type", "Buff");
			this.IesMods.Add("Buff", 1126, "Name", "Hemotoxic Miasma");
			this.IesMods.Add("Buff", 1126, "Caption", "For 5 seconds, Wugushi skills reduce healing by 60% for 8 seconds when they hit bleeding enemies.");
			this.IesMods.Add("Buff", 1126, "Desc", "For 5 seconds, Wugushi skills reduce healing by 60% for 8 seconds when they hit bleeding enemies.");
			this.IesMods.Add("Buff", 1126, "Description", "For 5 seconds, Wugushi skills reduce healing by 60% for 8 seconds when they hit bleeding enemies.");
			this.IesMods.Add("Buff", 1126, "ToolTip", "For 5 seconds, Wugushi skills reduce healing by 60% for 8 seconds when they hit bleeding enemies.");
			this.IesMods.Add("Buff", 1126, "Tooltip", "For 5 seconds, Wugushi skills reduce healing by 60% for 8 seconds when they hit bleeding enemies.");
			this.IesMods.Add("Buff", 1126, "Icon", "state_posion");
			this.IesMods.Add("Buff", 1126, "ShowIcon", "YES");
			this.IesMods.Add("Buff", 1126, "Group1", "Buff");
			this.IesMods.Add("Buff", 1126, "Group2", "Normal");
			this.IesMods.Add("Buff", 1126, "Group3", "None");
			this.IesMods.Add("Buff", 1126, "ApplyTime", 5000);
			this.IesMods.Add("Buff", 1126, "UpdateTime", 0);
			this.IesMods.Add("Buff", 1126, "CT_SlotType", "slot_buff");
			this.IesMods.Add("Buff", 1126, "OverBuff", 1);
			this.IesMods.Add("Buff", 1126, "CT_OverBuff", 1);
			this.IesMods.Add("Buff", 1126, "ApplyLimitCountBuff", "YES");
			this.IesMods.Add("Buff", 1126, "CT_RemoveBySkill", "YES");
			this.IesMods.Add("Buff", 1126, "DeadRemove", "YES");
			this.IesMods.Add("Buff", 1126, "Save", "NO");

			this.IesMods.Add("Buff", 1127, "ClassName", "GoldenFrog_Slow_Debuff");
			this.IesMods.Add("Buff", 1127, "Type", "Debuff");
			this.IesMods.Add("Buff", 1127, "Name", "Golden Frog: Slow");
			this.IesMods.Add("Buff", 1127, "Caption", "Movement Speed is reduced while near Tutu.");
			this.IesMods.Add("Buff", 1127, "Desc", "Movement Speed is reduced while near Tutu.");
			this.IesMods.Add("Buff", 1127, "Description", "Movement Speed is reduced while near Tutu.");
			this.IesMods.Add("Buff", 1127, "ToolTip", "Movement Speed is reduced while near Tutu.");
			this.IesMods.Add("Buff", 1127, "Tooltip", "Movement Speed is reduced while near Tutu.");
			this.IesMods.Add("Buff", 1127, "Icon", "arch_jincangu");
			this.IesMods.Add("Buff", 1127, "ShowIcon", "YES");
			this.IesMods.Add("Buff", 1127, "Group1", "Debuff");
			this.IesMods.Add("Buff", 1127, "Group2", "Poison");
			this.IesMods.Add("Buff", 1127, "Group3", "None");
			this.IesMods.Add("Buff", 1127, "ApplyTime", 1500);
			this.IesMods.Add("Buff", 1127, "UpdateTime", 0);
			this.IesMods.Add("Buff", 1127, "CT_SlotType", "slot_ability");
			this.IesMods.Add("Buff", 1127, "OverBuff", 1);
			this.IesMods.Add("Buff", 1127, "CT_OverBuff", 1);
			this.IesMods.Add("Buff", 1127, "ApplyLimitCountBuff", "YES");
			this.IesMods.Add("Buff", 1127, "CT_RemoveBySkill", "YES");
			this.IesMods.Add("Buff", 1127, "DeadRemove", "YES");
			this.IesMods.Add("Buff", 1127, "Save", "NO");

			this.IesMods.Add("Buff", 1128, "ClassName", "Poison_Mastery_Buff");
			this.IesMods.Add("Buff", 1128, "Name", "{#006633}Poison Mastery{/}");
			this.IesMods.Add("Buff", 1128, "Caption", "{#006633}Poison INT scaling efficiency. Stack count shows the current percentage.{/}");
			this.IesMods.Add("Buff", 1128, "Desc", "{#006633}Poison INT scaling efficiency. Stack count shows the current percentage.{/}");
			this.IesMods.Add("Buff", 1128, "Description", "{#006633}Poison INT scaling efficiency. Stack count shows the current percentage.{/}");
			this.IesMods.Add("Buff", 1128, "ToolTip", "{#006633}Poison INT scaling efficiency. Stack count shows the current percentage.{/}");
			this.IesMods.Add("Buff", 1128, "Tooltip", "{#006633}Poison INT scaling efficiency. Stack count shows the current percentage.{/}");
			this.IesMods.Add("Buff", 1128, "Icon", "state_posion");
			this.IesMods.Add("Buff", 1128, "ShowIcon", "None");
			this.IesMods.Add("Buff", 1128, "Group1", "Buff");
			this.IesMods.Add("Buff", 1128, "Group2", "Normal");
			this.IesMods.Add("Buff", 1128, "Group3", "None");
			this.IesMods.Add("Buff", 1128, "ApplyTime", 1800000);
			this.IesMods.Add("Buff", 1128, "UpdateTime", 0);
			this.IesMods.Add("Buff", 1128, "CT_SlotType", "slot_ability");
			this.IesMods.Add("Buff", 1128, "OverBuff", 100);
			this.IesMods.Add("Buff", 1128, "CT_OverBuff", 100);
			this.IesMods.Add("Buff", 1128, "ApplyLimitCountBuff", "YES");
			this.IesMods.Add("Buff", 1128, "CT_RemoveBySkill", "NO");
			this.IesMods.Add("Buff", 1128, "DeadRemove", "NO");
			this.IesMods.Add("Buff", 1128, "Save", "NO");

			this.IesMods.Add("Buff", 1067, "ClassName", "WideMiasma_Debuff");
			this.IesMods.Add("Buff", 1067, "Type", "Debuff");
			this.IesMods.Add("Buff", 1067, "Name", "Wide Miasma");
			this.IesMods.Add("Buff", 1067, "Caption", "Healing received is reduced. Reduction scales with skill level and Poison Mastery.");
			this.IesMods.Add("Buff", 1067, "Desc", "Healing received is reduced. Reduction scales with skill level and Poison Mastery.");
			this.IesMods.Add("Buff", 1067, "Description", "Healing received is reduced. Reduction scales with skill level and Poison Mastery.");
			this.IesMods.Add("Buff", 1067, "ToolTip", "Healing received is reduced. Reduction scales with skill level and Poison Mastery.");
			this.IesMods.Add("Buff", 1067, "Tooltip", "Healing received is reduced. Reduction scales with skill level and Poison Mastery.");
			this.IesMods.Add("Buff", 1067, "Icon", "arch_wugonggu");
			this.IesMods.Add("Buff", 1067, "ShowIcon", "YES");
			this.IesMods.Add("Buff", 1067, "Group1", "Debuff");
			this.IesMods.Add("Buff", 1067, "Group2", "Normal");
			this.IesMods.Add("Buff", 1067, "Group3", "None");
			this.IesMods.Add("Buff", 1067, "ApplyTime", 20000);
			this.IesMods.Add("Buff", 1067, "UpdateTime", 0);
			this.IesMods.Add("Buff", 1067, "CT_SlotType", "slot_ability");
			this.IesMods.Add("Buff", 1067, "OverBuff", 100);
			this.IesMods.Add("Buff", 1067, "CT_OverBuff", 100);
			this.IesMods.Add("Buff", 1067, "ApplyLimitCountBuff", "YES");
			this.IesMods.Add("Buff", 1067, "CT_RemoveBySkill", "YES");
			this.IesMods.Add("Buff", 1067, "DeadRemove", "YES");
			this.IesMods.Add("Buff", 1067, "Save", "NO");

			this.IesMods.Add("Buff", 120007, "ClassName", "DecreaseHeal_Debuff");
			this.IesMods.Add("Buff", 120007, "Type", "Debuff");
			this.IesMods.Add("Buff", 120007, "Name", "Healing Reduction");
			this.IesMods.Add("Buff", 120007, "Caption", "Healing received is reduced.");
			this.IesMods.Add("Buff", 120007, "Desc", "Healing received is reduced.");
			this.IesMods.Add("Buff", 120007, "Description", "Healing received is reduced.");
			this.IesMods.Add("Buff", 120007, "ToolTip", "Healing received is reduced.");
			this.IesMods.Add("Buff", 120007, "Tooltip", "Healing received is reduced.");
			this.IesMods.Add("Buff", 120007, "Icon", "arch_wugonggu");
			this.IesMods.Add("Buff", 120007, "ShowIcon", "YES");
			this.IesMods.Add("Buff", 120007, "Group1", "Debuff");
			this.IesMods.Add("Buff", 120007, "Group2", "Normal");
			this.IesMods.Add("Buff", 120007, "Group3", "None");
			this.IesMods.Add("Buff", 120007, "ApplyTime", 20000);
			this.IesMods.Add("Buff", 120007, "UpdateTime", 0);
			this.IesMods.Add("Buff", 120007, "CT_SlotType", "slot_ability");
			this.IesMods.Add("Buff", 120007, "OverBuff", 100);
			this.IesMods.Add("Buff", 120007, "CT_OverBuff", 100);
			this.IesMods.Add("Buff", 120007, "ApplyLimitCountBuff", "YES");
			this.IesMods.Add("Buff", 120007, "CT_RemoveBySkill", "YES");
			this.IesMods.Add("Buff", 120007, "DeadRemove", "YES");
			this.IesMods.Add("Buff", 120007, "Save", "NO");

			var symphonyBuffTooltips = new (int Id, string ClassName, string Name, string Description)[]
			{
				(697401, "Symphony_MarchOfTriumph_Buff", "March of Triumph", "Physical Attack and Magic Attack +25%, Movement Speed +8, and immunity from knockdowns, knockbacks, and stagger."),
				(697402, "Symphony_BalladOfSanctuary_Buff", "Ballad of Sanctuary", "Restores 60% HP and 60% SP over 20 seconds. Physical Defense and Magic Defense +20%."),
				(697403, "Symphony_DanceOfSwiftness_Buff", "Fire Dance", "Skill cooldowns are reduced by 20% while this buff is active. AoE Attack Ratio is increased."),
				(697404, "Symphony_GoldenResonance_Buff", "Bleed for Me", "After 12 seconds, removes 1 to 2 removable debuffs."),
				(697405, "Symphony_HerosCrescendo_Buff", "Poison Heart", "Minimum Critical Rate +10% and Critical Damage +15%."),
				(630125, "Symphony_IronWaltz_Buff", "Iron Waltz", "Damage received -15%, Block +25%, and Evasion +25%."),
				(630126, "Symphony_EchoOfReversal_Buff", "Echo of Reversal", "20% chance when hit to reflect 30% of received damage."),
				(697408, "Symphony_FestivalOverture_Buff", "Festival Overture", "Accuracy +60%."),
				(697409, "Symphony_FinaleOfResurrection_Buff", "Finale of Resurrection", "Revives once with 40% HP if defeated during the buff."),
				(697410, "Symphony_BrokenTempo_Debuff", "Broken Tempo", "Instantly forces one random hotbar skill into 15 seconds of cooldown."),
				(630130, "Symphony_CursedChorus_Debuff", "Cursed Chorus", "After 12 seconds, reduces HP or SP to 6.66%."),
				(697412, "Symphony_DiscordantMelody_Debuff", "Discordant Melody", "Stunned for 2 seconds."),
				(697413, "Symphony_DanceOfMadness_Debuff", "Dance of Madness", "Blinded for 4 seconds. Does not affect the Pied Piper who played the melody."),
			};

			foreach (var buffTooltip in symphonyBuffTooltips)
			{
				this.IesMods.Add("Buff", buffTooltip.Id, "ClassName", buffTooltip.ClassName);
				this.IesMods.Add("Buff", buffTooltip.Id, "Name", buffTooltip.Name);
				this.IesMods.Add("Buff", buffTooltip.Id, "Caption", buffTooltip.Description);
				this.IesMods.Add("Buff", buffTooltip.Id, "Desc", buffTooltip.Description);
				this.IesMods.Add("Buff", buffTooltip.Id, "Description", buffTooltip.Description);
				this.IesMods.Add("Buff", buffTooltip.Id, "ToolTip", buffTooltip.Description);
				this.IesMods.Add("Buff", buffTooltip.Id, "Tooltip", buffTooltip.Description);
			}

			this.IesMods.Add("SharedConst", 177, "Value", this.Conf.World.StorageFee); // WAREHOUSE_PRICE
			this.IesMods.Add("SharedConst", 10004, "Value", this.Conf.World.StorageExtCost); // WAREHOUSE_EXTEND_PRICE
			this.IesMods.Add("SharedConst", 10006, "Value", this.Conf.World.StorageExtCount); // WAREHOUSE_EXTEND_SLOT_COUNT
			this.IesMods.Add("SharedConst", 10010, "Value", this.Conf.World.StorageMaxExtensions); // WAREHOUSE_MAX_COUNT
			this.IesMods.Add("SharedConst", 10012, "Value", this.Conf.World.TeamStorageExtCost); // ACCOUNT_WAREHOUSE_EXTEND_PRICE
			this.IesMods.Add("SharedConst", 10013, "Value", this.Conf.World.TeamStorageMaxSilverExpands); // ACCOUNT_WAREHOUSE_MAX_EXTEND_COUNT
			this.IesMods.Add("SharedConst", 10021, "Value", this.Conf.World.TeamStorageMinimumLevelRequired); // ACCOUNT_WAREHOUSE_LIMIT_LEVEL

			this.IesMods.Add("SharedConst", 102, "Value", this.Conf.World.MaxLevel);
			this.IesMods.Add("SharedConst", 103, "Value", this.Conf.World.MaxCompanionLevel);
			this.IesMods.Add("SharedConst", 104, "Value", this.Conf.World.MaxBaseJobLevel);
			this.IesMods.Add("SharedConst", 105, "Value", this.Conf.World.MaxAdvanceJobLevel);
			this.IesMods.Add("SharedConst", 100050, "Value", this.Conf.World.JobMaxRank); // JOB_CHANGE_MAX_RANK

			// Magical Amulets are invisible by default, this makes them visible.
			if (Feature.IsEnabled("MagicalAmulet"))
			{
				for (var i = 0; i < 26; i++)
					this.IesMods.Add("Item", 648001 + i, "MarketCategory", "Misc_Usual");
			}

			if (Feature.IsEnabled("UnlockAllCompanions"))
			{
				foreach (var companion in Instance.Data.CompanionDb.Entries.Values)
				{
					if (companion.Id == 1 || !Instance.Data.MonsterDb.TryFind(companion.ClassName, out _))
						continue;
					this.IesMods.Add("Companion", companion.Id, "SellPrice", "SCR_GET_VELHIDER_PRICE");
					this.IesMods.Add("Companion", companion.Id, "ShopGroup", "Normal");
				}
			}

			//foreach (var item in this.Data.ItemDb.Entries.Values)
			//	this.IesMods.Add("Item", item.Id, "UserTrade", "YES");
		}

		private static int GetSkillTreeClassId(SkillTreeData skillTreeData)
		{
			var jobTree = ((int)skillTreeData.JobId) / 1000;
			var jobIndex = ((int)skillTreeData.JobId) % 1000;
			var skillIndex = ((int)skillTreeData.SkillId) % 100;

			return jobTree * 10000 + jobIndex * 100 + skillIndex;
		}

		/// <summary>
		/// Sets up Dialog Functions.
		/// </summary>
		private void LoadDialogFunctions()
		{
			Log.Info("Loading dialog functions...");

			try
			{
				this.DialogFunctions.LoadMethods();
				var staticQuestFallbacks = this.DialogFunctions.AddStaticQuestFallbacks(this.Data.QuestDb);
				Log.Info("  loaded {0} dialog functions ({1} static quest fallbacks).", this.DialogFunctions.Count, staticQuestFallbacks);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to load dialog functions: {0}", ex);
				ConsoleUtil.Exit(1);
			}
		}

		/// <summary>
		/// Sets up Trigger Functions.
		/// </summary>
		private void LoadTriggerFunctions()
		{
			Log.Info("Loading trigger functions...");

			try
			{
				this.TriggerFunctions.LoadMethods();
				Log.Info("  loaded {0} trigger functions.", this.TriggerFunctions.Count);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to load dialog functions: {0}", ex);
				ConsoleUtil.Exit(1);
			}
		}

		/// <summary>
		/// Called when a new connection is accepted.
		/// </summary>
		/// <param name="conn"></param>
		private void OnConnectionAccepted(ZoneConnection conn)
		{
			Log.Info("New connection accepted from '{0}'.", conn.Address);
		}

		/// <summary>
		/// Called when a new connection was rejected.
		/// </summary>
		/// <param name="conn"></param>
		/// <param name="reason"></param>
		private void OnConnectionRejected(ZoneConnection conn, string reason)
		{
			Log.Info("Connection rejected from '{0}'.", conn.Address);
		}

		private void StartAutoSaveService()
		{
			try
			{
				var autoSaveSlots = this.Conf.World.AutoSaveSlots;
				var autoSaveIntervalPerSlot = TimeSpan.FromMinutes(this.Conf.World.AutoSaveIntervalMinutes);
				var orphanCleanupCycles = this.Conf.World.OrphanCleanupEnabled ? this.Conf.World.OrphanCleanupCycles : 0;

				_autoSaveService = new AutoSaveService(this, this.Database, autoSaveSlots, autoSaveIntervalPerSlot, orphanCleanupCycles);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to initialize AutoSave Service: {0}", ex);
			}
		}

		private void StartOrphanCleanupService()
		{
			if (!this.Conf.World.OrphanCleanupEnabled)
			{
				Log.Info("OrphanCleanupService is disabled in configuration.");
				return;
			}

			try
			{
				var batchSize = this.Conf.World.OrphanCleanupBatchSize;

				_orphanCleanupService = new OrphanCleanupService(this.Database, batchSize);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to initialize OrphanCleanup Service: {0}", ex);
			}
		}

		/// <summary>
		/// Gets the orphan cleanup service instance for triggering cleanup.
		/// </summary>
		public OrphanCleanupService OrphanCleanupService => _orphanCleanupService;

		private void StartLogCleanupService()
		{
			try
			{
				var retentionDays = this.Conf.World.LogCleanupRetentionDays;
				var intervalHours = this.Conf.World.LogCleanupIntervalHours;

				_logCleanupService = new LogCleanupService(this.Database, retentionDays, TimeSpan.FromHours(intervalHours));
			}
			catch (Exception ex)
			{
				Log.Error("Failed to initialize LogCleanup Service: {0}", ex);
			}
		}

		private void StartDeadConnectionSweepService()
		{
			try
			{
				_deadConnectionSweepService = new DeadConnectionSweepService(TimeSpan.FromSeconds(15));
			}
			catch (Exception ex)
			{
				Log.Error("Failed to initialize DeadConnectionSweep Service: {0}", ex);
			}
		}

		public void StopServices()
		{
			Log.Info("Stopping server services...");

			// Block new connections (don't call _acceptor.Stop()
			// as Yggdrasil's ResetSocket causes an exception loop).
			this.BlockNewConnections = true;
			Log.Info("New connections blocked.");

			// Stop world update loop (gracefully if possible)
			this.World?.Heartbeat.Stop();
			Log.Info("World stopped.");

			// Dispose AutoSave Service
			_autoSaveService?.Dispose();
			Log.Info("AutoSave Service stopped.");

			// Dispose OrphanCleanup Service
			_orphanCleanupService?.Dispose();
			Log.Info("OrphanCleanup Service stopped.");

			// Dispose LogCleanup Service
			_logCleanupService?.Dispose();
			Log.Info("LogCleanup Service stopped.");

			// Dispose DeadConnectionSweep Service
			_deadConnectionSweepService?.Dispose();
			Log.Info("DeadConnectionSweep Service stopped.");

			// Disconnect communicator
			//Communicator?.Disconnect();
			Log.Info("Communicator disconnected.");

			// Other cleanup...
			Log.Info("Server services stopped.");
		}
	}
}
