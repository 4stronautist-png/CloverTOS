//--- SoulSociety Script ----------------------------------------------------
// GM Panel Client
//--- Description -----------------------------------------------------------
// Sends a GM-only client panel that builds chat commands from UI fields.
//---------------------------------------------------------------------------

using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Melia.Shared.Game.Const;
using Melia.Zone;
using Melia.Zone.Commands;
using Melia.Zone.Network;
using Melia.Zone.Scripting;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Items;
using Yggdrasil.Util.Commands;
using static Melia.Zone.Scripting.Shortcuts;

public class CloverGmPanelClientScript : ClientScript
{
	private const int ClientChunkSize = 1500;
	private string _panelLua = "";

	protected override void Load()
	{
		AddChatCommand("gmpanel", "", "Opens the SoulSociety GM command panel.", 50, 99, HandleGmPanel);
		AddChatCommand("pos", "", "Copies the current GM position to the clipboard.", 50, 99, HandlePosition);
		AddChatCommand("itemcard", "<player> <card id> <level>", "Creates a leveled monster card.", 50, 99, HandleItemCard);
		AddChatCommand("size", "<player> <scale>", "Changes a player's visual scale.", 50, 99, HandleSize);

		var sourceFilePath = this.GetCallingFilePath();
		var fileDirPath = Path.GetDirectoryName(sourceFilePath);
		var filePath = Path.Combine(fileDirPath, "gm_panel_client.lua");
		_panelLua = File.ReadAllText(filePath);
	}

	protected override void Ready(Character character)
	{
		if (character.Connection?.Account?.Authority < 50)
			return;

		this.SendPanelLua(character);
	}

	private CommandResult HandleGmPanel(Character sender, Character target, string message, string commandName, Arguments args)
	{
		if (sender.Connection?.Account?.Authority < 50)
			return CommandResult.Okay;

		this.SendPanelLua(sender);
		Send.ZC_EXEC_CLIENT_SCP(sender.Connection, "if SSGM_PANEL_OPEN ~= nil then SSGM_PANEL_OPEN() else ui.SysMsg('GM Panel Lua nao carregado. Relogue e tente novamente.') end");
		return CommandResult.Okay;
	}

	private CommandResult HandlePosition(Character sender, Character target, string message, string commandName, Arguments args)
	{
		if (sender.Connection?.Account?.Authority < 50)
			return CommandResult.Okay;

		var pos = sender.Position;
		var dir = sender.Direction.DegreeAngle;
		var text = string.Format(
			CultureInfo.InvariantCulture,
			"/warp {0} {1:0.#####} {2:0.#####} {3:0.#####} -- {4} dir={5:0.#####}",
			sender.Map.Id,
			pos.X,
			pos.Y,
			pos.Z,
			sender.Map.ClassName,
			dir
		);

		sender.ServerMessage("Posicao copiada: {0}", text);
		Send.ZC_EXEC_CLIENT_SCP(sender.Connection, "SSGM_PANEL_COPY_TEXT(" + ToLuaString(text) + ")");
		return CommandResult.Okay;
	}

	private CommandResult HandleItemCard(Character sender, Character target, string message, string commandName, Arguments args)
	{
		if (args.Count < 3)
			return CommandResult.InvalidArgument;

		if (!TryGetOnlinePlayer(args.Get(0), out var player))
		{
			sender.ServerMessage("Character not found.");
			return CommandResult.Okay;
		}

		if (!int.TryParse(args.Get(1), out var itemId) || itemId <= 0)
			return CommandResult.InvalidArgument;

		if (!int.TryParse(args.Get(2), out var level) || level < 1 || level > 10)
			return CommandResult.InvalidArgument;

		var data = ZoneServer.Instance.Data.ItemDb.Find(itemId);
		if (data == null)
		{
			sender.ServerMessage("Item not found.");
			return CommandResult.Okay;
		}

		if (!IsAllowedCard(data.Category, data.Group))
		{
			sender.ServerMessage("Only monster cards can be created with /itemcard.");
			return CommandResult.Okay;
		}

		var item = new Item(itemId, 1);
		item.Properties.SetFloat(PropertyName.CardLevel, level);
		item.Properties.SetFloat(PropertyName.ItemStar, level);
		item.Properties.SetFloat(PropertyName.Level, level);
		item.Properties.SetFloat(PropertyName.ItemExp, 0);
		item.Properties.SetString(PropertyName.ItemExpString, "0");
		item.Properties.SetFloat(PropertyName.StarIconNumber, level);
		item.Properties.SetString(PropertyName.StarIcon, level.ToString(CultureInfo.InvariantCulture));

		player.Inventory.Add(item, InventoryAddType.PickUp);
		ZoneServer.Instance.Database.SavePlayerData(player, player.Connection?.Account);

		sender.ServerMessage("Created card {0} level {1} for {2}.", itemId, level, player.TeamName);
		player.ServerMessage("{0} created card {1} level {2} in your inventory.", sender.TeamName, itemId, level);
		return CommandResult.Okay;
	}

