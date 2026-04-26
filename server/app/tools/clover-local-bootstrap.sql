CREATE DATABASE IF NOT EXISTS `laima_local` DEFAULT CHARACTER SET utf8 COLLATE utf8_general_ci;

DROP TABLE IF EXISTS `laima_local`.`accounts_seed`;

USE `laima_local`;

DELETE FROM `accounts`;
INSERT INTO `accounts`
(
  `accountId`,
  `name`,
  `password`,
  `sessionKey`,
  `teamName`,
  `authority`,
  `settings`,
  `medals`,
  `giftMedals`,
  `premiumMedals`,
  `additionalSlotCount`,
  `teamExp`,
  `barracksThema`,
  `themas`,
  `selectedSlot`,
  `loginState`,
  `loginCharacter`,
  `premiumTokenExpiration`,
  `language`,
  `lastLogin`
)
SELECT
  `accountId`,
  `name`,
  `password`,
  NULL,
  NULL,
  `authority`,
  `settings`,
  0,
  0,
  0,
  0,
  0,
  11,
  '11',
  0,
  0,
  0,
  NULL,
  'English',
  NULL
FROM `melia`.`accounts`;
