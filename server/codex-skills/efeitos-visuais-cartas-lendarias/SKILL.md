---
name: efeitos-visuais-cartas-lendarias
description: Add or adjust CloverTOS legendary card visual effects. Use when the user wants to map legendary card IDs to cosmetic item IDs, change an existing legendary card visual, add doll/wing/hair/effect costume visuals, preserve the ON/OFF switch behavior in the F2 Card Album, or fix conflicts between card visuals and real equipped items.
---

# Efeitos Visuais Das Cartas Lendarias

Use this skill to add or change visual effects granted by legendary cards in CloverTOS.

## Core Files

- Zone runtime mapping: `app/src/ZoneServer/World/Actors/Characters/Components/InventoryComponent.cs`
- Barracks character selection mapping: `app/src/BarracksServer/Database/Character.cs`
- Card Album switch client script: `app/packages/laima/scripts/zone/custom/client/legend_card_visual_toggle/`
- Login/initial appearance refresh: `app/src/ZoneServer/Network/PacketHandler.cs`

## Add Or Change A Card Visual

1. In `InventoryComponent.cs`, update `LegendCardVisualEffects`.
2. In `Character.cs`, update the Barracks `LegendCardVisualEffects` with the same card ID, item ID, and slot.
3. Pick the correct `EquipSlot`:
   - Wings: `EquipSlot.Wing`
   - Dolls: `EquipSlot.Doll`
   - Hair accessories: `EquipSlot.HairAccessory`
   - Effect costumes: `EquipSlot.EffectCostume`
4. For dolls, include the doll buff class name in Zone mapping:
   - Boruta doll `900023`: `DOLL_BORUTA_BUFF`
   - Paulius/Linus doll `900018`: `DOLL_PAULIUS_BUFF`
5. Do not make the feature exclusive to one card. Keep mappings data-driven in the dictionary.

Example Zone mapping:

```csharp
[644914] = new(900023, EquipSlot.Doll, "DOLL_BORUTA_BUFF"),
[644940] = new(11105013, EquipSlot.Wing),
```

Example Barracks mapping:

```csharp
[644914] = new(900023, EquipSlot.Doll),
[644940] = new(11105013, EquipSlot.Wing),
```

## Preserve Override Rules

- If the switch is OFF, do not apply the card visual.
- If no supported legendary card is equipped, keep the switch OFF.
- If a real item is equipped in the same visual slot, the real item must override the card visual.
- If the real item is removed and the switch is ON, restore the card visual.
- For dolls, never remove a doll buff if a real equipped doll in `EquipSlot.Doll` uses that same buff. This prevents real dolls from disappearing after movement or refresh.
- Keep the visual active in character selection by updating the Barracks mapping too.

## Validate

Run targeted builds before restarting:

```bash
dotnet build ./app/src/ZoneServer/ZoneServer.csproj --no-restore
dotnet build ./app/src/BarracksServer/BarracksServer.csproj --no-restore
```

If server restart is needed, use:

```bash
cd /home/an/CloverTOS/server && scripts/down.sh
cd /home/an/CloverTOS/server/app && LOCAL_HOST=172.31.136.69 ./start-server.sh
```

After restarting, tell the user to close the game completely and reopen:

```text
C:\CloverTOS-Local\release\Start-CloverTOS-Local.bat
```
