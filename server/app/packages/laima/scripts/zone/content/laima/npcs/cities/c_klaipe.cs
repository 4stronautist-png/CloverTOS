using Melia.Shared.Game.Const;
using Melia.Zone.Scripting;
using static Melia.Zone.Scripting.Shortcuts;

public class CKlaipeNpcScript : GeneralScript
{
	protected override void Load()
	{
		AddNpc(154018, L("[Storage Keeper] Rita"), "Rita", "c_Klaipe", 317, 279, 90.0, async dialog =>
		{
			dialog.SetTitle(L("Rita"));
			dialog.SetPortrait("WAREHOUSE_DLG");

			var response = await dialog.Select(L("Hello! Can I help you store your items?"),
				Option(L("Personal Storage"), "personal"),
				Option(L("Team Storage"), "team"),
				Option(L("Save Spawn Location"), "savelocation"),
				Option(L("Cancel"), "cancel")
			);

			if (response == "personal")
				await dialog.OpenPersonalStorage();
			else if (response == "team")
				await dialog.OpenTeamStorage();
			else if (response == "savelocation")
			{
				await dialog.SaveLocation();
				await dialog.Msg(L("Your location has been saved!"));
			}
		});
	}
}
