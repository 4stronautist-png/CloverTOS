using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.Game.Const;
using Melia.Zone;
using MySqlConnector;
using Yggdrasil.Db.MySql.SimpleCommands;

namespace Melia.Zone.Database
{
	public partial class ZoneDb
	{
		public bool TryGetAccountIdByCharacterOrTeamName(string name, out long accountId)
		{
			using (var conn = this.GetConnection())
			using (var cmd = new MySqlCommand("SELECT `accountId` FROM `characters` WHERE `name` = @name OR `teamName` = @name ORDER BY CASE WHEN `name` = @name THEN 0 ELSE 1 END LIMIT 1", conn))
			{
				cmd.Parameters.AddWithValue("@name", name);

				var result = cmd.ExecuteScalar();
				if (result != null && result != DBNull.Value)
				{
					accountId = Convert.ToInt64(result);
					return true;
				}
			}

			accountId = 0;
			return false;
		}

		public long[] GetRegisteredTeamAccountIds()
		{
			var result = new List<long>();

			using (var conn = this.GetConnection())
			using (var cmd = new MySqlCommand("SELECT `accountId` FROM `accounts` WHERE `teamName` IS NOT NULL AND `teamName` <> ''", conn))
			using (var reader = cmd.ExecuteReader())
			{
				while (reader.Read())
					result.Add(reader.GetInt64("accountId"));
			}

			return result.ToArray();
		}

		public void SendItemMail(long accountId, IEnumerable<(int ItemId, int Amount)> items, string senderName, string subject, string message, int expirationDays = 7)
		{
			var itemList = items.ToArray();
			if (itemList.Length == 0)
				return;

			var now = DateTime.Now;
			var expiration = now.AddDays(Math.Max(1, expirationDays));
			subject = LimitLength(subject, 128);
			message = LimitLength(message, 2048);

			using (var conn = this.GetConnection())
			using (var trans = conn.BeginTransaction())
			{
				using var mailCmd = new InsertCommand("INSERT INTO `mail` {parameters}", conn, trans);
				mailCmd.Set("accountId", accountId);
				mailCmd.Set("status", (byte)MailboxMessageState.Unread);
				mailCmd.Set("sender", senderName);
				mailCmd.Set("subject", subject);
				mailCmd.Set("message", message);
				mailCmd.Set("startDate", now);
				mailCmd.Set("expirationDate", expiration);
				mailCmd.Set("createdDate", now);
				mailCmd.Execute();

				var mailId = mailCmd.LastId;

				foreach (var item in itemList)
				{
					var itemData = ZoneServer.Instance.Data.ItemDb.Find(item.ItemId);
					var maxStack = Math.Max(1, itemData.MaxStack);
					var remaining = item.Amount;

					while (remaining > 0)
					{
						var stackAmount = Math.Min(remaining, maxStack);

						using var itemCmd = new InsertCommand("INSERT INTO `items` {parameters}", conn, trans);
						itemCmd.Set("itemId", item.ItemId);
						itemCmd.Set("amount", stackAmount);
						itemCmd.Execute();

						using var mailItemCmd = new InsertCommand("INSERT INTO `mail_items` {parameters}", conn, trans);
						mailItemCmd.Set("mailId", mailId);
						mailItemCmd.Set("itemId", itemCmd.LastId);
						mailItemCmd.Set("id", item.ItemId);
						mailItemCmd.Set("amount", stackAmount);
						mailItemCmd.Set("status", 0);
						mailItemCmd.Execute();

						remaining -= stackAmount;
					}
				}

				trans.Commit();
			}
		}

		private static string LimitLength(string value, int maxLength)
		{
			value ??= string.Empty;
			if (value.Length <= maxLength)
				return value;

			return value.Substring(0, maxLength);
		}
	}
}
