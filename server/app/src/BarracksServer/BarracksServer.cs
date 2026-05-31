using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Melia.Barracks.Database;
using Melia.Barracks.Events;
using Melia.Barracks.Network;
using Melia.Barracks.Util;
using Melia.Shared;
using Melia.Shared.Data.Database;
using Melia.Shared.IES;
using Melia.Shared.Network;
using Melia.Shared.Network.Inter.Messages;
using Yggdrasil.Logging;
using Yggdrasil.Network.Communication;
using Yggdrasil.Network.Communication.Messages;
using Yggdrasil.Network.TCP;
using Yggdrasil.Util;

namespace Melia.Barracks
{
	/// <summary>
	/// Represents the barracks server.
	/// </summary>
	public class BarracksServer : Server
	{
		/// <summary>
		/// Returns this server's type.
		/// </summary>
		public override ServerType Type => ServerType.Barracks;

		/// <summary>
		/// Returns global instance of the barracks server.
		/// </summary>
		public readonly static BarracksServer Instance = new();

		private TcpConnectionAcceptor<BarracksConnection> _acceptor;
		private readonly Dictionary<string, int> _zoneServerNames = new();

		/// <summary>
		/// Returns the server's inter-server communicator.
		/// </summary>
		public Communicator Communicator { get; private set; }

		/// <summary>
		/// Returns a reference to the server's packet handlers.
		/// </summary>
		public PacketHandler PacketHandler { get; } = new PacketHandler();

		/// <summary>
		/// Returns reference to the server's database interface.
		/// </summary>
		public BarracksDb Database { get; } = new BarracksDb();

		/// <summary>
		/// Returns a reference to the server's event manager.
		/// </summary>
		public ServerEvents ServerEvents { get; } = new ServerEvents();

		/// <summary>
		/// Returns reference to the server's IES mods.
		/// </summary>
		public IesModList IesMods { get; } = new IesModList();

		/// <summary>
		/// Runs the server.
		/// </summary>
		/// <param name="args"></param>
		public override void Run(string[] args)
		{
			this.GetServerId(args, out var groupId, out var serverId);
			var title = string.Format("Barracks ({0}, {1})", groupId, serverId);

			ConsoleUtil.WriteHeader(ConsoleHeader.ProjectName, title, ConsoleColor.Magenta, ConsoleHeader.Logo, ConsoleHeader.Credits);
			ConsoleUtil.LoadingTitle();

			Log.Init("BarracksServer" + serverId);

			this.NavigateToRoot();

			this.LoadConf();
			this.LoadPackages();
			this.LoadVersionInfo();
			this.LoadLocalization(this.Conf);
			this.LoadData(ServerType.Barracks);
			this.LoadServerList(this.Data.ServerDb, ServerType.Barracks, groupId, serverId);
			this.InitDatabase(this.Database, this.Conf);
			this.CheckDatabaseUpdates();
			this.Database.ClearLoginStates();
			this.LoadIesMods();
			this.LoadScripts("barracks");

			this.StartCommunicator();
			this.StartAcceptor();

			ConsoleUtil.RunningTitle();
			new BarracksConsoleCommands().Wait();
		}

