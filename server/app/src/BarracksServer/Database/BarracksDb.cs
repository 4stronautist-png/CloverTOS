using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.World;
using MySqlConnector;
using Yggdrasil.Db.MySql.SimpleCommands;
using Yggdrasil.Logging;
using Yggdrasil.Security.Hashing;
using Yggdrasil.Util;

namespace Melia.Barracks.Database
{
	public class BarracksDb : MeliaDb
	{
		/// <summary>
		/// Normalizes/Updates the file names in the update db.
		/// </summary>
		/// <remarks>
		/// Temporary fix, since we had some issues with the update names.
		/// </remarks>
		/// <returns></returns>
		public void NormalizeUpdateNames()
		{
			using (var conn = this.GetConnection())
			using (var mc = new MySqlCommand("UPDATE `updates` SET `path` = REPLACE(LOWER(`path`), \"update-\", \"update_\")", conn))
			{
				mc.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Returns true if the update with the given name was already applied.
		/// </summary>
		/// <param name="updateName"></param>
		/// <returns></returns>
		public bool CheckUpdate(string updateName)
		{
			using (var conn = this.GetConnection())
			using (var mc = new MySqlCommand("SELECT * FROM `updates` WHERE `path` = @path", conn))
			{
				mc.Parameters.AddWithValue("@path", updateName);

				using (var reader = mc.ExecuteReader())
					return reader.Read();
			}
		}

		/// <summary>
		/// Executes SQL update.
		/// </summary>
		/// <param name="updateName"></param>
		/// <param name="query"></param>
		public void RunUpdate(string updateName, string query)
		{
			try
			{
				using (var conn = this.GetConnection())
				{
					// Run update
					using (var cmd = new MySqlCommand(query, conn))
						cmd.ExecuteNonQuery();

					// Log update
					using (var cmd = new InsertCommand("INSERT INTO `updates` {parameters}", conn))
					{
						cmd.Set("path", updateName);
						cmd.Execute();
					}

					Log.Info("Successfully applied '{0}'.", updateName);
				}
			}
			catch (Exception ex)
			{
				Log.Error("RunUpdate: Failed to run '{0}': {1}", updateName, ex.Message);
				ConsoleUtil.Exit(1);
			}
		}

		/// <summary>
		/// Saves account data.
		/// </summary>
		/// <param name="account"></param>
		/// <returns></returns>
		public bool SaveAccount(Account account)
		{
			if (account == null)
				throw new ArgumentNullException(nameof(account));

			using (var conn = this.GetConnection())
			using (var cmd = new UpdateCommand("UPDATE `accounts` SET {parameters} WHERE `accountId` = @accountId", conn))
			{
				cmd.AddParameter("@accountId", account.Id);
				cmd.Set("teamName", account.TeamName);
				cmd.Set("password", account.Password);
				cmd.Set("medals", account.Medals);
				cmd.Set("giftMedals", account.GiftMedals);
				cmd.Set("premiumMedals", account.PremiumMedals);
				cmd.Set("additionalSlotCount", account.AdditionalSlotCount);
				cmd.Set("teamExp", account.TeamExp);
				cmd.Set("barracksThema", account.SelectedBarrack);
				cmd.Set("themas", string.Join(" ", account.Themas));
				cmd.Set("selectedSlot", account.SelectedCharacterSlot);
				cmd.Set("language", account.Language);

				return cmd.Execute() > 0;
			}
		}

		/// <summary>
		/// Returns account with given name, or null if it doesn't exist.
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public Account GetAccount(string name)
		{
			using (var conn = this.GetConnection())
			using (var mc = new MySqlCommand("SELECT * FROM `accounts` WHERE `name` = @name", conn))
			{
				mc.Parameters.AddWithValue("@name", name);

				using (var reader = mc.ExecuteReader())
				{
					if (!reader.Read())
						return null;

					var account = new Account();
					account.Id = reader.GetInt64("accountId");
					account.Name = reader.GetStringSafe("name");
					account.TeamName = reader.GetStringSafe("teamName");
					account.Authority = reader.GetInt32("authority");
					account.PermissionLevel = (PermissionLevel)reader.GetByte("type");
					account.Password = reader.GetStringSafe("password");
					account.Medals = reader.GetInt32("medals");
					account.GiftMedals = reader.GetInt32("giftMedals");
					account.PremiumMedals = reader.GetInt32("premiumMedals");
					account.AdditionalSlotCount = reader.GetInt32("additionalSlotCount");
					account.TeamExp = reader.GetInt32("teamExp");
					account.SelectedBarrack = reader.GetInt32("barracksThema");
					account.SelectedCharacterSlot = reader.GetInt32("selectedSlot");
					account.Language = reader.GetStringSafe("language");

					var themas = reader.GetStringSafe("themas");
					account.Themas.Clear();
					account.Themas.UnionWith(themas.Split(' ').Select(int.Parse));

					// Upgrade MD5 hashes
					if (account.Password.Length == 32)
						account.Password = BCrypt.HashPassword(account.Password, BCrypt.GenerateSalt());

					// Sets additional slots to default if needed
					var defaultSlotCount = BarracksServer.Instance.Conf.Barracks.StartAdditionalSlotCount;
					if (account.AdditionalSlotCount < defaultSlotCount)
					{
						account.AdditionalSlotCount += defaultSlotCount - account.AdditionalSlotCount;
					}

					return account;
				}
			}
		}

		/// <summary>
		/// Inserts character in database.
		/// </summary>
		/// <param name="accountId"></param>
		/// <param name="character"></param>
		/// <returns></returns>
		public void CreateCharacter(long accountId, Character character)
		{
			using (var conn = this.GetConnection())
			using (var trans = conn.BeginTransaction())
			{
				using (var cmd = new InsertCommand("INSERT INTO `characters` {parameters}", conn, trans))
				{
					cmd.Set("accountId", accountId);
					cmd.Set("name", character.Name);
					cmd.Set("teamName", character.TeamName);
					cmd.Set("job", character.JobId);
					cmd.Set("gender", character.Gender);
					cmd.Set("hair", character.Hair);
					cmd.Set("skinColor", character.SkinColor);

					cmd.Set("zone", character.MapId);
					cmd.Set("x", character.Position.X);
					cmd.Set("y", character.Position.Y);
					cmd.Set("z", character.Position.Z);
					cmd.Set("bx", character.BarracksPosition.X);
					cmd.Set("by", character.BarracksPosition.Y);
					cmd.Set("bz", character.BarracksPosition.Z);
					cmd.Set("bd", character.BarracksDirection.DegreeAngle);

					cmd.Set("hp", character.Hp);
					cmd.Set("hpRate", character.HpRateByJob);
					cmd.Set("sp", character.Sp);
					cmd.Set("spRate", character.SpRateByJob);
					cmd.Set("stamina", character.Stamina);
					cmd.Set("staminaByJob", character.StaminaByJob);
					cmd.Set("strByJob", character.StrByJob);
					cmd.Set("conByJob", character.ConByJob);
					cmd.Set("intByJob", character.IntByJob);
					cmd.Set("sprByJob", character.SprByJob);
					cmd.Set("dexByJob", character.DexByJob);
					cmd.Set("maxExp", character.MaxExp);

					cmd.Set("barrackLayer", character.BarrackLayer);
					cmd.Set("slot", character.Index);

					cmd.Execute();
					character.DbId = cmd.LastId;
				}

				// Equip
				// Only save items that aren't default equipment
				foreach (var item in character.Equipment.Where(a => !InventoryDefaults.EquipItems.Contains(a.Id)))
				{
					var newId = 0L;

					using (var cmd = new InsertCommand("INSERT INTO `items` {parameters}", conn))
					{
						cmd.Set("itemId", item.Id);
						cmd.Set("amount", 1);

						cmd.Execute();

						newId = cmd.LastId;
					}

					using (var cmd = new InsertCommand("INSERT INTO `inventory` {parameters}", conn))
					{
						cmd.Set("characterId", character.DbId);
						cmd.Set("itemId", newId);
						cmd.Set("sort", 0);
						cmd.Set("equipSlot", (byte)item.Slot);

						cmd.Execute();
					}
				}

				// Job
				using (var cmd = new InsertCommand("INSERT INTO `jobs` {parameters}", conn, trans))
				{
					var now = DateTime.Now;

					cmd.Set("characterId", character.DbId);
					cmd.Set("jobId", character.JobId);
					cmd.Set("circle", 1);
					cmd.Set("skillPoints", 1);
					cmd.Set("totalExp", 0);
					cmd.Set("selectionDate", now);
					cmd.Set("advDate", now);

					cmd.Execute();
				}

				foreach (var skillId in this.GetInitialSkillIds(character.JobId))
				{
					using (var cmd = new InsertCommand("INSERT INTO `skills` {parameters}", conn, trans))
					{
						cmd.Set("characterId", character.DbId);
						cmd.Set("id", skillId);
						cmd.Set("level", 1);

						cmd.Execute();
					}
				}

				trans.Commit();
			}
		}

		private IEnumerable<SkillId> GetInitialSkillIds(JobId jobId)
		{
			var skills = new List<SkillId>
			{
				SkillId.Default,
				SkillId.Common_shovel,
				SkillId.Common_otlflag,
				SkillId.Common_dumbbell,
				SkillId.Common_vuvuzela,
				SkillId.Common_snowspray,
				SkillId.Common_balloonpipe,
			};

			switch (jobId)
			{
				case JobId.Swordsman:
					skills.AddRange([
						SkillId.Normal_Attack,
						SkillId.Normal_Attack_TH,
						SkillId.Warrior_Guard,
						SkillId.Pistol_Attack,
						SkillId.Common_DaggerAries,
					]);
					break;

				case JobId.Wizard:
					skills.AddRange([
						SkillId.Magic_Attack,
						SkillId.Magic_Attack_TH,
						SkillId.Common_DaggerAries,
						SkillId.Common_StaffAttack,
					]);
					break;

				case JobId.Archer:
					skills.AddRange([
						SkillId.Bow_Attack,
						SkillId.CrossBow_Attack,
						SkillId.Common_DaggerAries,
						SkillId.Warrior_Guard,
						SkillId.Pistol_Attack,
						SkillId.Musket_Attack,
						SkillId.Sword_Attack,
						SkillId.Cannon_Normal_Attack,
					]);
					break;

				case JobId.Cleric:
					skills.AddRange([
						SkillId.Hammer_Attack,
						SkillId.Hammer_Attack_TH,
						SkillId.Common_DaggerAries,
					]);
					break;

				case JobId.Scout:
					skills.AddRange([
						SkillId.Normal_Attack,
						SkillId.Normal_Attack_TH,
						SkillId.Warrior_Guard,
						SkillId.War_JustFrameAttack,
						SkillId.War_JustFrameDagger,
						SkillId.War_JustFramePistol,
						SkillId.Pistol_Attack,
						SkillId.Common_DaggerAries,
					]);
					break;
			}

			return skills.Distinct();
		}

		/// <summary>
		/// Deletes character.
		/// </summary>
		/// <param name="character"></param>
		/// <returns></returns>
		public bool DeleteCharacter(Character character)
		{
			using (var conn = this.GetConnection())
			using (var mc = new MySqlCommand("DELETE FROM `characters` WHERE `characterId` = @characterId", conn))
			{
				mc.Parameters.AddWithValue("@characterId", character.DbId);

				return mc.ExecuteNonQuery() > 0;
			}
		}

		/// <summary>
		/// Saves character information.
		/// </summary>
		/// <param name="character"></param>
		/// <returns></returns>
		public void SaveCharacter(Character character)
		{
			using (var conn = this.GetConnection())
			using (var cmd = new UpdateCommand("UPDATE `characters` SET {parameters} WHERE `characterId` = @characterId", conn))
			{
				cmd.AddParameter("@characterId", character.DbId);
				cmd.Set("teamName", character.TeamName);
				cmd.Set("zone", character.MapId);
				cmd.Set("bx", character.BarracksPosition.X);
				cmd.Set("by", character.BarracksPosition.Y);
				cmd.Set("bz", character.BarracksPosition.Z);
				cmd.Set("bd", character.BarracksDirection.DegreeAngle);
				cmd.Set("barrackLayer", character.BarrackLayer);
				cmd.Set("slot", character.Index);

				cmd.Execute();
			}
		}

		/// <summary>
		/// Returns all characters on given account.
		/// </summary>
		/// <param name="accountId"></param>
		/// <returns></returns>
		public List<Character> GetCharacters(long accountId)
		{
			var result = new List<Character>();

			using (var conn = this.GetConnection())
			{
				using (var mc = new MySqlCommand("SELECT * FROM `characters` WHERE `accountId` = @accountId ORDER BY `slot`", conn))
				{
					mc.Parameters.AddWithValue("@accountId", accountId);

					using (var reader = mc.ExecuteReader())
					{
						while (reader.Read())
						{
							var character = new Character();
							character.DbId = reader.GetInt64("characterId");
							character.AccountId = accountId;
							character.Name = reader.GetStringSafe("name");
							character.JobId = (JobId)reader.GetInt16("job");
							character.Gender = (Gender)reader.GetByte("gender");
							character.Hair = reader.GetInt32("hair");
							character.SkinColor = reader.GetUInt32("skinColor");
							character.Level = reader.GetInt32("level");
							character.MapId = reader.GetInt32("zone");
							character.Index = (byte)reader.GetInt32("slot");
							character.BarrackLayer = reader.GetInt32("barrackLayer");
							character.Silver = reader.GetInt32("silver");

							// Something isn't quite right with the visibility
							// after login right now, because the client always
							// shows everything as visible, even when it's not.
							// So we'll default to everything being visible
							// for now, so the player can at least properly
							// disable the visibility while in-game.
							//character.VisibleEquip = (VisibleEquip)reader.GetInt32("equipVisibility");

							var bx = reader.GetFloat("bx");
							var by = reader.GetFloat("by");
							var bz = reader.GetFloat("bz");
							character.BarracksPosition = new Position(bx, by, bz);
							character.BarracksDirection = new Direction(reader.GetFloat("bd"));

							result.Add(character);
						}
					}
				}

				foreach (var character in result)
				{
					// Items
					using (var mc = new MySqlCommand("SELECT `i`.*, `inv`.`sort`, `inv`.`equipSlot` FROM `inventory` AS `inv` INNER JOIN `items` AS `i` ON `inv`.`itemId` = `i`.`itemUniqueId` WHERE `characterId` = @characterId AND `equipSlot` != 127", conn))
					{
						mc.Parameters.AddWithValue("@characterId", character.DbId);

						using (var reader = mc.ExecuteReader())
						{
							while (reader.Read())
							{
								var itemId = reader.GetInt32("itemId");
								var equipSlot = reader.GetByte("equipSlot");

								if (!BarracksServer.Instance.Data.ItemDb.Exists(itemId))
									continue;

								if (equipSlot >= 100 && equipSlot <= 115)
								{
									character.EquippedCardIds.Add(itemId);
									continue;
								}

								if (equipSlot >= InventoryDefaults.EquipSlotCount)
									continue;

								character.Equipment[equipSlot] = new EquipItem(itemId, (EquipSlot)equipSlot);
							}
						}
					}

					// Jobs
					using (var mc = new MySqlCommand("SELECT `jobId` FROM `jobs` WHERE `characterId` = @characterId", conn))
					{
						mc.Parameters.AddWithValue("@characterId", character.DbId);

						using (var reader = mc.ExecuteReader())
						{
							while (reader.Read())
							{
								var jobId = (JobId)reader.GetInt32("jobId");

								if (!BarracksServer.Instance.Data.JobDb.TryFind(jobId, out _))
									continue;

								character.Jobs.Add(jobId);
							}
						}
					}

					if (character.Jobs.Count == 0)
						character.Jobs.Add(character.JobId);

					this.LoadVars(character.Variables.Perm, "vars_characters", "characterId", character.DbId);
				}
			}

			return result;
		}

		/// <summary>
		/// Changes the given account's auth level.
		/// </summary>
		/// <param name="accountName"></param>
		/// <param name="level"></param>
		/// <returns></returns>
		public bool ChangeAuth(string accountName, int level)
		{
			using (var conn = this.GetConnection())
			using (var cmd = new UpdateCommand("UPDATE `accounts` SET {parameters} WHERE `name` = @accountName", conn))
			{
				cmd.AddParameter("@accountName", accountName);
				cmd.Set("authority", level);

				return (cmd.Execute() > 0);
			}
		}

		/// <summary>
		/// Changes the given account's password.
		/// </summary>
		/// <param name="accountName"></param>
		/// <param name="password"></param>
		public void SetAccountPassword(string accountName, string password)
		{
			var salt = BCrypt.GenerateSalt();
			var hashedPassword = MD5.Encode(password);
			hashedPassword = BCrypt.HashPassword(hashedPassword, salt);

			using (var conn = this.GetConnection())
			using (var mc = new MySqlCommand("UPDATE `accounts` SET `password` = @password WHERE `name` = @accountName", conn))
			{
				mc.Parameters.AddWithValue("@accountName", accountName);
				mc.Parameters.AddWithValue("@password", hashedPassword);

				mc.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Loads mail for the account.
		/// </summary>
		/// <param name="account"></param>
		public void LoadMailbox(Account account)
		{
			this.DeliverPendingGlobalMail(account.Id);

			using (var conn = this.GetConnection())
			using (var mc = new MySqlCommand("SELECT * FROM `mail` WHERE `accountId` = @accountId", conn))
			{
				mc.Parameters.AddWithValue("@accountId", account.Id);

				using (var reader = mc.ExecuteReader())
				{
					while (reader.Read())
					{
						var state = (MailboxMessageState)reader.GetByte("status");
						if (state == MailboxMessageState.Delete)
							continue;

						var expiration = reader.GetDateTimeSafe("expirationDate");
						if (DateTime.Now >= expiration)
							continue;

						var mail = new MailMessage
						{
							Id = reader.GetInt64("mailId"),
							State = state,
							Sender = reader.GetStringSafe("sender"),
							Subject = reader.GetStringSafe("subject"),
							Message = reader.GetStringSafe("message"),
							StartDate = reader.GetDateTimeSafe("startDate"),
							ExpirationDate = expiration,
							CreatedDate = reader.GetDateTimeSafe("createdDate"),
						};

						account.Mailbox.AddMail(mail);
					}
				}
			}

			// XXX: Optimize to get get all items at once?
			foreach (var mail in account.Mailbox.GetMessages())
			{
				foreach (var item in this.LoadMailItems(mail.Id))
					mail.AddItem(item);
			}
		}

		/// <summary>
		/// Loads mail items for a specific mail.
		/// </summary>
		/// <param name="mailId"></param>
		/// <returns></returns>
		public List<MailItem> LoadMailItems(long mailId)
		{
			var items = new List<MailItem>();
			using (var conn = this.GetConnection())
			{
				using (var mc = new MySqlCommand("SELECT * FROM `mail_items` WHERE `mailId` = @mailId", conn))
				{
					mc.Parameters.AddWithValue("@mailId", mailId);

					using (var reader = mc.ExecuteReader())
					{
						while (reader.Read())
						{
							var mailItem = new MailItem
							{
								DbId = (int)reader.GetInt64("mailItemId"),
								ItemDbId = reader.GetInt64("itemId"),
								Id = reader.GetInt32("id"),
								Amount = reader.GetInt32("amount"),
								WasReceived = reader.GetByte("status") == 1,
							};

							items.Add(mailItem);
						}
					}
				}
			}

			return items;
		}

		/// <summary>
		/// Persists the account's mail to the database.
		/// </summary>
		/// <param name="account"></param>
		public void SaveMail(Account account)
		{
			using (var conn = this.GetConnection())
			using (var trans = conn.BeginTransaction())
			{
				foreach (var mail in account.Mailbox.GetMessages())
				{
					using (var cmd = new UpdateCommand("UPDATE `mail` SET {parameters} WHERE `mailId` = @mailId", conn, trans))
					{
						cmd.AddParameter("@mailId", mail.Id);
						cmd.Set("accountId", account.Id);
						cmd.Set("sender", mail.Sender);
						cmd.Set("subject", mail.Subject);
						cmd.Set("message", mail.Message);
						cmd.Set("status", (byte)mail.State);
						cmd.Set("startDate", mail.StartDate);
						cmd.Set("expirationDate", mail.ExpirationDate);
						cmd.Set("createdDate", mail.CreatedDate);

						cmd.Execute();
					}

					foreach (var item in mail.GetItems())
					{
						using (var cmd = new UpdateCommand("UPDATE `mail_items` SET {parameters} WHERE `mailItemId` = @mailItemId", conn, trans))
						{
							cmd.AddParameter("@mailItemId", item.DbId);
							cmd.Set("mailId", mail.Id);
							cmd.Set("itemId", item.ItemDbId);
							cmd.Set("id", item.Id);
							cmd.Set("amount", item.Amount);
							cmd.Set("status", item.WasReceived);
							cmd.Execute();
						}
					}
				}

				trans.Commit();
			}
		}

		private void DeliverPendingGlobalMail(long accountId)
		{
			using (var conn = this.GetConnection())
			{
				this.EnsureGlobalMailTables(conn);

				using (var trans = conn.BeginTransaction())
				{
					this.DeleteExpiredMail(conn, trans);

					var globalMailIds = new List<long>();
					using (var cmd = new MySqlCommand(@"
						SELECT gm.`globalMailId`
						FROM `global_mail` gm
						LEFT JOIN `global_mail_deliveries` gmd
							ON gmd.`globalMailId` = gm.`globalMailId` AND gmd.`accountId` = @accountId
						WHERE gm.`expirationDate` > @now AND gmd.`globalMailId` IS NULL", conn, trans))
					{
						cmd.Parameters.AddWithValue("@accountId", accountId);
						cmd.Parameters.AddWithValue("@now", DateTime.Now);

						using (var reader = cmd.ExecuteReader())
						{
							while (reader.Read())
								globalMailIds.Add(reader.GetInt64("globalMailId"));
						}
					}

					foreach (var globalMailId in globalMailIds)
					{
						if (!this.TryInsertGlobalMailForAccount(conn, trans, globalMailId, accountId))
							continue;

						using (var deliveryCmd = new InsertCommand("INSERT INTO `global_mail_deliveries` {parameters}", conn, trans))
						{
							deliveryCmd.Set("globalMailId", globalMailId);
							deliveryCmd.Set("accountId", accountId);
							deliveryCmd.Set("deliveredDate", DateTime.Now);
							deliveryCmd.Execute();
						}
					}

					trans.Commit();
				}
			}
		}

		private bool TryInsertGlobalMailForAccount(MySqlConnection conn, MySqlTransaction trans, long globalMailId, long accountId)
		{
			string sender, subject, message;
			DateTime startDate, expirationDate, createdDate;

			using (var cmd = new MySqlCommand("SELECT * FROM `global_mail` WHERE `globalMailId` = @globalMailId AND `expirationDate` > @now", conn, trans))
			{
				cmd.Parameters.AddWithValue("@globalMailId", globalMailId);
				cmd.Parameters.AddWithValue("@now", DateTime.Now);

				using (var reader = cmd.ExecuteReader())
				{
					if (!reader.Read())
						return false;

					sender = reader.GetStringSafe("sender");
					subject = reader.GetStringSafe("subject");
					message = reader.GetStringSafe("message");
					startDate = reader.GetDateTimeSafe("startDate");
					expirationDate = reader.GetDateTimeSafe("expirationDate");
					createdDate = reader.GetDateTimeSafe("createdDate");
				}
			}

			var items = new List<(int ItemId, int Amount)>();
			using (var cmd = new MySqlCommand("SELECT `itemId`, `amount` FROM `global_mail_items` WHERE `globalMailId` = @globalMailId", conn, trans))
			{
				cmd.Parameters.AddWithValue("@globalMailId", globalMailId);

				using (var reader = cmd.ExecuteReader())
				{
					while (reader.Read())
						items.Add((reader.GetInt32("itemId"), reader.GetInt32("amount")));
				}
			}

			if (items.Count == 0)
				return false;

			using var mailCmd = new InsertCommand("INSERT INTO `mail` {parameters}", conn, trans);
			mailCmd.Set("accountId", accountId);
			mailCmd.Set("status", (byte)MailboxMessageState.Unread);
			mailCmd.Set("sender", sender);
			mailCmd.Set("subject", subject);
			mailCmd.Set("message", message);
			mailCmd.Set("startDate", startDate);
			mailCmd.Set("expirationDate", expirationDate);
			mailCmd.Set("createdDate", createdDate);
			mailCmd.Execute();

			var mailId = mailCmd.LastId;

			foreach (var item in items)
			{
				if (!BarracksServer.Instance.Data.ItemDb.Exists(item.ItemId))
					continue;

				using var itemCmd = new InsertCommand("INSERT INTO `items` {parameters}", conn, trans);
				itemCmd.Set("itemId", item.ItemId);
				itemCmd.Set("amount", item.Amount);
				itemCmd.Set("locked", 0);
				itemCmd.Execute();

				using var mailItemCmd = new InsertCommand("INSERT INTO `mail_items` {parameters}", conn, trans);
				mailItemCmd.Set("mailId", mailId);
				mailItemCmd.Set("itemId", itemCmd.LastId);
				mailItemCmd.Set("id", item.ItemId);
				mailItemCmd.Set("amount", item.Amount);
				mailItemCmd.Set("status", 0);
				mailItemCmd.Execute();
			}

			return true;
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

		/// <summary>
		/// Changes the name of a character on an account.
		/// </summary>
		/// <param name="accountId"></param>
		/// <param name="oldName"></param>
		/// <param name="newName"></param>
		/// <returns></returns>
		public bool UpdateCharacterName(long accountId, string oldName, string newName)
		{
			using (var conn = this.GetConnection())
			using (var cmd = new UpdateCommand("UPDATE `characters` SET {parameters} WHERE `accountId` = @accountId AND `name` = @oldName", conn))
			{
				cmd.AddParameter("@accountId", accountId);
				cmd.AddParameter("@oldName", oldName);
				cmd.Set("name", newName);

				return cmd.Execute() > 0;
			}
		}

		/// <summary>
		/// Updates the team name on all characters belonging to an account.
		/// </summary>
		/// <param name="accountId"></param>
		/// <param name="teamName"></param>
		/// <returns></returns>
		public bool UpdateCharactersTeamName(long accountId, string teamName)
		{
			using (var conn = this.GetConnection())
			using (var mc = new MySqlCommand("UPDATE `characters` SET `teamName` = @teamName WHERE `accountId` = @accountId", conn))
			{
				mc.Parameters.AddWithValue("@accountId", accountId);
				mc.Parameters.AddWithValue("@teamName", teamName);

				return mc.ExecuteNonQuery() > 0;
			}
		}

		/// <summary>
		/// Checks if a character has any active market items (Listed status).
		/// </summary>
		/// <param name="characterId">The character's database ID.</param>
		/// <returns>True if the character has active market listings.</returns>
		public bool HasActiveMarketItems(long characterId)
		{
			using (var conn = this.GetConnection())
			using (var mc = new MySqlCommand("SELECT COUNT(*) FROM `market_items` WHERE `sellerId` = @characterId AND `status` = 1", conn))
			{
				mc.Parameters.AddWithValue("@characterId", characterId);

				var count = Convert.ToInt32(mc.ExecuteScalar());
				return count > 0;
			}
		}

		/// <summary>
		/// Returns all companions on given account.
		/// </summary>
		/// <param name="accountId"></param>
		/// <returns></returns>
		public List<Companion> GetCompanions(long accountId)
		{
			var result = new List<Companion>();

			using (var conn = this.GetConnection())
			{
				using (var mc = new MySqlCommand("SELECT * FROM `companions` WHERE `accountId` = @accountId ORDER BY `slot`", conn))
				{
					mc.Parameters.AddWithValue("@accountId", accountId);

					using (var reader = mc.ExecuteReader())
					{
						while (reader.Read())
						{
							var characterId = reader.IsDBNull(2) ? 0 : reader.GetInt64("characterId");
							var companion = new Companion(reader.GetInt64("companionId"), reader.GetInt64("accountId"), characterId);
							companion.MonsterId = reader.GetInt32("monsterId");
							companion.Name = reader.GetStringSafe("name");
							companion.Index = (byte)reader.GetInt32("slot");
							companion.BarracksLayer = reader.GetInt32("barrackLayer");
							// Use cumulative totalExp so the barracks client derives
							// the companion's level correctly. The `exp` column only
							// stores partial progress toward the next level and is
							// reset on each level up in the zone server.
							companion.Exp = reader.GetInt64("totalExp");

							var bx = reader.GetFloat("bx");
							var by = reader.GetFloat("by");
							var bz = reader.GetFloat("bz");
							companion.BarracksPosition = new Position(bx, by, bz);

							var dx = reader.GetFloat("dx");
							var dy = reader.GetFloat("dy");
							companion.BarracksDirection = new Direction(dx, dy);

							result.Add(companion);
						}
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Set the current character associated with a companion.
		/// </summary>
		/// <param name="companionId"></param>
		/// <param name="characterId"></param>
		public void SetCompanionCharacter(long companionId, long characterId)
		{
			using (var conn = this.GetConnection())
			using (var trans = conn.BeginTransaction())
			{
				using (var cmd = new UpdateCommand("UPDATE `companions` SET {parameters} WHERE `companionId` = @companionId", conn, trans))
				{
					cmd.AddParameter("@companionId", companionId);
					if (characterId > 0)
						cmd.Set("characterId", characterId);
					else
						cmd.Set("characterId", null);

					cmd.Execute();
				}
				trans.Commit();
			}
		}

		/// <summary>
		/// Deletes a companion.
		/// </summary>
		/// <param name="companionId"></param>
		/// <returns></returns>
		public bool DeleteCompanion(long companionId)
		{
			using (var conn = this.GetConnection())
			using (var mc = new MySqlCommand("DELETE FROM `companions` WHERE `companionId` = @companionId", conn))
			{
				mc.Parameters.AddWithValue("@companionId", companionId);

				return mc.ExecuteNonQuery() > 0;
			}
		}

		/// <summary>
		/// Saves companion information.
		/// </summary>
		/// <param name="companion"></param>
		public void SaveCompanion(Companion companion)
		{
			using (var conn = this.GetConnection())
			using (var cmd = new UpdateCommand("UPDATE `companions` SET {parameters} WHERE `companionId` = @companionId", conn))
			{
				cmd.AddParameter("@companionId", companion.DbId);
				if (companion.CharacterDbId > 0)
					cmd.Set("characterId", companion.CharacterDbId);
				else
					cmd.Set("characterId", null);
				cmd.Set("bx", companion.BarracksPosition.X);
				cmd.Set("by", companion.BarracksPosition.Y);
				cmd.Set("bz", companion.BarracksPosition.Z);
				cmd.Set("dx", companion.BarracksDirection.Cos);
				cmd.Set("dy", companion.BarracksDirection.Sin);
				cmd.Set("barrackLayer", companion.BarracksLayer);
				cmd.Set("slot", companion.Index);
				cmd.Set("exp", companion.Exp);

				cmd.Execute();
			}
		}

		/// <summary>
		/// Adds an item to the character's inventory.
		/// </summary>
		/// <param name="character"></param>
		/// <param name="itemId"></param>
		public void SaveItem(Character character, long itemId)
		{
			using (var conn = this.GetConnection())
			using (var trans = conn.BeginTransaction())
			{
				using (var cmd = new InsertCommand("INSERT INTO `inventory` {parameters}", conn, trans))
				{
					cmd.Set("characterId", character.DbId);
					cmd.Set("itemId", itemId);
					cmd.Set("sort", 0);
					cmd.Set("equipSlot", 0x7F);

					cmd.Execute();
				}

				trans.Commit();
			}
		}
	}
}
