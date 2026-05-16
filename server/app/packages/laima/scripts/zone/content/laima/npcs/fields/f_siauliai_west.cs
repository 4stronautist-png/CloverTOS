//--- Melia Script ----------------------------------------------------------
// West Siauliai Woods
//--- Description -----------------------------------------------------------
// NPCs found in and around West Siauliai Woods.
//---------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Melia.Shared.Game.Const;
using Melia.Shared.Scripting;
using Melia.Zone.Network;
using Melia.Zone.Events.Arguments;
using Melia.Zone.Scripting;
using Melia.Zone.Scripting.Dialogues;
using Melia.Zone.Scripting.Extensions.LivelyDialog;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.Monsters;
using Melia.Zone.World.Quests;
using Yggdrasil.Logging;
using static Melia.Zone.Scripting.Shortcuts;

public class FSiauliaiWestNpcScript : GeneralScript
{
	private const bool UseOpeningCutscene = true;
	private Npc _titasNpc;
	private Npc _scoutNpc;
	private Npc _battleCommanderNpc;
	private Npc _laimonasNpc;

	[On("PlayerReady")]
	public void OnPlayerReady(object sender, PlayerEventArgs args)
	{
		var character = args.Character;
		if (character == null || character.MapId != 1021)
			return;

		_ = this.InitializeOpeningFlow(character);
	}

	private async Task InitializeOpeningFlow(Character character)
	{
		await Task.Delay(1500);

		if (character.Connection == null || character.MapId != 1021)
			return;

		try
		{
			character.RestoreCoreHudState(true, true);
			character.Quests.RepairWestSiauliaiMainQuestState();
			ApplyStarterProgressCatchUp(character);
			RevealTitasIfNeeded(character);
			RevealScoutIfNeeded(character);
			RevealBattleCommanderIfNeeded(character);
			RevealLaimonasIfNeeded(character);

			if (HasEnteredWestSiauliaiStory(character))
				return;

			var introTrackCompleted = character.Etc.Properties.GetFloat(PropertyName.SIAUL_WEST_MEET_TITAS_TRACK) == 1;
			if (!introTrackCompleted)
			{
				Send.ZC_NORMAL.SetupCutscene(character, false, false, false);
				character.ShowHelp("TUTO_MOVE_KB");
				Log.Info("West Siauliai opening: movement tutorial triggered for '{0}'.", character.Name);
			}

			if (!character.Quests.TryGetById(1001, out var titasQuest) && !character.Quests.HasCompleted(1001))
			{
				character.Quests.HandleStaticNpcDialog("SIAUL_WEST_MEET_TITAS_AUTO");
				Log.Info("West Siauliai opening: triggered static start SIAUL_WEST_MEET_TITAS for '{0}'.", character.Name);
			}

			Send.ZC_NORMAL.SetupCutscene(character, false, false, false);
			RevealTitasIfNeeded(character);
			RevealScoutIfNeeded(character);
			RevealBattleCommanderIfNeeded(character);
			RevealLaimonasIfNeeded(character);
			QueueOpeningCutscene(character);

			if (!UseOpeningCutscene)
			{
				if (character.Quests.IsActive(1001))
				{
					character.Quests.Complete(1001);
					Log.Info("West Siauliai opening: completed intro trigger quest for '{0}' because the cutscene is disabled.", character.Name);
				}

				RevealTitasIfNeeded(character);
				RevealScoutIfNeeded(character);
				RevealBattleCommanderIfNeeded(character);
				RevealLaimonasIfNeeded(character);
				Log.Info("West Siauliai opening: skipping broken intro cutscene for '{0}' and keeping HUD/world controls active.", character.Name);
				character.LookAround();
				return;
			}
		}
		catch (Exception ex)
		{
			Log.Warning("West Siauliai opening failed for '{0}': {1}", character.Name, ex);
		}
	}

