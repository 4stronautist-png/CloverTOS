using Melia.Zone.Scripting;
using static Melia.Zone.Scripting.Shortcuts;

public class ProjectEpicBeautyShopNpcScript : GeneralScript
{
	protected override void Load()
	{
		AddNpc(1, 40001, "Barber Shop", "c_barber_dress", 72.97089, 6.97539, 1189.793, 90.0, "BEAUTY_HAIRSHOP_MOVE", "BEAUTY_HAIRSHOP_MOVE", "", -2, 15.0);
		AddNpc(2, 40001, "Boutique", "c_barber_dress", 39.45613, 54.38683, 109.495, 270.0, "BEAUTY_BOUTIQUE_MOVE", "BEAUTY_BOUTIQUE_MOVE", "", -2, 15.0);
		AddNpc(3, 40001, "Klaipeda", "c_barber_dress", -8.57293, 4.81811, -90.51904, 0.0, "BEAUTY_OUT_MOVE", "BEAUTY_OUT_MOVE", "", -2, 15.0);
		AddNpc(4, 161004, "[Hair Stylist]{nl}Anabell Swyn", "c_barber_dress", -15.959274, 4.718109, -55.123299, 0.0, "BEAUTY_SHOP_HAIR_F", "", "", -2, 23.0);
		AddNpc(5, 161005, "[Hair Stylist]{nl}Henry Swyn", "c_barber_dress", -6.081351, 4.718109, 41.641911, 0.0, "BEAUTY_SHOP_HAIR_M", "", "", -2, 23.0);
		AddNpc(6, 161003, "[Fashion Designer]{nl}Kastytis", "c_barber_dress", 23.599277, 6.875391, 1063.969238, 0.0, "BEAUTY_SHOP_FASHION", "", "", -2, 23.0);
		AddNpc(7, 150020, "UnvisibleName", "c_barber_dress", -3.16, 6.88, 1105.25, -119.0, "", "", "", -2, 1.0);
	}
}