		/// <summary>
		/// Sets up IES mods.
		/// </summary>
		private void LoadIesMods()
		{
			// This method is temporary until we have a more proper way
			// way of handling IES mods.

			// Add IES mods to apply the server-side skin tone data changes
			// on the client. This, in combination with our custom data,
			// enables three additional skin tones during character creation
			// that match the skin tone images displayed.
			var skinTonesData = this.Data.SkinToneDb.Entries;
			foreach (var data in skinTonesData)
			{
				this.IesMods.Add("SkinTone", data.ClassId, "UseableBarrack", data.Creation ? "YES" : "NO");
				this.IesMods.Add("SkinTone", data.ClassId, "Red", ((data.Color & 0x00FF0000) >> 16).ToString());
				this.IesMods.Add("SkinTone", data.ClassId, "Green", ((data.Color & 0x0000FF00) >> 08).ToString());
				this.IesMods.Add("SkinTone", data.ClassId, "Blue", ((data.Color & 0x000000FF) >> 00).ToString());
			}

			this.IesMods.Add("Skill", 22503, "CoolDown", "40000");
			this.IesMods.Add("Skill", 42130, "CoolDown", "40000");
			this.IesMods.Add("Skill", 51745, "CoolDown", "40000");
			this.IesMods.Add("Skill", 31708, "CoolDown", "35000");
			this.IesMods.Add("Skill", 31708, "BasicCoolDown", "35000");
			this.IesMods.Add("Skill", 30504, "CoolDown", 1000);
			this.IesMods.Add("Skill", 30504, "BasicCoolDown", 1000);
			this.IesMods.Add("Skill", 30504, "ShootTime", 0);
			this.IesMods.Add("Skill", 30504, "DelayTime", 0);
			this.IesMods.Add("Skill", 30504, "CancelTime", 0);
			this.IesMods.Add("Skill", 30504, "HoldTime", "0");
			this.IesMods.Add("Skill", 30509, "Caption", "{#339999}{ol}[Stealth]{/}{/} Duration and movement speed scale with Poison Mastery.{nl}Any damage you deal or any skill use removes Stealth.{nl}{#339999}{ol}[Hemotoxic Miasma]{/}{/} Buff Duration: 5 seconds{nl}Wugushi skills that hit bleeding enemies inflict 60% healing reduction for 8 seconds.");
			this.IesMods.Add("Skill", 32218, "Caption", "Focus your mind and shoot an arrow at the target in front of you. Automatically fires upon completion of casting. Movement Speed decreases by 30% during casting.{nl}{nl}{#339999}{ol}[Arts] Concentrated Shot: Piercing Shot{/}{/}{nl}Skill Factor is halved but obtains penetration effect.");
			this.IesMods.Add("Skill", 32219, "Caption", "Move quickly in the direction you specify to attack the target. If the target is not in range, or if there is no target, move backward. Becomes invincible while moving. However, you can't evade targeting-specific attacks.{nl}{nl}{#339999}{ol}Dodging Shot: Speedy{/}{/}{nl}Does not fire arrows but movement range drastically increased.");
			this.IesMods.Add("Skill", 32220, "Caption", "Shoots an arrow that rains down from the sky over a designated area, with a chance to Stun over time.");
			this.IesMods.Add("Skill", 32221, "Caption", "Consume all of your Charge Arrow, firing shots that scatter over a short distance equal to the number of buffs consumed. Buff removal effect applied per scattered hit.{nl}{nl}{#339999}{ol}[Arts] Scatter Shot: Explosive{/}{/}{nl}Attach an Explosive that lasts for 20 seconds on the target hit. The Explosive is triggered when Angelic Arrow accurately hits. Scatter Shot Skill Factor x 9.");
			this.IesMods.Add("Skill", 32222, "Caption", "Consume all of your Charge Arrow to fire a shot that makes an explosion in a close range. The range expands in proportion to the consumed Charge Arrow stacks.{nl}{nl}{#339999}{ol}Blasting Shot: Control Recoil{/}{/}{nl}No longer knocked back after casting Blasting Shot.");
			this.IesMods.Add("Skill", 32223, "Caption", "Consume all of your Charge Arrow to fire a powerful, penetrating arrow.{nl}{nl}{#339999}{ol}[Arts] Angelic Arrow: Swift{/}{/}{nl}Casting Time is fixed to 1.2 seconds and Skill Factor decreases by 25%. Can only be changed in towns.");
			this.IesMods.Add("Ability", 321242, "Caption", "All Godeye skill cooldown is drastically reduced but SP Consumption increases by 100%.");
			this.IesMods.Add("Ability", 321249, "Caption", "Skill Factor is halved but obtains penetration effect.");
			this.IesMods.Add("Ability", 321250, "Caption", "Does not fire arrows but movement range drastically increased.");
			this.IesMods.Add("Ability", 321251, "Caption", "Attach an Explosive that lasts for 20 seconds on the target hit. The Explosive is triggered when Angelic Arrow accurately hits. Scatter Shot Skill Factor x 9.");
			this.IesMods.Add("Ability", 321252, "Caption", "No longer knocked back after casting Blasting Shot.");
			this.IesMods.Add("Ability", 321253, "Caption", "Casting Time is fixed to 1.2 seconds and Skill Factor decreases by 25%. Can only be changed in towns.");

			this.IesMods.Add("Buff", 3323, "Name", "Embarrassed");
			this.IesMods.Add("Buff", 3323, "Caption", "Attributes are reduced.");
			this.IesMods.Add("Buff", 3323, "Icon", "buff_icon_dark");
			this.IesMods.Add("Buff", 479, "Name", "Embarrassed");
			this.IesMods.Add("Buff", 479, "Caption", "Each stack reduces Evasion, Critical Resistance, Accuracy and Defense. Movement Speed is also reduced.");
			this.IesMods.Add("Buff", 1126, "ClassName", "Hemotoxic_Miasma_Buff");
			this.IesMods.Add("Buff", 1126, "Type", "Buff");
			this.IesMods.Add("Buff", 1126, "Name", "Hemotoxic Miasma");
			this.IesMods.Add("Buff", 1126, "Caption", "For 5 seconds, Wugushi skills reduce healing by 60% for 8 seconds when they hit bleeding enemies.");
			this.IesMods.Add("Buff", 1126, "Icon", "state_posion");
			this.IesMods.Add("Buff", 1126, "ShowIcon", "YES");
			this.IesMods.Add("Buff", 1126, "Group1", "Buff");
			this.IesMods.Add("Buff", 1126, "Group2", "Normal");
			this.IesMods.Add("Buff", 1126, "ApplyTime", 5000);
			this.IesMods.Add("Buff", 1126, "OverBuff", 1);
			this.IesMods.Add("Buff", 1126, "CT_OverBuff", 1);
			this.IesMods.Add("Buff", 1126, "CT_SlotType", "slot_buff");

			void AddJobText(int id, string name, string description, string icon, string costume)
			{
				this.IesMods.Add("Job", id, "Name", name);
				this.IesMods.Add("Job", id, "EngName", name);
				this.IesMods.Add("Job", id, "JobName", name);
				this.IesMods.Add("Job", id, "Caption", description);
				this.IesMods.Add("Job", id, "Desc", description);
				this.IesMods.Add("Job", id, "Description", description);
				this.IesMods.Add("Job", id, "ToolTip", description);
				this.IesMods.Add("Job", id, "Tooltip", description);
				this.IesMods.Add("Job", id, "ClassIcon", icon);
				this.IesMods.Add("Job", id, "IconName", icon);
				this.IesMods.Add("Job", id, "Costume", costume);
				this.IesMods.Add("Job", id, "CostumeClassName", icon);
			}

			AddJobText(2031, "Incendiar", "An explosive Wizard class that overwhelms enemies with destructive fire magic.", "c_wizard_incendiar", "costume_Char2_31");
			AddJobText(3113, "Commodore[A]", "A tactical ranged class that marks targets and supports the frontline with artillery fire.", "c_archer_Commodore", "costume_Char3_30");
			AddJobText(5025, "Commodore[T]", "A tactical ranged class that marks targets and supports the frontline with artillery fire.", "c_archer_Commodore", "costume_Char5_25");

		}

