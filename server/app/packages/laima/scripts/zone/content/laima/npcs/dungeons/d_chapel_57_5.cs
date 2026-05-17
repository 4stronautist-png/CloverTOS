//--- Melia Script ----------------------------------------------------------
// Tenet Church B1
//--- Description -----------------------------------------------------------
// NPCs found in and around Tenet Church B1.
//---------------------------------------------------------------------------

using Melia.Shared.Game.Const;
using Melia.Zone.Scripting;
using static Melia.Zone.Scripting.Shortcuts;

public class DChapel575NpcScript : GeneralScript
{
	protected override void Load()
	{
		// Statue of Goddess Vakarine
		//-------------------------------------------------------------------------
		AddNpc(24, 40120, "Statue of Goddess Vakarine", "d_chapel_57_5", -1429.68, 0.55, 1033.58, 76, "WARP_D_CHAPEL_57_5", "STOUP_CAMP", "STOUP_CAMP");
		
		// Lv1 Treasure Chest
		//-------------------------------------------------------------------------
		AddNpc(33, 147392, "Lv1 Treasure Chest", "d_chapel_57_5", 814.34, 0.65, -988.18, 90, "TREASUREBOX_LV_D_CHAPEL_57_533", "", "");

		// Papaya main quest actors: Church Underground Passage
		//-------------------------------------------------------------------------
		AddNpc(70, 147390, "Paladin Follower", "d_chapel_57_5", -1258, 1, 1095, 0, "CHAPEL_TOMAS", "", "");
		AddNpc(71, 147353, "", "d_chapel_57_5", 300, 1, -300, 0, "CHAPLE575_MQ_04", "", "");
		AddNpc(72, 147390, "Paladin Follower", "d_chapel_57_5", -1120, 1, 980, 0, "CHAPEL_VIDAS", "", "");
		AddNpc(73, 147353, "", "d_chapel_57_5", 500, 1, -500, 0, "CHAPLE575_MQ_09", "", "");
	}
}
