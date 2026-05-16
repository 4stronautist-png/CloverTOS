//--- Melia Script ----------------------------------------------------------
// East Siauliai Woods
//--- Description -----------------------------------------------------------
// NPCs found in and around East Siauliai Woods.
//---------------------------------------------------------------------------

using Melia.Shared.Game.Const;
using Melia.Zone.Scripting;
using static Melia.Zone.Scripting.Shortcuts;

public class FSiauliai2NpcScript : GeneralScript
{
	protected override void Load()
	{
		// Knight Aras
		//-------------------------------------------------------------------------
		AddNpc(2, 20125, L("Knight Aras"), "f_siauliai_2", 167, 151, 697, -90, "SIAUL_EAST_MANAGER", "", "");

		// Operations Officer
		//-------------------------------------------------------------------------
		AddNpc(30, 20014, L("Operations Officer"), "f_siauliai_2", -1290, 130, 928, -90, "SIAUL_EAST_SUPPLY_MANAGER2", "", "");

		// Search Scout
		//-------------------------------------------------------------------------
		AddNpc(50, 10032, L("Search Scout"), "f_siauliai_2", 1242, 130, 339, -90, "SIAUL_EAST_SOLDIER8", "", "");

		// Statue of Goddess Vakarine
		//-------------------------------------------------------------------------
		AddNpc(33, 40120, "Statue of Goddess Vakarine", "f_siauliai_2", 233, 157, 724, 0, "WARP_F_SIAULIAI_EST", "STOUP_CAMP", "STOUP_CAMP");
		
		// Lv2 Treasure Chest
		//-------------------------------------------------------------------------
		// AddNpc(10023, 40030, "Lv2 Treasure Chest", "f_siauliai_2", 49.24, 130.12, -963.25, 90, "TREASUREBOX_LV_F_SIAULIAI_210023", "", "");
		
		// Lv1 Treasure Chest
		//-------------------------------------------------------------------------
		// AddNpc(10039, 147392, "Lv1 Treasure Chest", "f_siauliai_2", -2235.57, 130.12, 690.81, 90, "TREASUREBOX_LV_F_SIAULIAI_210039", "", "");
	}
}
