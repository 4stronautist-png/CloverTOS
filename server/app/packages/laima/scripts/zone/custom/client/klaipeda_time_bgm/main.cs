using System;
using System.Threading.Tasks;
using Melia.Shared.Scripting;
using Melia.Zone;
using Melia.Zone.Events.Arguments;
using Melia.Zone.Scripting;
using Melia.Zone.World.Actors.Characters;

public class KlaipedaTimeBgmClientScript : ClientScript
{
	private const string KlaipedaMap = "c_Klaipe";
	private const string TavernMap = "c_request_1";

	protected override void Load()
	{
		this.LoadAllScripts();

		ZoneServer.Instance.IesMods.Add("Map", 1001, "BgmPlayList", "c_Klaipe");
		ZoneServer.Instance.IesMods.Add("Map", 1005, "BgmPlayList", TavernMap);
	}

	protected override void Ready(Character character)
	{
		this.SendLuaScript(character, "001.lua");
		this.ForceMapBgm(character);
	}

	[On("PlayerLoadComplete")]
	private void OnPlayerLoadComplete(object sender, PlayerEventArgs args)
	{
		this.ForceMapBgm(args.Character);
	}

	private void ForceMapBgm(Character character)
	{
		var mapName = character?.Map?.ClassName;
		if (mapName != KlaipedaMap && mapName != TavernMap)
		{
			if (character?.Connection != null)
				this.SendRawLuaScript(character, "if SS_RELEASE_MAP_BGM ~= nil then SS_RELEASE_MAP_BGM() end");

			return;
		}

		var trackKey = mapName == TavernMap ? "klaipeda_tavern" : this.GetKlaipedaTrackKey();
		this.SendRawLuaScript(character, $"if SS_FORCE_MAP_BGM ~= nil then SS_FORCE_MAP_BGM('{trackKey}') end");

		if (mapName == TavernMap)
			this.ForceTavernBgmAgain(character);
	}

	private void ForceTavernBgmAgain(Character character)
	{
		_ = Task.Run(async () =>
		{
			foreach (var delay in new[] { 600, 1600, 3200 })
			{
				await Task.Delay(delay);

				if (character?.Connection == null || character.Map?.ClassName != TavernMap)
					return;

				this.SendRawLuaScript(character, "if SS_FORCE_MAP_BGM ~= nil then SS_FORCE_MAP_BGM('klaipeda_tavern') end");
			}
		});
	}

	private string GetKlaipedaTrackKey()
	{
		var hour = GameTime.Now.Hour;
		return hour >= 18 || hour < 6 ? "klaipeda_night" : "klaipeda_day";
	}
}