	private static void QueueOpeningCutscene(Character character)
	{
		if (character == null || character.MapId != 1021)
			return;
		if (!UseOpeningCutscene)
			return;
		if (HasEnteredWestSiauliaiStory(character) || character.Tracks.ActiveTrack != null)
			return;
		if (character.Etc.Properties.GetFloat(PropertyName.SIAUL_WEST_MEET_TITAS_TRACK) == 1)
		{
			_ = EnsureWestForestFallback(character);
			return;
		}

		_ = Task.Run(async () =>
		{
			await Task.Delay(12000);

			if (character.Connection == null || character.MapId != 1021)
				return;
			if (HasEnteredWestSiauliaiStory(character) || character.Tracks.ActiveTrack != null)
				return;
			if (character.Etc.Properties.GetFloat(PropertyName.SIAUL_WEST_MEET_TITAS_TRACK) == 1)
			{
				await EnsureWestForestFallback(character);
				return;
			}

			try
			{
				Send.ZC_NORMAL.SetupCutscene(character, false, false, false);

				var started = await character.Tracks.Start(
					"SIAU_WEST_START_TRACK",
					TimeSpan.Zero,
					0,
					QuestStatus.Possible,
					QuestStatus.Possible,
					PropertyName.SIAUL_WEST_MEET_TITAS_TRACK
				);

				if (started)
					Log.Info("West Siauliai opening: started SIAU_WEST_START_TRACK for '{0}' after basic command tutorial.", character.Name);
				else
					await EnsureWestForestFallback(character);
			}
			catch (Exception ex)
			{
				Log.Warning("West Siauliai opening: automatic intro track failed for '{0}': {1}", character.Name, ex);
				await EnsureWestForestFallback(character);
			}
		});
	}

	private static bool HasEnteredWestSiauliaiStory(Character character)
	{
		return
			character.Quests.IsActive(1002) ||
			character.Quests.HasCompleted(1002) ||
			character.Quests.IsActive(1003) ||
			character.Quests.HasCompleted(1003) ||
			character.Quests.IsActive(1004) ||
			character.Quests.HasCompleted(1004) ||
			character.Quests.IsActive(1014) ||
			character.Quests.HasCompleted(1014) ||
			character.Quests.IsActive(1020) ||
			character.Quests.HasCompleted(1020) ||
			character.Quests.IsActive(1021) ||
			character.Quests.HasCompleted(1021) ||
			character.Quests.IsActive(1013) ||
			character.Quests.HasCompleted(1013) ||
			character.Quests.IsActive(1015) ||
			character.Quests.HasCompleted(1015);
	}

	private static async Task EnsureWestForestFallback(Character character)
	{
		if (character == null || character.MapId != 1021)
			return;

		if (character.Quests.IsActive(1001) && !character.Quests.HasCompleted(1001))
		{
			character.Quests.Complete(1001);
			Log.Info("West Siauliai opening: completed SIAUL_WEST_MEET_TITAS for '{0}' via intro fallback.", character.Name);
		}

		if (character.Quests.HasCompleted(1001) && !character.Quests.IsActive(1002) && !character.Quests.HasCompleted(1002))
			Log.Info("West Siauliai opening: SIAUL_WEST_WEST_FOREST is available at Titas for '{0}' via intro fallback.", character.Name);

		character.RestoreCoreHudState(true, true);
		character.Quests.SyncStaticQuestNpcStates();
	}

	private static void ApplyStarterProgressCatchUp(Character character)
	{
		var targetLevel = 1;

		if (character.Quests.HasCompleted(1002))
			targetLevel = Math.Max(targetLevel, 2);
		if (character.Quests.HasCompleted(1003) || character.Quests.HasCompleted(20127))
			targetLevel = Math.Max(targetLevel, 3);
		if (character.Quests.HasCompleted(1004) || character.Quests.IsActive(1014) || character.Quests.HasCompleted(1014))
			targetLevel = Math.Max(targetLevel, 4);
		if (character.Quests.HasCompleted(8350) || character.Quests.IsActive(1020) || character.Quests.HasCompleted(1020))
			targetLevel = Math.Max(targetLevel, 5);

		EnsureMinimumLevel(character, targetLevel);
	}

	private static void EnsureMinimumLevel(Character character, int targetLevel)
	{
		for (var guard = 0; character.Level < targetLevel && guard < 20; guard++)
		{
			var expNeeded = Math.Max(1, character.MaxExp - character.Exp);
			var jobExpNeeded = Math.Max(1, character.Job?.MaxExp ?? expNeeded);
			character.GiveExp(expNeeded, jobExpNeeded, null);
		}
	}

