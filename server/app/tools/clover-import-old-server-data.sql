USE laima_local;

SET FOREIGN_KEY_CHECKS=0;

CREATE TEMPORARY TABLE source_accounts
SELECT accountId
FROM melia.accounts;

CREATE TEMPORARY TABLE source_characters
SELECT characterId
FROM melia.characters
WHERE accountId IN (SELECT accountId FROM source_accounts);

CREATE TEMPORARY TABLE source_mail_ids
SELECT mailId
FROM melia.mail
WHERE accountId IN (SELECT accountId FROM source_accounts);

CREATE TEMPORARY TABLE source_buff_ids
SELECT buffId
FROM melia.buffs
WHERE characterId IN (SELECT characterId FROM source_characters);

CREATE TEMPORARY TABLE source_item_ids
SELECT itemId
FROM melia.inventory
WHERE characterId IN (SELECT characterId FROM source_characters)
UNION
SELECT itemId
FROM melia.storage_personal
WHERE characterId IN (SELECT characterId FROM source_characters)
UNION
SELECT itemId
FROM melia.storage_team
WHERE accountId IN (SELECT accountId FROM source_accounts)
UNION
SELECT itemId
FROM melia.mail_items
WHERE mailId IN (SELECT mailId FROM source_mail_ids);

DELETE FROM laima_local.vars_buffs
WHERE buffId IN (SELECT buffId FROM source_buff_ids);

DELETE FROM laima_local.buffs
WHERE characterId IN (SELECT characterId FROM source_characters);

DELETE FROM laima_local.abilities
WHERE characterId IN (SELECT characterId FROM source_characters);

DELETE FROM laima_local.character_etc_properties
WHERE characterId IN (SELECT characterId FROM source_characters);

DELETE FROM laima_local.character_properties
WHERE characterId IN (SELECT characterId FROM source_characters);

DELETE FROM laima_local.cooldowns
WHERE characterId IN (SELECT characterId FROM source_characters);

DELETE FROM laima_local.inventory
WHERE characterId IN (SELECT characterId FROM source_characters);

DELETE FROM laima_local.jobs
WHERE characterId IN (SELECT characterId FROM source_characters);

DELETE FROM laima_local.likes
WHERE receiverId IN (SELECT characterId FROM source_characters)
   OR senderId IN (SELECT characterId FROM source_characters);

DELETE FROM laima_local.mail_items
WHERE mailId IN (SELECT mailId FROM source_mail_ids);

DELETE FROM laima_local.mail
WHERE accountId IN (SELECT accountId FROM source_accounts);

DELETE FROM laima_local.quests_progress
WHERE characterId IN (SELECT characterId FROM source_characters);

DELETE FROM laima_local.quests
WHERE characterId IN (SELECT characterId FROM source_characters);

DELETE FROM laima_local.revealedmaps
WHERE accountId IN (SELECT accountId FROM source_accounts);

DELETE FROM laima_local.session_objects_properties
WHERE characterId IN (SELECT characterId FROM source_characters);

DELETE FROM laima_local.skills
WHERE characterId IN (SELECT characterId FROM source_characters);

DELETE FROM laima_local.social_users
WHERE accountId IN (SELECT accountId FROM source_accounts);

DELETE FROM laima_local.storage_personal
WHERE characterId IN (SELECT characterId FROM source_characters);

DELETE FROM laima_local.storage_team
WHERE accountId IN (SELECT accountId FROM source_accounts);

DELETE FROM laima_local.vars_accounts
WHERE accountId IN (SELECT accountId FROM source_accounts);

DELETE FROM laima_local.vars_characters
WHERE characterId IN (SELECT characterId FROM source_characters);

DELETE FROM laima_local.account_properties
WHERE accountId IN (SELECT accountId FROM source_accounts);

DELETE FROM laima_local.chatmacros
WHERE accountId IN (SELECT accountId FROM source_accounts);

DELETE FROM laima_local.collection_items
WHERE accountId IN (SELECT accountId FROM source_accounts);

