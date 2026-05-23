//--- Melia Script ----------------------------------------------------------
// Tenet Church 1F
//--- Description -----------------------------------------------------------
// NPCs found in and around Tenet Church 1F.
//---------------------------------------------------------------------------

using Melia.Shared.Game.Const;
using Melia.Zone.Scripting;
using static Melia.Zone.Scripting.Shortcuts;

public class DChapel576NpcScript : GeneralScript
{
	protected override void Load()
	{
		// Statue of Goddess Vakarine
		//-------------------------------------------------------------------------
		AddNpc(18, 40120, "Statue of Goddess Vakarine", "d_chapel_57_6", 1322.36, 0.16, 435.09, 270, "WARP_D_CHAPEL_57_6", "STOUP_CAMP", "STOUP_CAMP");
		
		// Lv1 Treasure Chest
		//-------------------------------------------------------------------------
		AddNpc(30, 147392, "Lv1 Treasure Chest", "d_chapel_57_6", -660.85, 0.59, 117.06, 90, "TREASUREBOX_LV_D_CHAPEL_57_630", "", "");

		// Papaya main quest actors: Church Gate
		//-------------------------------------------------------------------------
		AddNpc(70, 147390, "Paladin Follower", "d_chapel_57_6", 746, 0, -251, 0, "CHAPEL_VIRGINIJA", "", "");
		AddNpc(71, 147353, "", "d_chapel_57_6", 298, -78, -110, 0, "CHAPLE576_MQ_04", "", "");
		AddNpc(72, 147390, "Paladin Follower", "d_chapel_57_6", -536, 0, 1563, 0, "CHAPEL576_DONATAS", "", "");
		AddNpc(73, 147353, "", "d_chapel_57_6", 259, 0, 427, 0, "CHAPLE576_MQ_09", "", "");
	}
}
