using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.Routing;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.Game.Const.Web;
using Melia.Web.Const;
using Newtonsoft.Json;

namespace Melia.Web.Controllers
{
	/// <summary>
	/// Handles the classic client's market web calls.
	/// </summary>
	public class MarketController : BaseController
	{
		private static long LastKnownCharacterId;

		[Route(HttpVerbs.Get, "/market/alive_ping")]
		public async Task AlivePing()
		{
			await this.SendText(MimeTypes.Text.Plain, "OK");
		}

		[Route(HttpVerbs.Get, "/market/min_price/{classId}")]
		public async Task MinPrice(int classId)
		{
			// The client expects plain text here.
			var minPrice = WebServer.Instance.Database.GetMarketMinPrice(classId);
			await this.SendText(MimeTypes.Text.Plain, minPrice.ToString());
		}

		[Route(HttpVerbs.Get, "/market/my_sell_list")]
		public async Task MySellList()
		{
			await this.SendSellList();
		}

		[Route(HttpVerbs.Get, "/market/my_sell_list/")]
		public async Task MySellListSlash()
		{
			await this.SendSellList();
		}

		[Route(HttpVerbs.Put, "/market/search/{page}/{perPage}")]
		public async Task Search(int page, int perPage)
		{
			var search = await this.GetMarketSearch();
			var items = WebServer.Instance.Database.GetActiveMarketItems(publicOnly: true);
			items = this.FilterMarketItems(items, search);
			await this.SendMarketList(items);
		}

		[Route(HttpVerbs.Put, "/market/search_recipe/{page}/{perPage}")]
		public async Task SearchRecipe(int page, int perPage)
		{
			await this.SendText(MimeTypes.Text.Plain, JsonConvert.SerializeObject(new { find_recipe_list = Array.Empty<object>() }));
		}

		[Route(HttpVerbs.Get, "/market/is_market_registered/{characterId}")]
		public async Task IsMarketRegistered(long characterId)
		{
			if (characterId > 0)
				Interlocked.Exchange(ref LastKnownCharacterId, characterId);

			var hasItems = WebServer.Instance.Database.HasActiveMarketItems(characterId);
			await this.SendText(MimeTypes.Text.Plain, hasItems ? "True" : "False");
		}

		private async Task SendSellList()
		{
			var characterId = Interlocked.Read(ref LastKnownCharacterId);
			var items = WebServer.Instance.Database.GetActiveMarketItems(publicOnly: false, sellerCharacterId: characterId);

			if (items.Count == 0 && characterId != 0)
				items = WebServer.Instance.Database.GetActiveMarketItems(publicOnly: false);

			await this.SendMarketList(items);
		}

		private async Task SendMarketList(System.Collections.Generic.List<MarketItem> items)
		{
			var json = JsonConvert.SerializeObject(new MarketSearchResult(items));
			await this.SendText(MimeTypes.Text.Plain, HttpStatusCode.OK, json);
		}

		private async Task<MarketSearch> GetMarketSearch()
		{
			try
			{
				using var reader = new StreamReader(this.Request.InputStream, Encoding.UTF8, false, leaveOpen: true);
				var body = await reader.ReadToEndAsync();
				if (string.IsNullOrWhiteSpace(body))
					return new MarketSearch();

				return JsonConvert.DeserializeObject<MarketSearch>(body) ?? new MarketSearch();
			}
			catch
			{
				return new MarketSearch();
			}
		}