DELETE FROM laima_local.collections
WHERE accountId IN (SELECT accountId FROM source_accounts);

DELETE FROM laima_local.friends
WHERE userId IN (SELECT accountId FROM source_accounts)
   OR friendUserId IN (SELECT accountId FROM source_accounts);

DELETE FROM laima_local.items
WHERE itemUniqueId IN (SELECT itemId FROM source_item_ids);

DELETE FROM laima_local.characters
WHERE accountId IN (SELECT accountId FROM source_accounts);

REPLACE INTO laima_local.accounts
(
	accountId, name, password, sessionKey, teamName, type, authority, settings,
	medals, giftMedals, premiumMedals, additionalSlotCount, teamExp, barracksThema,
	themas, selectedSlot, loginState, loginCharacter, premiumTokenExpiration,
	language, lastLogin
)
SELECT
	accountId, name, password, sessionKey, teamName, 3, authority, settings,
	medals, giftMedals, premiumMedals, additionalSlotCount, teamExp, barracksThema,
	themas, selectedSlot, 0, 0, premiumTokenExpiration, language, lastLogin
FROM melia.accounts;

REPLACE INTO laima_local.characters
(
	`characterId`, `accountId`, `name`, `teamName`, `job`, `gender`, `hair`, `skinColor`, `level`,
	`slot`, `barrackLayer`, `bx`, `by`, `bz`, `bd`, `bdir`, `zone`, `x`, `y`, `z`, `dir`, `exp`, `maxExp`,
	`totalExp`, `hp`, `hpRate`, `maxHp`, `sp`, `spRate`, `maxSp`, `stamina`, `staminaByJob`,
	`maxStamina`, `str`, `strByJob`, `con`, `conByJob`, `int`, `intByJob`, `spr`, `sprByJob`, `dex`,
	`dexByJob`, `statByLevel`, `statByBonus`, `usedStat`, `abilityPoints`, `silver`,
	`equipVisibility`
)
SELECT
	`characterId`, `accountId`, `name`, `teamName`, `job`, `gender`, `hair`, `skinColor`, `level`,
	`slot`, `barrackLayer`, `bx`, `by`, `bz`, `bd`, `bd`, `zone`, `x`, `y`, `z`, `bd`, `exp`, `maxExp`,
	`totalExp`, `hp`, `hpRate`, `maxHp`, `sp`, `spRate`, `maxSp`, `stamina`, `staminaByJob`,
	`maxStamina`, `str`, `strByJob`, `con`, `conByJob`, `int`, `intByJob`, `spr`, `sprByJob`, `dex`,
	`dexByJob`, `statByLevel`, `statByBonus`, `usedStat`, `abilityPoints`, `silver`,
	`equipVisibility`
FROM melia.characters;

REPLACE INTO laima_local.items
(
	itemUniqueId, itemId, amount, locked
)
SELECT
	itemUniqueId, itemId, amount, 0
FROM melia.items
WHERE itemUniqueId IN (SELECT itemId FROM source_item_ids);

REPLACE INTO laima_local.account_properties
SELECT *
FROM melia.account_properties
WHERE accountId IN (SELECT accountId FROM source_accounts);

REPLACE INTO laima_local.abilities
SELECT *
FROM melia.abilities
WHERE characterId IN (SELECT characterId FROM source_characters);

REPLACE INTO laima_local.buffs
(
	buffId, characterId, classId, numArg1, numArg2, numArg3, numArg4, numArg5,
	duration, runTime, skillId, overbuffCount
)
SELECT
	buffId, characterId, classId, numArg1, numArg2, 0, 0, 0,
	duration, runTime, skillId, overbuffCount
FROM melia.buffs
WHERE characterId IN (SELECT characterId FROM source_characters);

REPLACE INTO laima_local.character_etc_properties
SELECT *
FROM melia.character_etc_properties
WHERE characterId IN (SELECT characterId FROM source_characters);

