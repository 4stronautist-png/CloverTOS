//--- Melia Script ----------------------------------------------------------
// Crystal Mine 3F
//--- Description -----------------------------------------------------------
// NPCs found in and around Crystal Mine 3F.
//---------------------------------------------------------------------------

using Melia.Shared.Game.Const;
using Melia.Zone.Scripting;
using static Melia.Zone.Scripting.Shortcuts;

public class DCmine6NpcScript : GeneralScript
{
	protected override void Load()
	{
		// Statue of Goddess Vakarine
		//-------------------------------------------------------------------------
		AddNpc(525, 40120, "Statue of Goddess Vakarine", "d_cmine_6", -2175.529, 360.2849, -1773.89, 90, "WARP_D_CMINE_6", "STOUP_CAMP", "STOUP_CAMP");
		
		// Lv1 Treasure Chest
		//-------------------------------------------------------------------------
		AddNpc(530, 147392, "Lv1 Treasure Chest", "d_cmine_6", -1145.18, 303.59, 103.15, 0, "TREASUREBOX_LV_D_CMINE_6530", "", "");
		
		// Lv1 Treasure Chest
		//-------------------------------------------------------------------------
		AddNpc(540, 147392, "Lv1 Treasure Chest", "d_cmine_6", -874.95, 184.05, -970.45, 90, "TREASUREBOX_LV_D_CMINE_6540", "", "");

		// Papaya main quest actors: Rescue the Villagers -> Mysterious Slate
		//-------------------------------------------------------------------------
		AddNpc(600, 20110, "[Alchemist Master]\nVaidotas", "d_cmine_6", -250, 65, -1320, 0, "MINE_3_ALCHEMIST", "", "");
		AddNpc(601, 151009, "Trapped Miner", "d_cmine_6", 820, 65, -620, 0, "MINE_3_RESIENT1_BIND", "", "");
		AddNpc(602, 20150, "Miner", "d_cmine_6", 860, 65, -620, 0, "MINE_3_RESIENT1", "", "");
		AddNpc(603, 47233, "", "d_cmine_6", 2070, 63, 1580, 0, "CMINE6_TO_KATYN7_1_START", "", "");
		AddNpc(604, 12080, "", "d_cmine_6", 2050, 63, 1600, 0, "CMINE3_BOSSROOM_OPEN", "", "");
	}
}
