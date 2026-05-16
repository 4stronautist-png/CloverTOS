//--- Melia Script ----------------------------------------------------------
// Veja Ravine
//--- Description -----------------------------------------------------------
// NPCs found in and around Veja Ravine.
//---------------------------------------------------------------------------

using Melia.Shared.Game.Const;
using Melia.Zone.Scripting;
using static Melia.Zone.Scripting.Shortcuts;

public class FHuevillage581NpcScript : GeneralScript
{
	protected override void Load()
	{
		// Statue of Goddess Vakarine
		//-------------------------------------------------------------------------
		AddNpc(33, 40120, "Statue of Goddess Vakarine", "f_huevillage_58_1", 217.9083, 371.3148, -916.1648, 79, "WARP_F_HUEVILLAGE_58_1", "STOUP_CAMP", "STOUP_CAMP");
		
		// Track NPCs
		//---------------------------------------------------------------------------
		AddTrackNPC(153008, "", "f_huevillage_58_1", 638.2, 129.74, 468.58, 0, "f_huevillage58_1_cablecar", 2, 5);


		// Lv1 Treasure Chest
		//-------------------------------------------------------------------------
		AddNpc(50, 147392, "Lv1 Treasure Chest", "f_huevillage_58_1", -315.60, 371.41, -1374.85, 90, "TREASUREBOX_LV_F_HUEVILLAGE_58_150", "", "");

		// Papaya main quest actors: Veja Ravine / Saule handoff
		//-------------------------------------------------------------------------
		AddNpc(100, 147390, "Villager", "f_huevillage_58_1", 975, 97, 951, 0, "HUEVILLAGE_58_1_MQ01_NPC", "", "");
		AddNpc(101, 147390, "Villager", "f_huevillage_58_1", -988, 231, 1036, 0, "HUEVILLAGE_58_1_MQ02_NPC", "", "");
		AddNpc(102, 147390, "Villager", "f_huevillage_58_1", -720, 231, 860, 0, "HUEVILLAGE_58_1_MQ03_NPC", "", "");
		AddNpc(103, 20041, "", "f_huevillage_58_1", -640, 231, 820, 0, "HUEVILLAGE_58_1_PORTAL", "", "");
		AddNpc(104, 20041, "", "f_huevillage_58_1", 975, 97, 951, 0, "HUEVILLAGE_58_1_MQ11_TRIGGER", "", "");
		AddNpc(105, 20041, "", "f_huevillage_58_1", 975, 97, 951, 0, "HUEVILLAGE_58_3_MQ04_TO_HUE1", "", "");
	}
}
