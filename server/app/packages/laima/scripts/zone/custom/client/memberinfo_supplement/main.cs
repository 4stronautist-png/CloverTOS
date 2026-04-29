//--- SoulSociety Script ----------------------------------------------------
// Member Info Supplement
//--- Description -----------------------------------------------------------
// Complements the compare window with class icons and achievement counts.
//---------------------------------------------------------------------------

using Melia.Zone.Scripting;
using Melia.Zone.World.Actors.Characters;

public class MemberInfoSupplementClientScript : ClientScript
{
	protected override void Load()
	{
		this.LoadAllScripts();
	}

	protected override void Ready(Character character)
	{
		this.SendAllScripts(character);
	}
}