		/// <summary>
		/// Starts accepting connections.
		/// </summary>
		private void StartAcceptor()
		{
			_acceptor = new TcpConnectionAcceptor<BarracksConnection>(this.ServerInfo.Port);
			_acceptor.ConnectionChecker = (conn) => this.CheckConnection(conn, this.Database);
			_acceptor.ConnectionAccepted += this.OnConnectionAccepted;
			_acceptor.ConnectionRejected += this.OnConnectionRejected;
			_acceptor.Listen();

			Log.Status("Server ready, listening on {0}.", _acceptor.Address);
		}

		/// <summary>
		/// Starts the communicator and waits for connections from other
		/// servers.
		/// </summary>
		private void StartCommunicator()
		{
			var commName = "" + this.ServerInfo.Type + this.ServerInfo.Id;
			var authentication = this.Conf.Inter.Authentication;

			this.Communicator = new Communicator(commName, authentication);
			this.Communicator.ClientConnected += this.Communicator_OnClientConnected;
			this.Communicator.ClientDisconnected += this.Communicator_OnClientDisconnected;
			this.Communicator.MessageReceived += this.Communicator_OnMessageReceived;

			this.Communicator.Listen(this.ServerInfo.InterPort);
		}

		/// <summary>
		/// Called when a server connected via the communicator.
		/// </summary>
		/// <param name="commName"></param>
		private void Communicator_OnClientConnected(string commName)
		{
			Log.Info("Accepted connection from server {0}.", commName);
		}

