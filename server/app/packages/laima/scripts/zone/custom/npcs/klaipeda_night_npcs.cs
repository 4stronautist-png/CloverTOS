using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Melia.Shared.Game.Const;
using Melia.Shared.World;
using Melia.Zone;
using Melia.Zone.Scripting;
using Melia.Zone.Scripting.Dialogues;
using Melia.Zone.World.Actors.Monsters;
using static Melia.Zone.Scripting.Shortcuts;

public class KlaipedaNightNpcScript : GeneralScript
{
	private const string KlaipedaMap = "c_Klaipe";
	private const string TavernMap = "c_request_1";

	private readonly List<Npc> _dayNpcs = new();
	private readonly List<Npc> _nightNpcs = new();
	private bool _initialized;
	private bool _isNight;
	private bool _disposed;

	protected override void Load()
	{
		UpdateNpcs();
		_ = RunScheduleLoop();
	}

	public override void Dispose()
	{
		_disposed = true;
		base.Dispose();
	}

	private async Task RunScheduleLoop()
	{
		while (!_disposed)
		{
			UpdateNpcs();
			await Task.Delay(TimeSpan.FromSeconds(10));
		}
	}

	private void UpdateNpcs()
	{
		var night = IsNightTime();
		if (_initialized && night == _isNight)
			return;

		if (night)
		{
			RemoveNpcs(_dayNpcs);
			AddNightNpcs();
		}
		else
		{
			RemoveNpcs(_nightNpcs);
			AddDayNpcs();
		}

		_isNight = night;
		_initialized = true;
	}

	private static bool IsNightTime()
	{
		var hour = GameTime.Now.Hour;
		return hour >= 18 || hour < 6;
	}

	private static void RemoveNpcs(List<Npc> npcs)
	{
		foreach (var npc in npcs)
			npc.Map?.RemoveMonster(npc);

		npcs.Clear();
	}

	private void AddDayNpcs()
	{
		if (_dayNpcs.Count != 0)
			return;

		_dayNpcs.Add(AddNpc(4, 20113, "Uska", KlaipedaMap, -474.0, 149.0, 82.0, 90.0, "KLAPEDA_USKA", "", "", -2, 35.0));
		_dayNpcs.Add(AddNpc(8, 20114, "Dona Alzira", KlaipedaMap, -60.0, 148.0, 42.0, -8.43, "ACT_SMOM", "", "", -2, 40.0));
		_dayNpcs.Add(AddNpc(11, 20114, "Dona Matilde", KlaipedaMap, -409.0, -1.0, -647.0, -92.290001, "TUTO_GIRL", "", "", -2, 0.0));
		_dayNpcs.Add(AddNpc(108, 147445, "Quarrel Shooter Master", KlaipedaMap, -236.0, 241.0, 867.0, 0.0, "MASTER_QU", "", "", -2, 20.0));
		_dayNpcs.Add(AddNpc(109, 147343, "Ranger Master", KlaipedaMap, -488.779999, 148.610001, 27.0, 90.0, "MASTER_RANGER", "", "", -2, 20.0));
		_dayNpcs.Add(AddNpc(110, 20023, "Swordsman Master", KlaipedaMap, -92.0, 241.0, 784.0, 0.0, "MASTER_SWORDMAN", "", "", -2, 20.0));
		_dayNpcs.Add(AddNpc(113, 20112, "Cleric Master", KlaipedaMap, -409.0, 149.0, 174.0, 45.0, "MASTER_CLERIC", "", "", -2, 20.0));
		_dayNpcs.Add(AddNpc(114, 57229, "Priest Master", KlaipedaMap, -196.470001, 148.830002, 350.730011, 0.0, "MASTER_PRIEST", "", "", -2, 20.0));
		_dayNpcs.Add(AddNpc(10006, 147473, "Mira", KlaipedaMap, 615.22998, -1.35, 132.440002, 0.0, "HUEVILLAGE_58_3_KLAIPEDA_NPC", "", "", -2, 40.0));
		_dayNpcs.Add(AddNpc(10020, 147473, "Selene", KlaipedaMap, -605.338135, -1.34, -479.097412, -93.0, "LOWLV_GREEN_SELPHUI", "", "", -2, 40.0));
		_dayNpcs.Add(AddNpc(10025, 147517, "Fishing Manager", KlaipedaMap, -616.0, 241.0, 723.0, 45.0, "KLAPEDA_FISHING_MANAGER", "", "", -2, 40.0));
		_dayNpcs.Add(AddNpc(10029, 20148, "Dona Amelina", KlaipedaMap, 519.843567, -1.156548, 326.143768, 0.0, "CHAR119_MSTEP3_4_NPC", "", "", -2, 40.0));
	}

