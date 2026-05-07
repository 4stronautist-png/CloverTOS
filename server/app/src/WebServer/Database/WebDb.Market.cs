using System;
using System.Collections.Generic;
using Melia.Shared.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.Game.Const.Web;
using MySqlConnector;

namespace Melia.Web.Database
{
	public partial class WebDb
	{
		private static readonly TimeSpan MarketPublicDelay = TimeSpan.FromMinutes(1);

		public List<MarketItem> GetActiveMarketItems(bool publicOnly, long sellerCharacterId = 0)
		{
			var result = new List<MarketItem>();

			using (var conn = this.GetConnection())
			using (var cmd = new MySqlCommand(@"
				SELECT
					m.marketItemUniqueId, m.itemUniqueId, m.sellerId, m.buyerId, m.price,
					m.dateRegistered, m.dateExpired, m.status,
					i.itemId, i.amount,
					c.accountId, c.name AS sellerName
				FROM market_items AS m
				INNER JOIN items AS i ON m.itemUniqueId = i.itemUniqueId
				INNER JOIN characters AS c ON m.sellerId = c.characterId
				WHERE m.status = @status
				  AND m.dateExpired > @now
				  AND (@sellerCharacterId = 0 OR m.sellerId = @sellerCharacterId)
				  AND (@publicOnly = 0 OR m.dateRegistered <= @publicVisibleAt)
				ORDER BY m.dateRegistered DESC, m.marketItemUniqueId DESC", conn))
			{
				var now = DateTime.Now;

				cmd.Parameters.AddWithValue("@status", (byte)MarketItemStatus.Listed);
				cmd.Parameters.AddWithValue("@now", now);
				cmd.Parameters.AddWithValue("@sellerCharacterId", sellerCharacterId);
				cmd.Parameters.AddWithValue("@publicOnly", publicOnly ? 1 : 0);
				cmd.Parameters.AddWithValue("@publicVisibleAt", now.Subtract(MarketPublicDelay));

				using (var reader = cmd.ExecuteReader())
				{
					while (reader.Read())
						result.Add(this.ReadMarketItem(reader, isMine: !publicOnly));
				}
			}

			return result;
		}

		public long GetMarketMinPrice(int itemClassId)
		{
			using (var conn = this.GetConnection())
			using (var cmd = new MySqlCommand(@"
				SELECT MIN(m.price)
				FROM market_items AS m
				INNER JOIN items AS i ON m.itemUniqueId = i.itemUniqueId
				WHERE i.itemId = @itemClassId
				  AND m.status = @status
				  AND m.dateExpired > @now
				  AND m.dateRegistered <= @publicVisibleAt", conn))
			{
				var now = DateTime.Now;

				cmd.Parameters.AddWithValue("@itemClassId", itemClassId);
				cmd.Parameters.AddWithValue("@status", (byte)MarketItemStatus.Listed);
				cmd.Parameters.AddWithValue("@now", now);
				cmd.Parameters.AddWithValue("@publicVisibleAt", now.Subtract(MarketPublicDelay));

				var result = cmd.ExecuteScalar();
				if (result == null || result == DBNull.Value)
					return 0;

				return Convert.ToInt64(result);
			}
		}

		public bool HasActiveMarketItems(long characterId)
		{
			if (characterId <= 0)
				return false;

			using (var conn = this.GetConnection())
			using (var cmd = new MySqlCommand(@"
				SELECT COUNT(*)
				FROM market_items
				WHERE sellerId = @characterId
				  AND status = @status
				  AND dateExpired > @now", conn))
			{
				cmd.Parameters.AddWithValue("@characterId", characterId);
				cmd.Parameters.AddWithValue("@status", (byte)MarketItemStatus.Listed);
				cmd.Parameters.AddWithValue("@now", DateTime.Now);

				return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
			}
		}

		private MarketItem ReadMarketItem(MySqlDataReader reader, bool isMine)
		{
			return new MarketItem
			{
				MarketGuid = reader.GetInt64("marketItemUniqueId"),
				ItemGuid = reader.GetInt64("itemUniqueId"),
				ItemType = reader.GetInt32("itemId"),
				Count = reader.GetInt32("amount"),
				SellerCharacterId = reader.GetInt64("sellerId"),
				SellerAccountId = reader.GetInt64("accountId"),
				SellerName = reader.GetStringSafe("sellerName") ?? "",
				BuyerId = reader.IsDBNull(reader.GetOrdinal("buyerId")) ? 0 : reader.GetInt64("buyerId"),
				SellPrice = reader.GetInt64("price"),
				RegTime = reader.GetDateTimeSafe("dateRegistered"),
				EndTime = reader.GetDateTimeSafe("dateExpired"),
				Status = (MarketItemStatus)reader.GetByte("status"),
				IsMine = isMine,
				IsPrivate = isMine,
				PremiumState = 0,
				ShowDelay = 0,
			};
		}
	}
}
