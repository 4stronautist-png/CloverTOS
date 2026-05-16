# Papaya West Siauliai Trace - 2026-05-05

Source trace:

- `C:\CloverTOS-Captures\quest-traces\20260505-145952-papaya-west-siauliai-clean-new-character`
- Played segment: new character through the Laimonas quest pickup.

Observed network game flows:

- Login/handshake: `169.57.130.88:7019`
- Main zone/game flow: `169.57.130.89:7011`
- Secondary game channel: `169.57.130.88:9001`
- Social/aux channel: `169.57.130.90:9002`

Papaya IES source of truth for this segment:

1. `SIAUL_WEST_MEET_TITAS`
   - `QuestEndMode`: `SYSTEM`
   - `Track1`: `SProgress/ESuccess/SIAU_WEST_START_TRACK/1000`
   - Progress marker: `f_siauliai_west 660 260 -960 50`

2. `SIAUL_WEST_WEST_FOREST`
   - Starts/ends at `SIAUL_WEST_CAMP_MANAGER`
   - Available only after `SIAUL_WEST_MEET_TITAS`

3. `SIAUL_WEST_DRASIUS1`
   - Starts/ends at `SIALUL_WEST_DRASIUS`
   - Layer kill objective, official target `Onion`, count `4`

4. `SIAUL_WEST_STATUS_TUTO_1`
   - Starts/ends at `SIALUL_WEST_DRASIUS`
   - Success next quest: `SIAUL_WEST_DRASIUS2`

5. `SIAUL_WEST_DRASIUS2`
   - Starts/ends at `SIALUL_WEST_DRASIUS`
   - Progress marker: `f_siauliai_west -1649 260 -763 300`

6. `SIAUL_WEST_MEET_NAGLIS`
   - Starts/progresses/ends at `SIAUL_WEST_NAGLIS2`
   - Start location: `f_siauliai_west SIAUL_WEST_NAGLIS2 100`
   - Progress marker: `f_siauliai_west -1490 260 -140 100`
   - Layer kill objective, official target `Onion_Big`, count `1`
   - Success next quest: `TUTO_SKILL_RUN`

7. `TUTO_SKILL_RUN`
   - Starts/progresses/ends at `SIAUL_WEST_NAGLIS2`
   - Progress marker: `f_siauliai_west SIAUL_WEST_SOL3 125`
   - Required before `SIAUL_WEST_SOLDIER3`

8. `SIAUL_WEST_SOLDIER3`
   - Starts at `SIAUL_WEST_SOL3`
   - Start location: `f_siauliai_west SIAUL_WEST_SOL3 25`
   - Layer kill objective, target `Hanaming/Hanaming`, count `8`
   - Success next quest: `SIAUL_WEST_HAMING_LEAF`

9. `SIAUL_WEST_HAMING_LEAF`
   - System start
   - Ends at `SIAUL_WEST_SOL3`

10. `SIAUL_WEST_KNIGHT`
   - Starts at `SIAUL_WEST_SOL3`
   - Progresses/ends at `SIAUL_WEST_CAMP_MANAGER`

11. `SIAUL_WEST_LAIMONAS1`
   - Starts/ends at `SIAUL_WEST_LAIMONAS`
   - Progress marker: `f_siauliai_west 1751 285 349 500`

Important compatibility finding:

- NPC suppression must never hide an NPC that is relevant to the player's current active/success quest, even if the same NPC is also referenced by a future blocked main quest.