	private void AddNightNpcs()
	{
		if (_nightNpcs.Count != 0)
			return;

		var swordsman = AddNpc(20023, L("Swordsman Master"), TavernMap, -24.84385, -161.0761, 90, SwordsmanMasterNightDialog);
		swordsman.Properties.SetFloat(PropertyName.Range, 80f);
		_nightNpcs.Add(swordsman);

		var worker = AddNpc(20148, L("Tavern Worker"), TavernMap, 43.99628, 128.6694, 180, TavernWorkerDialog);
		worker.Properties.SetFloat(PropertyName.Range, 70f);
		_nightNpcs.Add(worker);

		var broker = AddNpc(151038, L("[Night Contract Broker] Elara"), TavernMap, -100.1454, -92.34874, 90, ElaraDialog);
		broker.Properties.SetFloat(PropertyName.Range, 85f);
		_nightNpcs.Add(broker);

		var brokerCounter = AddNpc(47274, "", "ELARA_COUNTER_SERVICE", TavernMap, -58.91764, -100.1039, 270, ElaraDialog);
		brokerCounter.Properties.SetFloat(PropertyName.Range, 90f);
		_nightNpcs.Add(brokerCounter);

		var merchant = AddNpc(151035, L("Veryon"), TavernMap, 165.2461, 31.44116, 0, NightMerchantDialog);
		merchant.Properties.SetFloat(PropertyName.Range, 80f);
		_nightNpcs.Add(merchant);
	}

	private async Task SwordsmanMasterNightDialog(Dialog dialog)
	{
		dialog.SetTitle(L("Swordsman Master"));
		dialog.SetPortrait("Dlg_port_SWORDMAN_MASTER");

		await dialog.Msg(IsPortuguese(dialog)
			? "Hic... achei que depois de me aposentar eu teria uma folga. Mas n\u00e3o. De dia em p\u00e9, de noite no balc\u00e3o, e ainda dizem que mestre n\u00e3o trabalha..."
			: "Hic... I thought retirement meant I would finally get a break. But no. Standing all day, leaning on counters all night, and people still say masters don't work...");
	}

	private async Task TavernWorkerDialog(Dialog dialog)
	{
		try
		{
			dialog.SetTitle(IsPortuguese(dialog) ? "Trabalhadora da Taverna" : "Tavern Worker");
			dialog.SetPortrait("Dlg_port_fedimian_oldlady");

			await dialog.Msg(IsPortuguese(dialog)
				? "Sem tempo para conversa, querido. A noite est\u00e1 cheia, as canecas n\u00e3o se lavam sozinhas e Brannock j\u00e1 perdeu a conta de quantos barris abriu."
				: "No time to chat, dear. The night is busy, the mugs won't wash themselves, and Brannock has already lost count of the barrels he opened.");
		}
		finally
		{
			if (dialog.Npc != null)
				dialog.Npc.Direction = new Direction(180);
		}
	}

	private async Task ElaraDialog(Dialog dialog)
	{
		dialog.SetTitle(L("Elara"));
		dialog.SetPortrait("Dlg_port_Yoana");

		var selection = await dialog.Select(IsPortuguese(dialog) ? "Em que posso ajudar?" : "How may I help you?",
			Option(IsPortuguese(dialog) ? "H\u00e1 algum contrato dispon\u00edvel?" : "Do you have any contracts available?", "contracts"),
			Option(IsPortuguese(dialog) ? "Quem \u00e9 voc\u00ea?" : "Who are you?", "who"),
			Option(IsPortuguese(dialog) ? "Sair" : "Leave", "leave"));

		if (selection == "contracts")
		{
			await dialog.Msg(IsPortuguese(dialog)
				? "A noite est\u00e1 quieta, viajante. N\u00e3o tenho contratos dispon\u00edveis no momento. Volte mais tarde; talvez algu\u00e9m deixe um pedido digno da sua l\u00e2mina."
				: "The night is quiet, traveler. I have no contracts available at the moment. Come back later, and perhaps someone will have left a request worth your blade.");
		}
		else if (selection == "who")
		{
			await dialog.Msg(IsPortuguese(dialog)
				? "Sou respons\u00e1vel pelas miss\u00f5es mercen\u00e1rias autorizadas por Uska. Entrego contratos espec\u00edficos, e quem os concluir dentro do prazo recebe uma recompensa generosa."
				: "I handle the mercenary missions authorized by Uska. I deliver specific contracts, and whoever completes one in time earns a generous reward.");
		}
	}

	private async Task NightMerchantDialog(Dialog dialog)
	{
		dialog.SetTitle(L("Veryon"));
		dialog.SetPortrait("Dlg_port_Yorgis");

		await dialog.Select(IsPortuguese(dialog)
			? "Aparentemente o andar de cima est\u00e1 cheio. Te aviso quando desocupar uma mesa."
			: "It seems the upstairs floor is full. I'll let you know when a table opens up.",
			Option(IsPortuguese(dialog) ? "Sair" : "Leave", "leave"));
	}

	private static bool IsPortuguese(Dialog dialog)
		=> string.Equals(dialog.Player.Connection?.SelectedLanguage, "Portuguese", StringComparison.OrdinalIgnoreCase);
}