	private CommandResult HandleSize(Character sender, Character target, string message, string commandName, Arguments args)
	{
		if (args.Count < 2)
			return CommandResult.InvalidArgument;

		if (!TryGetOnlinePlayer(args.Get(0), out var player))
		{
			sender.ServerMessage("Character not found.");
			return CommandResult.Okay;
		}

		if (!float.TryParse(args.Get(1), NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
			return CommandResult.InvalidArgument;

		if (scale < -1.0f || scale > 5.0f)
			return CommandResult.InvalidArgument;

		player.Properties.SetFloat(PropertyName.Scale, scale);
		Send.ZC_OBJECT_PROPERTY(player.Connection, player, PropertyName.Scale);

		var viewers = new List<Character>();
		player.Map.GetVisibleCharacters(player, viewers);
		foreach (var viewer in viewers)
			Send.ZC_OBJECT_PROPERTY(viewer.Connection, player, PropertyName.Scale);

		sender.ServerMessage("Size of {0} changed to {1}.", player.TeamName, scale.ToString("0.###", CultureInfo.InvariantCulture));
		if (sender != player)
			player.ServerMessage("Your size was changed by {0}.", sender.TeamName);

		return CommandResult.Okay;
	}

	private static bool TryGetOnlinePlayer(string playerName, out Character player)
	{
		player = ZoneServer.Instance.World.GetCharacter(a =>
			string.Equals(a.TeamName, playerName, System.StringComparison.InvariantCultureIgnoreCase) ||
			string.Equals(a.Name, playerName, System.StringComparison.InvariantCultureIgnoreCase)
		);

		return player != null;
	}

	private static bool IsAllowedCard(InventoryCategory category, ItemGroup group)
	{
		if (group == ItemGroup.Card)
			return true;

		return category == InventoryCategory.Card ||
			category == InventoryCategory.Card_CardBlue ||
			category == InventoryCategory.Card_CardRed ||
			category == InventoryCategory.Card_CardGreen ||
			category == InventoryCategory.Card_CardPurple ||
			category == InventoryCategory.Card_CardGoddess ||
			category == InventoryCategory.Card_CardLeg ||
			category == InventoryCategory.Ancient_Card;
	}

	private static string ToLuaString(string value)
	{
		var result = new StringBuilder();
		result.Append('"');

		foreach (var ch in value)
		{
			switch (ch)
			{
				case '\\':
					result.Append("\\\\");
					break;
				case '"':
					result.Append("\\\"");
					break;
				case '\r':
					result.Append("\\r");
					break;
				case '\n':
					result.Append("\\n");
					break;
				default:
					result.Append(ch);
					break;
			}
		}

		result.Append('"');
		return result.ToString();
	}

	private void SendPanelLua(Character character)
	{
		if (string.IsNullOrEmpty(_panelLua))
			return;

		Send.ZC_EXEC_CLIENT_SCP(character.Connection, "SSGM_PANEL_SOURCE = ''");

		for (var i = 0; i < _panelLua.Length; i += ClientChunkSize)
		{
			var chunkLength = System.Math.Min(ClientChunkSize, _panelLua.Length - i);
			var chunk = _panelLua.Substring(i, chunkLength);
			var script = "SSGM_PANEL_SOURCE = SSGM_PANEL_SOURCE .. " + ToLuaString(chunk);
			Send.ZC_EXEC_CLIENT_SCP(character.Connection, script);
		}

		Send.ZC_EXEC_CLIENT_SCP(character.Connection, "local s=SSGM_PANEL_SOURCE or ''; local f,err=load(s); if f ~= nil then local ok,runErr = pcall(f); if ok ~= true then ui.SysMsg(runErr or 'GM panel runtime failed.'); end else ui.SysMsg(err or 'GM panel load failed.'); end; SSGM_PANEL_SOURCE=nil");
	}
}
