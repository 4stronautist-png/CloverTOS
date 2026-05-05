//--- SoulSociety Script ----------------------------------------------------
// Walk Toggle
//--- Description -----------------------------------------------------------
// Adds a minimap button and hotkey command that locks the player to walking.
//---------------------------------------------------------------------------

using Melia.Shared.Game.Const;
using Melia.Zone;
using Melia.Zone.Commands;
using Melia.Zone.Network;
using Melia.Zone.Scripting;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Yggdrasil.Util.Commands;
using static Melia.Zone.Scripting.Shortcuts;

public class WalkToggleClientScript : ClientScript
{
	private const float WalkSpeed = 18f;
	private const string WalkVar = "SoulSociety.Walk.Enabled";

	protected override void Load()
	{
		this.LoadAllScripts();
		AddChatCommand("walk", "<on|off|toggle|status>", "Toggles walk mode.", 0, 99, HandleWalk);
	}

	protected override void Ready(Character character)
	{
		this.SendLuaScript(character, "001.lua");
		this.ApplyState(character);
		this.SendState(character);
	}

	private CommandResult HandleWalk(Character sender, Character target, string message, string commandName, Arguments args)
	{
		var action = args.Count > 0 ? args.Get(0).ToLowerInvariant() : "toggle";
		var enabled = sender.Variables.Temp.GetBool(WalkVar, false);

		switch (action)
		{
			case "on":
				enabled = true;
				break;
			case "off":
				enabled = false;
				break;
			case "toggle":
				enabled = !enabled;
				break;
			case "status":
				break;
			default:
				return CommandResult.InvalidArgument;
		}

		sender.Variables.Temp.SetBool(WalkVar, enabled);
		this.ApplyState(sender);
		this.SendState(sender);
		return CommandResult.Okay;
	}

	private void ApplyState(Character character)
	{
		var enabled = character.Variables.Temp.GetBool(WalkVar, false);
		if (enabled)
		{
			character.StopBuff(BuffId.DashRun);
			character.Movement.SetFixedMoveSpeed(WalkSpeed);
		}
		else
		{
			character.Movement.ResetFixedMoveSpeed();
		}
	}

	private void SendState(Character character)
	{
		var enabled = character.Variables.Temp.GetBool(WalkVar, false);
		Send.ZC_EXEC_CLIENT_SCP(character.Connection, $"SS_WALK_SYNC({(enabled ? "true" : "false")})");
	}
}
