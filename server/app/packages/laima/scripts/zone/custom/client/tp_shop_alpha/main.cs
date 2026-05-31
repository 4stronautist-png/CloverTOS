using Melia.Zone.Scripting;
using Melia.Zone.World.Actors.Characters;

public class TpShopAlphaClientScript : ClientScript
{
	protected override void Load()
	{
		this.LoadLuaScript("001.lua");
		this.LoadLuaScript("006.lua");
		this.LoadLuaScript("007.lua");
		this.LoadLuaScript("003.lua");
		this.LoadLuaScript("004.lua");
		this.LoadLuaScript("premium_item.lua");
		this.LoadLuaScript("002.lua");
	}

	protected override void Ready(Character character)
	{
		this.SendLuaScript(character, "001.lua");
		this.SendLuaScript(character, "006.lua");
		this.SendLuaScript(character, "007.lua");
		this.SendLuaScript(character, "003.lua");
		this.SendLuaScript(character, "004.lua");
		this.SendLuaScript(character, "premium_item.lua");
		this.SendLuaScript(character, "002.lua");
	}
}
