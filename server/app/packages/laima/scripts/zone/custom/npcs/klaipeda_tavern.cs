using System;
using Melia.Shared.Game.Const;
using System.Threading.Tasks;
using Melia.Shared.Scripting;
using Melia.Shared.World;
using Melia.Zone;
using Melia.Zone.Events.Arguments;
using Melia.Zone.Scripting;
using Melia.Zone.Scripting.Dialogues;
using Melia.Zone.World.Actors.Monsters;
using static Melia.Zone.Scripting.Shortcuts;

public class KlaipedaTavernScript : GeneralScript
{
	private const string TavernMap = "c_request_1";
	private const string KlaipedaMap = "c_Klaipe";
	private const string TavernShop = "Brannock_Tavern";

	private WarpMonster _entranceWarp;
	private bool _wasOpen;
	private bool _disposed;

	protected override void Load()
	{
		CreateShop(TavernShop, shop =>
		{
			shop.AddItem(721024, 1, 40000);
			shop.AddItem(10003048, 1, 30000);
			shop.AddItem(666310, 1, 20000);
		});

		var brannock = AddNpc(152002, L("Brannock, the Tavernkeeper"), TavernMap, -14.6287, 38.10761, 0, BrannockDialog);
		brannock.Properties.SetFloat(PropertyName.Range, 260f);
		var counterService = AddNpc(47274, "", "BRANNOCK_COUNTER_SERVICE", TavernMap, -8.83877, -6.11647, 180, BrannockTavernkeeperDialog);
		counterService.Properties.SetFloat(PropertyName.Range, 90f);

		UpdateEntranceWarp();
		_ = RunScheduleLoop();
	}

	public override void Dispose()
	{
		_disposed = true;
		base.Dispose();
	}

	private async Task BrannockDialog(Dialog dialog)
	{
		if (dialog.Player.Position.Z > 10)
		{
			await BrannockWrongSideDialog(dialog);
			return;
		}

		await BrannockTavernkeeperDialog(dialog);
	}

	private async Task BrannockTavernkeeperDialog(Dialog dialog)
	{
		dialog.SetTitle(L("Brannock, the Tavernkeeper"));
		dialog.SetPortrait("Dlg_port_alchemist_2");

		await dialog.Msg(L("Welcome to my humble tavern."));
		var selection = await dialog.Select(L("What can I do for you?"),
			Option(L("Buy"), "buy"),
			Option(L("Leave"), "exit"));

		if (selection == "buy")
			await dialog.OpenShop(TavernShop);
	}

	private async Task BrannockWrongSideDialog(Dialog dialog)
	{
		dialog.SetTitle(L("Brannock, the Tavernkeeper"));
		dialog.SetPortrait("Dlg_port_alchemist_2");

		await dialog.Msg(IsPortuguese(dialog)
			? "Ei, ei! Sai desse lado do balcão, criatura. Eu só atendo do outro lado... e não derruba meus barris, pela Laima."
			: "Oi, oi! Out from behind my counter. I only serve from the other side... and don't knock over my barrels, for Laima's sake.");
	}

	private static bool IsPortuguese(Dialog dialog)
		=> string.Equals(dialog.Player.Connection?.SelectedLanguage, "Portuguese", StringComparison.OrdinalIgnoreCase);

	[On("PlayerEnteredMap")]
	private void OnPlayerEnteredMap(object sender, PlayerEventArgs args)
	{
		var character = args.Character;
		if (character?.Map?.ClassName != TavernMap)
			return;

		if (!IsOpenTime() && !IsGameMaster(character.Connection?.Account?.Authority ?? 0))
			WarpToTavernEntrance(character);
	}

	private async Task RunScheduleLoop()
	{
		while (!_disposed)
		{
			UpdateEntranceWarp();
			await Task.Delay(TimeSpan.FromSeconds(10));
		}
	}

	private void UpdateEntranceWarp()
	{
		var open = IsOpenTime();

		if (open && _entranceWarp == null)
		{
			_entranceWarp = AddWarp(10064, "KLAPEDA_TAVERN", 270,
				From(KlaipedaMap, -1118.001, 240.8842, 286.1519),
				To(TavernMap, 152.6733, 0.34982, -133.8388));
		}
		else if (!open && _entranceWarp != null)
		{
			_entranceWarp.Map?.RemoveMonster(_entranceWarp);
			_entranceWarp = null;
		}

		if (_wasOpen && !open)
			ExpelGuests();

		_wasOpen = open;
	}

	private static bool IsOpenTime()
	{
		var hour = GameTime.Now.Hour;
		return hour >= 18 || hour < 6;
	}

	private static bool IsGameMaster(int authority)
		=> authority >= 99;

	private static void ExpelGuests()
	{
		if (!ZoneServer.Instance.World.TryGetMap(TavernMap, out var map))
			return;

		foreach (var character in map.GetCharacters())
		{
			if (IsGameMaster(character.Connection?.Account?.Authority ?? 0))
				continue;

			WarpToTavernEntrance(character);
		}
	}

	private static void WarpToTavernEntrance(Melia.Zone.World.Actors.Characters.Character character)
	{
		character.Warp(KlaipedaMap, new Position(-1037.083f, 248.538f, 265.6501f));
	}
}
