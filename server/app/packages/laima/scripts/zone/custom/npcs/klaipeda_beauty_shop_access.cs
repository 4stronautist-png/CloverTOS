using System;
using System.Threading.Tasks;
using Melia.Shared.Scripting;
using Melia.Shared.World;
using Melia.Zone;
using Melia.Zone.Events.Arguments;
using Melia.Zone.Network;
using Melia.Zone.Scripting;
using Melia.Zone.Scripting.Dialogues;
using Melia.Zone.World.Actors.Monsters;
using static Melia.Zone.Scripting.Shortcuts;

public class KlaipedaBeautyShopAccessScript : GeneralScript
{
	private const string KlaipedaMap = "c_Klaipe";
	private const string BeautyShopMap = "c_barber_dress";

	private WarpMonster _entranceWarp;
	private bool _disposed;

	protected override void Load()
	{
		AddNpc(40070, "Placa", "KLAIPEDA_TAVERN_HOURS_SIGN", KlaipedaMap, -987.869, 233.9606, -90, TavernHoursSignDialog);
		AddNpc(40070, "Placa", "KLAIPEDA_BEAUTY_HOURS_SIGN", KlaipedaMap, -1086.855, 624.9548, 90, BeautyHoursSignDialog);

		UpdateEntranceWarp();
		_ = RunScheduleLoop();
	}

	public override void Dispose()
	{
		_disposed = true;
		base.Dispose();
	}

	private async Task TavernHoursSignDialog(Dialog dialog)
	{
		dialog.SetTitle(IsPortuguese(dialog) ? "Placa" : "Sign");
		await dialog.Msg(IsPortuguese(dialog)
			? "Horário de abertura: 18h. Fechamos às 6h da manhã."
			: "Opening hours: 6:00 PM. We close at 6:00 AM.");
	}

	private async Task BeautyHoursSignDialog(Dialog dialog)
	{
		dialog.SetTitle(IsPortuguese(dialog) ? "Placa" : "Sign");
		await dialog.Msg(IsPortuguese(dialog)
			? "Horário de abertura: 8h da manhã. Fechamos às 21h."
			: "Opening hours: 8:00 AM. We close at 9:00 PM.");
	}

	[On("PlayerLeftMap")]
	private void OnPlayerLeftMap(object sender, PlayerEventArgs args)
	{
		var character = args.Character;
		if (character?.Map?.ClassName != BeautyShopMap || character.Connection == null)
			return;

		PacketHandler.ResetBeautyShopTryOn(character.Connection, character);
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
			_entranceWarp = AddWarp(10063, "KLAPEDA_TO_BEAUTYSHOP", 225,
				From(KlaipedaMap, -1013.956, 240.7917, 655.087),
				To(BeautyShopMap, -8.57293, 4.81811, -90.51904));
		}
		else if (!open && _entranceWarp != null)
		{
			_entranceWarp.Map?.RemoveMonster(_entranceWarp);
			_entranceWarp = null;
		}
	}

	private static bool IsOpenTime()
	{
		var hour = GameTime.Now.Hour;
		return hour >= 8 && hour < 21;
	}

	private static bool IsPortuguese(Dialog dialog)
		=> string.Equals(dialog.Player.Connection?.SelectedLanguage, "Portuguese", StringComparison.OrdinalIgnoreCase);
}