	protected override void Load()
	{
		_scoutNpc = AddNpc(100300, 10032, L("Scout"), "f_siauliai_west", -1121, 260, -528, -99, "SIALUL_WEST_DRASIUS");

		// Search Scout
		//-------------------------------------------------------------------------
		// Naglis is part of the main Large Kepa handoff. Side quest NPCs remain
		// disabled, but this actor must exist so SIAUL_WEST_MEET_NAGLIS can be
		// turned in before the Battle Commander chain opens.
		AddNpc(100301, 20016, L("Search Scout"), "f_siauliai_west", -1490, 260, -140, 0, "SIAUL_WEST_NAGLIS2", state: (int)NpcState.Invisible);

		// Opening quest trigger
		//-------------------------------------------------------------------------
		// The first West Siauliai quest starts from an invisible enter trigger,
		// which kicks off the Titas intro chain after the movement tutorial.
		AddNpc(55, 20041, "", "f_siauliai_west", -560, 260, -780, 0, "", "SIAUL_WEST_MEET_TITAS_AUTO", "", (int)NpcState.Invisible, 25);

		// Knight Titas / Camp Manager
		//-------------------------------------------------------------------------
		// Client mongen maps Titas to npc_intermediate_officer_men.
		_titasNpc = AddNpc(7, 20107, L("Knight Titas"), "f_siauliai_west", -576, 260, -719, 165, "SIAUL_WEST_CAMP_MANAGER", state: (int)NpcState.Invisible);

		// Battle Commander
		//-------------------------------------------------------------------------
		_battleCommanderNpc = AddNpc(2002, 20016, L("Battle Commander"), "f_siauliai_west", -663, 322, 503, 0, "SIAUL_WEST_SOL3", state: (int)NpcState.Invisible);

		// Laimonas
		//-------------------------------------------------------------------------
		_laimonasNpc = AddNpc(9, 20117, L("Laimonas"), "f_siauliai_west", 326.508606, 210.211899, -346.852936, -90, "SIAUL_WEST_LAIMONAS", state: (int)NpcState.Normal);

		// Klaipeda road handoff, revealed after Laimonas' favor.
		AddNpc(1019, 20016, L("Klaipeda Guard Captain"), "f_siauliai_west", 1880, 210, -1175, 0, "SIAUL_ST1_ST2", state: (int)NpcState.Invisible);

		// Western Woods Scout / Drasius
		//-------------------------------------------------------------------------
		// Drasius is revealed per character after the Titas handoff. The shared
		// NPC state helper sends both the actor and the effective state.
		AddAreaTrigger("f_siauliai_west", -1121, -528, 300, async triggerArgs =>
		{
			if (triggerArgs.Initiator is not Character character)
				return;

			Log.Info(
				"West Siauliai scout trigger: '{0}' entered reveal area. layer={1}, q1002={2}, q1002Done={3}, q1003={4}, q1003Done={5}, q1004={6}, q1014={7}, scoutState={8}",
				character.Name,
				character.Layer,
				character.Quests.IsActive(1002),
				character.Quests.HasCompleted(1002),
				character.Quests.IsActive(1003),
				character.Quests.HasCompleted(1003),
				character.Quests.IsActive(1004),
				character.Quests.IsActive(1014),
				character.GetMapNPCState(_scoutNpc)
			);

			RevealScoutIfNeeded(character);

			await Task.CompletedTask;
		});

		// Statue of Goddess Vakarine
		//-------------------------------------------------------------------------
		AddNpc(4, 40120, "Statue of Goddess Vakarine", "f_siauliai_west", -525, 260, -435, 0, "WARP_F_SIAULIAI_WEST", "STOUP_CAMP", "STOUP_CAMP");

		// Statue of Goddess Zemyna
		//-------------------------------------------------------------------------
		AddNpc(2026, 20026, "Statue of Goddess Zemyna", "f_siauliai_west", 1705.19, 285.05, 390.19, 90, "", "SIAUL_WEST_LAIMONAS3_TRIGGER", "");

		// Lv1 Treasure Chest
		//-------------------------------------------------------------------------
		AddNpc(2027, 147392, "Lv1 Treasure Chest", "f_siauliai_west", 1564, 210, -370, 270, "TREASUREBOX_LV_F_SIAULIAI_WEST2027", "", "");

		// Statue of Goddess Zemyna
		//-------------------------------------------------------------------------
		AddNpc(2029, 40110, "Statue of Goddess Zemyna", "f_siauliai_west", 1687, 285.05, 366, 20, "F_SIAULIAI_WEST_EV_55_001", "F_SIAULIAI_WEST_EV_55_001", "F_SIAULIAI_WEST_EV_55_001");

		// Lv1 Treasure Chest
		//-------------------------------------------------------------------------
		AddNpc(2032, 147392, "Lv1 Treasure Chest", "f_siauliai_west", -580, 260, -1417, 180, "TREASUREBOX_LV_F_SIAULIAI_WEST2032", "", "");

		// Lv3 Treasure Chest (Cow Headband)
		//-------------------------------------------------------------------------
		AddNpc(2035, 147393, "Lv3 Treasure Chest", "f_siauliai_west", 185.81, 210.31, -856.9, 90, "TREASUREBOX_LV_F_SIAULIAI_WEST2035", "", "");

		// Lv1 Treasure Chest
		//-------------------------------------------------------------------------
		AddNpc(2036, 147392, "Lv1 Treasure Chest", "f_siauliai_west", 1346.05, 210.31, -1087.24, 90, "TREASUREBOX_LV_F_SIAULIAI_WEST2036", "", "");
	}

