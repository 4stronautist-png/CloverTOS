//--- Melia Script ----------------------------------------------------------
// Nefritas Cliff
//--- Description -----------------------------------------------------------
// NPCs found in and around Nefritas Cliff.
//---------------------------------------------------------------------------

using Melia.Shared.Game.Const;
using Melia.Zone.Scripting;
using static Melia.Zone.Scripting.Shortcuts;

public class FGele573NpcScript : GeneralScript
{
	protected override void Load()
	{
		// Statue of Goddess Vakarine
		//-------------------------------------------------------------------------
		AddNpc(25, 40120, "Statue of Goddess Vakarine", "f_gele_57_3", -407.211, -107.0825, -1328.491, 15, "WARP_F_GELE_57_3", "STOUP_CAMP", "STOUP_CAMP");
		
		// Lv1 Treasure Chest
		//-------------------------------------------------------------------------
		AddNpc(40, 147392, "Lv1 Treasure Chest", "f_gele_57_3", 1150.08, 217.61, 817.83, 90, "TREASUREBOX_LV_F_GELE_57_340", "", "");

		// Papaya main quest actors: Foreseen Crisis
		//-------------------------------------------------------------------------
		AddNpc(70, 57223, "Paladin Master", "f_gele_57_3", 871, -68, -514, 0, "GELE573_MASTER", "", "");
		AddNpc(71, 147390, "Paladin Follower", "f_gele_57_3", 805, -68, -450, 0, "GELE573_MQ_07_F", "", "");
	}
}
