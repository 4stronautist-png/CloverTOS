//--- Melia Script ----------------------------------------------------------
// Warps
//--- Description -----------------------------------------------------------
// Sets up warps in Klaipeda Tavern
//---------------------------------------------------------------------------

using Melia.Zone.Scripting;
using static Melia.Zone.Scripting.Shortcuts;

public class c_request_1WarpsScript : GeneralScript
{
	protected override void Load()
	{
		// Klaipeda Tavern to Klaipeda
		AddWarp("REQUEST1_KLAIPEDA", 90, From("c_request_1", 195, -130), To("c_Klaipe", -1037.083, 248.538, 265.6501));
	}
}
