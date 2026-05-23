//--- Melia Script ----------------------------------------------------------
// Miners' Village
//--- Description -----------------------------------------------------------
// NPCs found in and around Miners' Village.
//---------------------------------------------------------------------------

using Melia.Shared.Game.Const;
using Melia.Zone.Scripting;
using static Melia.Zone.Scripting.Shortcuts;

public class FSiauliaiOutNpcScript : GeneralScript
{
	protected override void Load()
	{
		// Statue of Goddess Vakarine
		//-------------------------------------------------------------------------
		AddNpc(5, 40120, "Statue of Goddess Vakarine", "f_siauliai_out", 190.5049, 42.7921, -1214.24, 0, "WARP_F_SIAULIAI_OUT", "STOUP_CAMP", "STOUP_CAMP");
				
		// Statue of Goddess Zemyna
		//-------------------------------------------------------------------------
		AddNpc(10031, 40110, "Statue of Goddess Zemyna", "f_siauliai_out", -2194, 40, -2055, 84, "F_SIAULIAI_OUT_EV_55_001", "F_SIAULIAI_OUT_EV_55_001", "F_SIAULIAI_OUT_EV_55_001");

		// Official Papaya main quest chain actors
		//-------------------------------------------------------------------------
		AddNpc(300, 20026, "", "f_siauliai_out", 506, 35, -1622, -90, "SIAULIAIOUT_Q01", "", "");
		AddNpc(312, 20118, "Miner's Village Chief", "f_siauliai_out", -87.647362, 145.231903, -802.089050, 0, "SIAULIAIOUT_CHIEF_A", "", "");
		AddNpc(313, 20110, "[Alchemist Master] Vaidotas", "f_siauliai_out", 1309.118652, 147.351593, 331.725952, -86, "SIAULIAIOUT_ALCHE", "", "");
		AddNpc(314, 20026, "", "f_siauliai_out", 1298, 147.351593, 307, 0, "SIAULIAIOUT_PREAL", "", "");
		AddNpc(321, 20110, "[Alchemist Master] Vaidotas", "f_siauliai_out", -38.880001, 85.269997, -1021.809998, 0, "SIAULIAIOUT_ALCHE_A", "", "");
		AddNpc(322, 40095, "", "f_siauliai_out", -61, 157, -656, 0, "SIAULIAIOUT_BLOCK", "", "");
		
		// Lv1 Treasure Chest
		//-------------------------------------------------------------------------
		AddNpc(10054, 147392, "Lv1 Treasure Chest", "f_siauliai_out", 1224.35, 198.02, 279.95, 90, "TREASUREBOX_LV_F_SIAULIAI_OUT10054", "", "");

		// Lv2 Treasure Chest
		//-------------------------------------------------------------------------
		 AddNpc(10023, 40030, "Lv2 Treasure Chest", "f_siauliai_out", 1451, 229, 577, 0, "TREASUREBOX_LV_F_SIAULIAI_210023", "", "");

		// Lv1 Treasure Chest (East Siauliai Woods Collection)
		// We're never going to have East Siauliai Woods, but having it in
		// Miner's Village looks weird. Will leave it commented out for time being.
		//-------------------------------------------------------------------------
		// AddNpc(10039, 147392, "Lv1 Treasure Chest", "f_siauliai_out", -1810, 170, -952, 90, "TREASUREBOX_LV_F_SIAULIAI_210039", "", "");
	}
}
