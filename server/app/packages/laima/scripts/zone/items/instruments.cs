using System;
using Melia.Shared.Game.Const;
using Melia.Zone.Network;
using Melia.Zone.Scripting;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.Components;
using Melia.Zone.World.Items;

public class InstrumentItemScripts : GeneralScript
{
	private const string ActiveInstrumentTypeVar = "Melia.Instrument.Type";

	[ScriptableFunction]
	public ItemUseResult SCR_USE_ITEM_PLAY_SELECT_TOY_INSTRUMENT(Character character, Item item, string strArg, float numArg1, float numArg2)
	{
		if (string.IsNullOrWhiteSpace(strArg))
			return ItemUseResult.Fail;

		if (string.IsNullOrWhiteSpace(character.Variables.Temp.GetString(ActiveInstrumentTypeVar, "")))
			character.Lock(LockType.Movement);

		character.Variables.Temp.SetString(ActiveInstrumentTypeVar, strArg);
		character.StartBuff(BuffId.Instrument_Use_Buff, TimeSpan.Zero, character);

		Send.ZC_READY_INSTRUMENT(character, strArg, true);
		Send.ZC_ADDON_MSG(character, "INSTRUMENT_KEYBOARD_OPEN", 0, strArg);

		return ItemUseResult.OkayNotConsumed;
	}
}