		/// <summary>
		/// Called when a server disconnected from the communicator.
		/// </summary>
		/// <param name="commName"></param>
		private void Communicator_OnClientDisconnected(string commName)
		{
			Log.Info("Lost connection from server {0}.", commName);

			if (_zoneServerNames.TryGetValue(commName, out var serverId))
			{
				var serverUpdateMessage = new ServerUpdateMessage(ServerType.Zone, serverId, 0, ServerStatus.Offline);

				this.ServerList.Update(serverUpdateMessage);
				this.Communicator.Broadcast("ServerUpdates", serverUpdateMessage);
			}
		}

		/// <summary>
		/// Called when a message is received from a server.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="message"></param>
		private void Communicator_OnMessageReceived(string sender, ICommMessage message)
		{
			//Log.Debug("Message received from '{0}': {1}", sender, message);

			switch (message)
			{
				case ServerUpdateMessage serverUpdateMessage:
				{
					if (serverUpdateMessage.ServerType == ServerType.Zone)
						_zoneServerNames[sender] = serverUpdateMessage.ServerId;

					this.ServerList.Update(serverUpdateMessage);
					this.Communicator.Broadcast("ServerUpdates", serverUpdateMessage);

					Send.BC_NORMAL.ZoneTraffic();
					break;
				}
				case RequestMessage requestMessage:
				{
					this.Communicator_OnRequestReceived(sender, requestMessage);
					break;
				}
				case ForceLogOutMessage logoutMessage:
				{
					var connection = this.GetAllConnections().FirstOrDefault(a => a?.Account?.Id == logoutMessage.AccountId);
					connection?.Close();
					break;
				}
			}
		}

		/// <summary>
		/// Called when a request message was received.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="requestMessage"></param>
		private void Communicator_OnRequestReceived(string sender, RequestMessage requestMessage)
		{
			switch (requestMessage.Message)
			{
				case ReqPlayerCountMessage:
				{
					var playerCount = this.ServerList.GetAll(ServerType.Zone).Sum(server => server.CurrentPlayers);

					var message = new ResPlayerCountMessage(playerCount);
					var responseMessage = new ResponseMessage(requestMessage.Id, message);

					this.Communicator.Send(sender, responseMessage);
					break;
				}
				case ReqServerListMessage:
				{
					var servers = this.ServerList.GetAll();

					var message = new ResServerListMessage(servers);
					var responseMessage = new ResponseMessage(requestMessage.Id, message);

					this.Communicator.Send(sender, responseMessage);
					break;
				}
			}
		}

		/// <summary>
		/// Called when a new connection is accepted.
		/// </summary>
		/// <param name="conn"></param>
		private void OnConnectionAccepted(BarracksConnection conn)
		{
			Log.Info("New connection accepted from '{0}'.", conn.Address);
		}

		/// <summary>
		/// Called when a new connection was rejected.
		/// </summary>
		/// <param name="conn"></param>
		/// <param name="reason"></param>
		private void OnConnectionRejected(BarracksConnection conn, string reason)
		{
			Log.Info("Connection rejected from '{0}'. Reason: {1}", conn.Address, reason);
		}

		/// <summary>
		/// Checks for potential updates for the database.
		/// </summary>
		private void CheckDatabaseUpdates()
		{
			Log.Info("Checking for updates...");

			// We had an issue with our update names, and to ensure that we
			// don't break everyone's update history, we'll temporarily fix
			// the update names on the fly. This should be removed at some
			// point in the future.
			this.Database.NormalizeUpdateNames();

			var enumOptions = new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive };
			var filePaths = Directory.GetFiles("sql/updates/", "*.sql", enumOptions).OrderBy(a => a);

			var updateFiles = new Dictionary<string, string>();
			foreach (var filePath in filePaths)
			{
				var updateName = Path.GetFileName(filePath);
				var normalizedName = updateName.ToLower().Replace("update-", "update_");

				if (this.Database.CheckUpdate(normalizedName))
					continue;

				Log.Info("Update '{0}' found, executing...", updateName);
				this.Database.RunUpdate(normalizedName, File.ReadAllText(filePath));
			}
		}

		/// <summary>
		/// Returns a list of all active connections.
		/// </summary>
		/// <returns></returns>
		public BarracksConnection[] GetAllConnections()
			=> _acceptor.GetAllConnections();

		/// <summary>
		/// Broadcasts the packet to all logged in connections.
		/// </summary>
		/// <param name="packet"></param>
		public void Broadcast(Packet packet)
		{
			var connections = this.GetAllConnections();

			foreach (var conn in connections)
			{
				if (conn.LoggedIn)
					conn.Send(packet);
			}
		}
	}
}
