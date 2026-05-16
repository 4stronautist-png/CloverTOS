//--- Melia Script ----------------------------------------------------------
// Warps
//--- Description -----------------------------------------------------------
// Sets up warps in West Siauliai Woods
//---------------------------------------------------------------------------

using System.Threading.Tasks;
using Melia.Zone.Scripting;
using Melia.Zone.World.Actors.Characters;
using static Melia.Zone.Scripting.Shortcuts;

public class f_siauliai_westWarpsScript : GeneralScript
{
	protected override void Load()
	{
		// West Siauliai Woods to Klaipeda
		AddWarp(28, "WS_SIAULST1_KLAPEDA", 68, From("f_siauliai_west", 1691, -755), To("c_Klaipe", -181, -1123));
		AddAreaTrigger("f_siauliai_west", 1691, -755, 70, async args =>
		{
			if (args.Initiator is Character character && CanUseKlaipedaRoad(character))
				character.Warp("c_Klaipe", -181, 0, -1123);

			await Task.CompletedTask;
		});

		// West Siauliai Woods to West Siauliai Woods
		AddWarp(2031, "TO_SIAULIAI_WEST", -24, From("f_siauliai_west", 2755.275, 443.1412), To("f_siauliai_west", 1412, -362));

		// West Siauliai Woods to West Siauliai Woods
		AddNpc(147501, "Teleporter", "f_siauliai_west", 1451, -341, 45, async dialog =>
		{
			dialog.Player.Warp("f_siauliai_west", 2769, 423, 521);
		});
	}

	private static bool CanUseKlaipedaRoad(Character character)
	{
		return character.Quests.HasCompleted(1019) ||
			character.Quests.IsActive(1027) ||
			character.Quests.HasCompleted(1027) ||
			character.Quests.IsActive(20236) ||
			character.Quests.HasCompleted(20236) ||
			character.Quests.IsActive(40010) ||
			character.Quests.HasCompleted(40010);
	}
}