		private System.Collections.Generic.List<MarketItem> FilterMarketItems(System.Collections.Generic.List<MarketItem> items, MarketSearch search)
		{
			var result = items.AsEnumerable();

			if (!string.IsNullOrWhiteSpace(search?.FindText))
			{
				var findText = search.FindText.Trim();
				result = result.Where(item =>
				{
					var data = WebServer.Instance.Data.ItemDb.Find(item.ItemType);
					return data != null && (data.Name.Contains(findText, StringComparison.OrdinalIgnoreCase) || data.ClassName.Contains(findText, StringComparison.OrdinalIgnoreCase));
				});
			}

			if (!string.IsNullOrWhiteSpace(search?.Category))
			{
				var category = NormalizeMarketCategory(search.Category);
				if (category != "" && category != "searchall" && category != "all")
					result = result.Where(item => IsMarketCategory(item.ItemType, category));
			}

			result = search?.PriceOrder switch
			{
				1 => result.OrderBy(item => item.SellPrice),
				2 => result.OrderByDescending(item => item.SellPrice),
				_ => result,
			};

			return result.ToList();
		}

		private static string NormalizeMarketCategory(string category)
		{
			var sb = new StringBuilder();
			foreach (var ch in category)
			{
				if (char.IsLetterOrDigit(ch))
					sb.Append(char.ToLowerInvariant(ch));
			}

			return sb.ToString();
		}

		private static bool IsMarketCategory(int itemType, string category)
		{
			var data = WebServer.Instance.Data.ItemDb.Find(itemType);
			if (data == null)
				return false;

			return category switch
			{
				"weapon" => data.Type == ItemType.Equip && (data.Group == ItemGroup.Weapon || data.Group == ItemGroup.SubWeapon || data.Category.ToString().StartsWith("Weapon_") || data.Category == InventoryCategory.Weapon || data.Category == InventoryCategory.SubWeapon),
				"armor" => data.Type == ItemType.Equip && (data.Group == ItemGroup.Armor || data.Category.ToString().StartsWith("Armor_") || data.Category == InventoryCategory.Armor),
				"consume" => data.Type == ItemType.Consume || data.Category.ToString().StartsWith("Consume_") || data.Group == ItemGroup.Drug || data.Group == ItemGroup.Consume,
				"accessory" => data.Group == ItemGroup.Armband || data.Group == ItemGroup.Seal || data.Group == ItemGroup.Ark || data.Group == ItemGroup.Relic || data.Group == ItemGroup.Earring || data.Group == ItemGroup.BELT || data.Group == ItemGroup.SHOULDER || data.Group == ItemGroup.CORE || data.Category.ToString().StartsWith("Accessory_") || data.Category == InventoryCategory.Accessory,
				"recipe" => data.Type == ItemType.Recipe || data.Group == ItemGroup.Recipe || data.Category.ToString().StartsWith("Recipe_") || data.Category == InventoryCategory.Recipe,
				"card" => data.Group == ItemGroup.Card || data.Category.ToString().Contains("Card"),
				"material" => data.Group == ItemGroup.Material || data.Group == ItemGroup.LegendMaterial || data.Group == ItemGroup.SpecialMaterial || data.Group == ItemGroup.MagicAmulet || data.Group == ItemGroup.Icor || data.Category.ToString().StartsWith("Misc_") || data.Category.ToString().StartsWith("OPTMisc_"),
				"gems" or "gem" => data.Group == ItemGroup.Gem || data.Group == ItemGroup.Gem_High_Color || data.Group == ItemGroup.Gem_Relic || data.Category.ToString().StartsWith("Gem"),
				"specialgearmaterials" => data.Group == ItemGroup.SpecialMaterial || data.Group == ItemGroup.Icor || data.Category.ToString().StartsWith("OPTMisc_"),
				"arts" => data.Group == ItemGroup.HiddenAbility || data.Category.ToString().StartsWith("HiddenAbility_"),
				"hairaccessories" or "hairaccessory" => data.Category.ToString().StartsWith("HairAcc_") || data.Group == ItemGroup.Helmet,
				"appearance" => data.Category.ToString().StartsWith("Look_") || data.Category == InventoryCategory.Outer,
				"appearanceequipment" => data.Category.ToString().StartsWith("ChangeEquip_"),
				"premium" => data.Group == ItemGroup.Premium || data.Category.ToString().StartsWith("Premium_"),
				"arcanumbrewing" => data.Category.ToString().StartsWith("Pharmacy_"),
				_ => true,
			};
		}
	}
}
