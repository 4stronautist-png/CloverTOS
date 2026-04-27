SET @slot := 0;
SET @accountId := 0;

UPDATE characters c
JOIN (
	SELECT
		characterId,
		@slot := IF(@accountId = accountId, @slot + 1, 1) AS repairedSlot,
		@accountId := accountId AS accountMarker
	FROM characters
	ORDER BY accountId, IF(slot IS NULL OR slot < 1, 999, slot), characterId
) fixed ON fixed.characterId = c.characterId
SET c.slot = fixed.repairedSlot
WHERE c.slot <> fixed.repairedSlot;

UPDATE accounts a
LEFT JOIN (
	SELECT accountId, MIN(slot) AS firstSlot
	FROM characters
	GROUP BY accountId
) firstCharacter ON firstCharacter.accountId = a.accountId
SET a.selectedSlot = COALESCE(firstCharacter.firstSlot, 0)
WHERE a.selectedSlot IS NULL
	OR a.selectedSlot < 1
	OR NOT EXISTS (
		SELECT 1
		FROM characters c
		WHERE c.accountId = a.accountId
			AND c.slot = a.selectedSlot
	);

UPDATE accounts
SET authority = 99,
	type = 3
WHERE name IN ('gotei', 'anzinha');
