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

		public long[] GetAllAccountIds()
		{
			var result = new List<long>();

			using (var conn = this.GetConnection())
			using (var cmd = new MySqlCommand("SELECT `accountId` FROM `accounts`", conn))
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
				this.InsertItemMail(conn, trans, accountId, itemList, senderName, subject, message, now, expiration, now);
				trans.Commit();
			}
		}

		public int SendGlobalItemMail(IEnumerable<long> accountIds, IEnumerable<(int ItemId, int Amount)> items, string senderName, string subject, string message, int expirationDays)
		{
			var itemList = items.ToArray();
			if (itemList.Length == 0)
				return 0;

			var accountIdList = accountIds.Distinct().ToArray();
			var now = DateTime.Now;
			var expiration = now.AddDays(Math.Max(1, expirationDays));
			subject = LimitLength(subject, 128);
			message = LimitLength(message, 2048);

			using (var conn = this.GetConnection())
			{
				this.EnsureGlobalMailTables(conn);

				using (var trans = conn.BeginTransaction())
				{
					this.DeleteExpiredMail(conn, trans);

					using var globalCmd = new InsertCommand("INSERT INTO `global_mail` {parameters}", conn, trans);
					globalCmd.Set("sender", senderName);
					globalCmd.Set("subject", subject);
					globalCmd.Set("message", message);
					globalCmd.Set("startDate", now);
					globalCmd.Set("expirationDate", expiration);
					globalCmd.Set("createdDate", now);
					globalCmd.Execute();

					var globalMailId = globalCmd.LastId;

					foreach (var item in itemList)
					{
						var itemData = ZoneServer.Instance.Data.ItemDb.Find(item.ItemId);
						var maxStack = Math.Max(1, itemData.MaxStack);
						var remaining = item.Amount;

						while (remaining > 0)
						{
							var stackAmount = Math.Min(remaining, maxStack);

							using var globalItemCmd = new InsertCommand("INSERT INTO `global_mail_items` {parameters}", conn, trans);
							globalItemCmd.Set("globalMailId", globalMailId);
							globalItemCmd.Set("itemId", item.ItemId);
							globalItemCmd.Set("amount", stackAmount);
							globalItemCmd.Execute();

							remaining -= stackAmount;
						}
					}

					var sent = 0;
					foreach (var accountId in accountIdList)
					{
						this.InsertItemMail(conn, trans, accountId, itemList, senderName, subject, message, now, expiration, now);

						using var deliveryCmd = new InsertCommand("INSERT INTO `global_mail_deliveries` {parameters}", conn, trans);
						deliveryCmd.Set("globalMailId", globalMailId);
						deliveryCmd.Set("accountId", accountId);
						deliveryCmd.Set("deliveredDate", now);
						deliveryCmd.Execute();

						sent++;
					}

					trans.Commit();
					return sent;
				}
			}
		}

		private void InsertItemMail(MySqlConnection conn, MySqlTransaction trans, long accountId, IEnumerable<(int ItemId, int Amount)> items, string senderName, string subject, string message, DateTime startDate, DateTime expirationDate, DateTime createdDate)
		{
			using var mailCmd = new InsertCommand("INSERT INTO `mail` {parameters}", conn, trans);
			mailCmd.Set("accountId", accountId);
			mailCmd.Set("status", (byte)MailboxMessageState.Unread);
			mailCmd.Set("sender", senderName);
			mailCmd.Set("subject", subject);
			mailCmd.Set("message", message);
			mailCmd.Set("startDate", startDate);
			mailCmd.Set("expirationDate", expirationDate);
			mailCmd.Set("createdDate", createdDate);
			mailCmd.Execute();

			var mailId = mailCmd.LastId;

			foreach (var item in items)
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
					itemCmd.Set("locked", 0);
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
		}

		private void DeleteExpiredMail(MySqlConnection conn, MySqlTransaction trans)
		{
			using (var mailCmd = new MySqlCommand("DELETE FROM `mail` WHERE `expirationDate` <= @now", conn, trans))
			{
				mailCmd.Parameters.AddWithValue("@now", DateTime.Now);
				mailCmd.ExecuteNonQuery();
			}

			using (var globalCmd = new MySqlCommand("DELETE FROM `global_mail` WHERE `expirationDate` <= @now", conn, trans))
			{
				globalCmd.Parameters.AddWithValue("@now", DateTime.Now);
				globalCmd.ExecuteNonQuery();
			}
		}

		private void EnsureGlobalMailTables(MySqlConnection conn)
		{
			using (var cmd = new MySqlCommand(@"
				CREATE TABLE IF NOT EXISTS `global_mail` (
					`globalMailId` bigint(20) NOT NULL AUTO_INCREMENT,
					`sender` varchar(128) NOT NULL,
					`subject` varchar(128) NOT NULL,
					`message` varchar(2048) DEFAULT NULL,
					`startDate` datetime DEFAULT '2016-04-01 00:00:00',
					`expirationDate` datetime DEFAULT '2038-01-01 00:00:00',
					`createdDate` datetime DEFAULT CURRENT_TIMESTAMP,
					PRIMARY KEY (`globalMailId`)
				) ENGINE=InnoDB DEFAULT CHARSET=utf8", conn))
			{
				cmd.ExecuteNonQuery();
			}

			using (var cmd = new MySqlCommand(@"
				CREATE TABLE IF NOT EXISTS `global_mail_items` (
					`globalMailItemId` bigint(20) NOT NULL AUTO_INCREMENT,
					`globalMailId` bigint(20) NOT NULL,
					`itemId` int(11) NOT NULL,
					`amount` int(11) NOT NULL,
					PRIMARY KEY (`globalMailItemId`),
					KEY `globalMailId` (`globalMailId`)
				) ENGINE=InnoDB DEFAULT CHARSET=utf8", conn))
			{
				cmd.ExecuteNonQuery();
			}

			using (var cmd = new MySqlCommand(@"
				CREATE TABLE IF NOT EXISTS `global_mail_deliveries` (
					`globalMailId` bigint(20) NOT NULL,
					`accountId` bigint(20) NOT NULL,
					`deliveredDate` datetime DEFAULT CURRENT_TIMESTAMP,
					PRIMARY KEY (`globalMailId`, `accountId`)
				) ENGINE=InnoDB DEFAULT CHARSET=utf8", conn))
			{
				cmd.ExecuteNonQuery();
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
