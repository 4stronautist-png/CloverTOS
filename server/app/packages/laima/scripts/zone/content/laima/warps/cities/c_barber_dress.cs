//--- Melia Script ----------------------------------------------------------
// Warps
//--- Description -----------------------------------------------------------
// Sets up warps in Beauty Shop
//---------------------------------------------------------------------------

using Melia.Zone.Scripting;
using static Melia.Zone.Scripting.Shortcuts;

public class c_barber_dressWarpsScript : GeneralScript
{
	protected override void Load()
	{
		AddWarp(1, "BEAUTYSHOP_TO_BOUTIQUE", 270, From("c_barber_dress", 39.45613, 109.495), To("c_barber_dress", 72.97089, 1189.793));
		AddWarp(2, "BEAUTYSHOP_TO_HAIRSHOP", 90, From("c_barber_dress", 72.97089, 1189.793), To("c_barber_dress", 39.45613, 109.495));
	}
}