	private void RevealScoutIfNeeded(Character character)
	{
		if (_scoutNpc == null || character == null || character.Connection == null)
			return;

		var shouldReveal =
			character.Quests.HasCompleted(1002) ||
			character.Quests.IsActive(1003) ||
			character.Quests.HasCompleted(1003) ||
			character.Quests.IsActive(1004) ||
			character.Quests.HasCompleted(1004) ||
			character.Quests.IsActive(1014);

		if (!shouldReveal || character.GetMapNPCState(_scoutNpc) == NpcState.Normal)
			return;

		character.SetMapNPCState(_scoutNpc, NpcState.Normal);
		Log.Info("West Siauliai scout reveal: revealed Drasius for '{0}'.", character.Name);
	}

	private void RevealBattleCommanderIfNeeded(Character character)
	{
		if (_battleCommanderNpc == null || character == null || character.Connection == null)
			return;

		var shouldReveal =
			character.Quests.HasCompleted(1014) ||
			character.Quests.IsActive(1020) ||
			character.Quests.HasCompleted(1020) ||
			character.Quests.IsActive(1021) ||
			character.Quests.HasCompleted(1021) ||
			character.Quests.IsActive(1013) ||
			character.Quests.HasCompleted(1013);

		if (!shouldReveal)
			return;

		var shouldHighlight =
			character.Quests.IsActive(1021) ||
			character.Quests.HasCompleted(1014) && !character.Quests.IsActive(1020) && !character.Quests.HasCompleted(1020) ||
			character.Quests.HasCompleted(1021) && !character.Quests.IsActive(1013) && !character.Quests.HasCompleted(1013);
		var desiredState = shouldHighlight ? NpcState.Highlighted : NpcState.Normal;

		if (character.GetMapNPCState(_battleCommanderNpc) == desiredState)
			return;

		character.SetMapNPCState(_battleCommanderNpc, desiredState);
		Log.Info("West Siauliai Battle Commander reveal: revealed commander for '{0}' with state {1}.", character.Name, desiredState);
	}

	private void RevealLaimonasIfNeeded(Character character)
	{
		if (_laimonasNpc == null || character == null || character.Connection == null)
			return;

		var shouldReveal =
			character.Quests.HasCompleted(1013) ||
			character.Quests.IsActive(1015) ||
			character.Quests.HasCompleted(1015);

		if (!shouldReveal)
			return;

		var shouldHighlight =
			character.Quests.HasCompleted(1013) && !character.Quests.IsActive(1015) && !character.Quests.HasCompleted(1015) ||
			character.Quests.IsActive(1015);
		var desiredState = shouldHighlight ? NpcState.Highlighted : NpcState.Normal;

		character.SetMapNPCState(_laimonasNpc, desiredState);
		Log.Info("West Siauliai Laimonas reveal: revealed Laimonas for '{0}' with state {1}.", character.Name, desiredState);
	}

	private void RevealTitasIfNeeded(Character character)
	{
		if (_titasNpc == null || character == null || character.Connection == null)
			return;

		var westForestAvailable =
			character.Quests.HasCompleted(1001) &&
			!character.Quests.IsActive(1002) &&
			!character.Quests.HasCompleted(1002);
		var shouldReveal =
			westForestAvailable ||
			character.Quests.IsActive(1002) ||
			character.Quests.HasCompleted(1002) ||
			character.Quests.IsActive(1003) ||
			character.Quests.HasCompleted(1003) ||
			character.Quests.IsActive(1004);

		if (!shouldReveal)
			return;

		var desiredState = westForestAvailable || character.Quests.IsActive(1002) ? NpcState.Highlighted : NpcState.Normal;
		if (character.GetMapNPCState(_titasNpc) == desiredState)
			return;

		character.SetMapNPCState(_titasNpc, desiredState);
		Log.Info("West Siauliai Titas reveal: revealed Titas for '{0}' with state {1}.", character.Name, desiredState);
	}
}