REPLACE INTO laima_local.character_properties
SELECT *
FROM melia.character_properties
WHERE characterId IN (SELECT characterId FROM source_characters);

REPLACE INTO laima_local.chatmacros
SELECT *
FROM melia.chatmacros
WHERE accountId IN (SELECT accountId FROM source_accounts);

REPLACE INTO laima_local.collections
SELECT *
FROM melia.collections
WHERE accountId IN (SELECT accountId FROM source_accounts);

REPLACE INTO laima_local.collection_items
SELECT *
FROM melia.collection_items
WHERE accountId IN (SELECT accountId FROM source_accounts);

REPLACE INTO laima_local.cooldowns
SELECT *
FROM melia.cooldowns
WHERE characterId IN (SELECT characterId FROM source_characters);

REPLACE INTO laima_local.friends
SELECT *
FROM melia.friends
WHERE userId IN (SELECT accountId FROM source_accounts)
   OR friendUserId IN (SELECT accountId FROM source_accounts);

REPLACE INTO laima_local.inventory
SELECT *
FROM melia.inventory
WHERE characterId IN (SELECT characterId FROM source_characters);

REPLACE INTO laima_local.jobs
(
	characterId, jobId, circle, skillPoints, totalExp, selectionDate, advDate
)
SELECT
	characterId, jobId, circle, skillPoints, totalExp, selectionDate, selectionDate
FROM melia.jobs
WHERE characterId IN (SELECT characterId FROM source_characters);

REPLACE INTO laima_local.likes
SELECT *
FROM melia.likes
WHERE receiverId IN (SELECT characterId FROM source_characters)
   OR senderId IN (SELECT characterId FROM source_characters);

REPLACE INTO laima_local.mail
SELECT *
FROM melia.mail
WHERE accountId IN (SELECT accountId FROM source_accounts);

REPLACE INTO laima_local.mail_items
SELECT *
FROM melia.mail_items
WHERE mailId IN (SELECT mailId FROM source_mail_ids);

REPLACE INTO laima_local.quests
SELECT *
FROM melia.quests
WHERE characterId IN (SELECT characterId FROM source_characters);

REPLACE INTO laima_local.quests_progress
SELECT *
FROM melia.quests_progress
WHERE characterId IN (SELECT characterId FROM source_characters);

REPLACE INTO laima_local.revealedmaps
SELECT *
FROM melia.revealedmaps
WHERE accountId IN (SELECT accountId FROM source_accounts);

REPLACE INTO laima_local.session_objects_properties
SELECT *
FROM melia.session_objects_properties
WHERE characterId IN (SELECT characterId FROM source_characters);

REPLACE INTO laima_local.skills
SELECT *
FROM melia.skills
WHERE characterId IN (SELECT characterId FROM source_characters);

REPLACE INTO laima_local.social_users
SELECT *
FROM melia.social_users
WHERE accountId IN (SELECT accountId FROM source_accounts);

REPLACE INTO laima_local.storage_personal
SELECT *
FROM melia.storage_personal
WHERE characterId IN (SELECT characterId FROM source_characters);

REPLACE INTO laima_local.storage_team
SELECT *
FROM melia.storage_team
WHERE accountId IN (SELECT accountId FROM source_accounts);

REPLACE INTO laima_local.vars_accounts
SELECT *
FROM melia.vars_accounts
WHERE accountId IN (SELECT accountId FROM source_accounts);

REPLACE INTO laima_local.vars_buffs
SELECT *
FROM melia.vars_buffs
WHERE buffId IN (SELECT buffId FROM source_buff_ids);

REPLACE INTO laima_local.vars_characters
SELECT *
FROM melia.vars_characters
WHERE characterId IN (SELECT characterId FROM source_characters);

SET FOREIGN_KEY_CHECKS=1;

ALTER TABLE laima_local.accounts AUTO_INCREMENT = 11;
ALTER TABLE laima_local.characters AUTO_INCREMENT = 16;
