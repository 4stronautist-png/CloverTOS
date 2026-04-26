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
using Melia.Zone.World.Quests;
using Yggdrasil.Logging;
using static Melia.Zone.Scripting.Shortcuts;

public class FSiauliaiWestNpcScript : GeneralScript
{
	[On("PlayerReady")]
	public void OnPlayerReady(object sender, PlayerEventArgs args)
	{
		var character = args.Character;
		if (character == null || character.MapId != 1021 || character.Level > 1)
			return;

		_ = this.InitializeOpeningFlow(character);
	}

	private async Task InitializeOpeningFlow(Character character)
	{
		await Task.Delay(1500);

		if (character.Connection == null || character.MapId != 1021 || character.Level > 1)
			return;

		try
		{
			if (character.Variables.Perm.ActivateOnce("Clover.Tutorial.Movement.WestSiauliai"))
			{
				character.ShowHelp("TUTO_MOVE_KB", true);
				Log.Info("West Siauliai opening: movement tutorial triggered for '{0}'.", character.Name);
			}

			if (!character.Quests.TryGetById(1001, out var titasQuest))
			{
				character.Quests.Start("SIAUL_WEST_MEET_TITAS");
				Log.Info("West Siauliai opening: started quest SIAUL_WEST_MEET_TITAS for '{0}'.", character.Name);
			}

			await Task.Delay(2500);

			if (character.Connection == null || character.MapId != 1021 || character.Level > 1)
				return;

			if (!character.Quests.TryGetById(1001, out titasQuest))
				return;
			if (titasQuest.Status == QuestStatus.Completed)
				return;
			if (character.Etc.Properties.GetFloat(PropertyName.SIAUL_WEST_MEET_TITAS_TRACK) == 1)
				return;
			if (character.Tracks.ActiveTrack != null)
				return;

			await character.Tracks.Start("SIAU_WEST_START_TRACK", TimeSpan.Zero, 1001, QuestStatus.InProgress, QuestStatus.Completed, PropertyName.SIAUL_WEST_MEET_TITAS_TRACK);
			Log.Info("West Siauliai opening: started SIAU_WEST_START_TRACK for '{0}'.", character.Name);
		}
		catch (Exception ex)
		{
			Log.Warning("West Siauliai opening failed for '{0}': {1}", character.Name, ex);
		}
	}

	protected override void Load()
	{
		var scoutNpc = AddNpc(11, 20019, L("Scout"), "f_siauliai_west", -1649, 260, -763, 90, "SIALUL_WEST_DRASIUS", state: (int)NpcState.Invisible);

		// Opening quest trigger
		//-------------------------------------------------------------------------
		// The first West Siauliai quest starts from an invisible enter trigger,
		// which kicks off the Titas intro chain after the movement tutorial.
		AddNpc(24001, 20117, "", "f_siauliai_west", -643, 260, -948, 0, "", "SIAUL_WEST_MEET_TITAS_AUTO", "", (int)NpcState.Invisible, 25);

		// Knight Titas / Camp Manager
		//-------------------------------------------------------------------------
		// The extracted client data ties Knight Titas to the original West
		// Siauliai camp setup and uses the knight-style NPC presentation rather
		// than the later kingdom guard model. Keep the stable genType, but align
		// the static map actor with the actual opening camp position.
		AddNpc(77, 20113, L("Knight Titas"), "f_siauliai_west", -652, 260, -952, 180, "SIAUL_WEST_CAMP_MANAGER");

		// Battle Commander
		//-------------------------------------------------------------------------
		AddNpc(2002, 150219, L("Battle Commander"), "f_siauliai_west", -144, 322, 444, 180, "SIAUL_WEST_SOL3");

		// Western Woods Scout / Drasius
		//-------------------------------------------------------------------------
		// In the original flow the scout is revealed only when the player gets
		// close to the marked area after the Titas handoff. Keep the NPC hidden
		// by default and unhide it per-character on proximity for the active
		// quest chain.
		AddAreaTrigger("f_siauliai_west", -1649, -763, 300, async triggerArgs =>
		{
			if (triggerArgs.Initiator is not Character character)
				return;

			var shouldReveal =
				character.Quests.IsActive(1002) ||
				character.Quests.HasCompleted(1002) ||
				character.Quests.IsActive(1003) ||
				character.Quests.HasCompleted(1003) ||
				character.Quests.IsActive(1004) ||
				character.Quests.IsActive(1014);

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
				character.GetMapNPCState(scoutNpc)
			);

			if (!shouldReveal)
				return;

			if (character.GetMapNPCState(scoutNpc) != NpcState.Normal)
			{
				character.SetMapNPCState(scoutNpc, NpcState.Normal);
				Send.ZC_ENTER_MONSTER(character.Connection, scoutNpc);
				Send.ZC_SET_NPC_STATE(character.Connection, scoutNpc, (short)NpcState.Normal);
				character.LookAround();
				Log.Info("West Siauliai scout reveal: revealed Drasius for '{0}'.", character.Name);
			}

			await Task.CompletedTask;
		});

		// Search Scout Naglis
		//-------------------------------------------------------------------------
		AddNpc(33, 20117, L("Search Scout Naglis"), "f_siauliai_west", -1490, 260, -140, 90, "SIAUL_WEST_NAGLIS2");

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
}
