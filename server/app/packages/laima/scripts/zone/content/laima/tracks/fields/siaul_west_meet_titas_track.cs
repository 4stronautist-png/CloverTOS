using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Melia.Shared.Game.Const;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Scripting;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.Monsters;
using Melia.Zone.World.Quests;
using Melia.Zone.World.Tracks;
using Yggdrasil.Logging;

[TrackScript("SIAU_WEST_START_TRACK")]
public class SiaulWestMeetTitasTrackScript : TrackScript
{
	protected override void Load()
	{
		SetId("SIAU_WEST_START_TRACK");
		SetPropertyId(PropertyName.SIAUL_WEST_MEET_TITAS_TRACK);
		SetCancelable(false);
	}

	public override IActor[] OnStart(Character character, Track track)
	{
		base.OnStart(character, track);

		var actors = new List<IActor>();
		const int titasNpcId = 20107;       // npc_intermediate_officer_men
		const int frontGuardNpcId = 20016;  // soldier6
		const int guardNpcId = 10032;       // Silvertransporter_m

		// Actor templates come from the client mongen for f_siauliai_west.
		var titas = SpawnCutsceneNpc(character, 7, titasNpcId, "Knight Titas", -576, 260, -719, 165, "SIAUL_WEST_CAMP_MANAGER");
		actors.Add(titas);

		var frontGuard = SpawnCutsceneNpc(character, 8, frontGuardNpcId, "Sentinel", -652, 260, -953, -90, "SIAU_FRON_NPC_01");
		actors.Add(frontGuard);

		var guard1 = SpawnCutsceneNpc(character, 51, guardNpcId, "Sentinel", -626, 260, -757, 86, "SIAU_FRON_NPC_04");
		actors.Add(guard1);

		var guard2 = SpawnCutsceneNpc(character, 52, guardNpcId, "Sentinel", -619, 260, -707, -75, "SIAU_FRON_NPC_05");
		actors.Add(guard2);

		var guard3 = SpawnCutsceneNpc(character, 53, guardNpcId, "Sentinel", -509, 260, -821, 161, "SIAU_FRON_NPC_03");
		actors.Add(guard3);

		var guard4 = SpawnCutsceneNpc(character, 54, guardNpcId, "Sentinel", -589, 260, -822, 0, "SIAU_FRON_NPC_02");
		actors.Add(guard4);

		Log.Info(
			"SIAU_WEST_START_TRACK: spawned actors for '{0}' on layer {1} -> Titas={2}, FrontGuard={3}, Guard51={4}, Guard52={5}, Guard53={6}, Guard54={7}, ActorCount={8}",
			character.Name,
			character.Layer,
			titas.Handle,
			frontGuard.Handle,
			guard1.Handle,
			guard2.Handle,
			guard3.Handle,
			guard4.Handle,
			actors.Count
		);

		// If the client fails to report the end of the intro cutscene cleanly,
		// the player remains stuck in the track layer with the HUD hidden and
		// the Scout chain never starts. The official client feels like it has a
		// small delay here anyway, so a short server-side fallback is safe.
		_ = Task.Run(async () =>
		{
			await Task.Delay(TimeSpan.FromSeconds(8));

			if (character.Connection == null)
				return;
			if (character.Tracks.ActiveTrack?.Id != track.Id)
				return;

			Log.Warning(
				"SIAU_WEST_START_TRACK fallback: force-ending intro track for '{0}' on layer {1}.",
				character.Name,
				character.Layer
			);

			character.Tracks.End(track.Id);
		});

		return actors.ToArray();
	}

	private static Npc SpawnCutsceneNpc(Character character, int genType, int monsterId, string name, double x, double y, double z, double direction, string dialogName = "")
	{
		var npc = Shortcuts.AddNpc(genType, monsterId, name, character.Map.ClassName, x, y, z, direction, dialogName);
		npc.SetVisibilty(ActorVisibility.Track, character.ObjectId);
		npc.Layer = character.Layer;

		// Track actors are created after the player already switched layers,
		// so they won't naturally be discovered by the last LookAround call.
		// Send them explicitly before StartCutscene so the client knows the
		// handles referenced by the cutscene packet.
		Send.ZC_ENTER_MONSTER(character.Connection, npc);

		return npc;
	}

	public override Task OnProgress(Character character, Track track, int frame)
		=> base.OnProgress(character, track, frame);

	public override void OnComplete(Character character, Track track)
	{
		base.OnComplete(character, track);

		character.RestoreCoreHudState(true, true);

		var westForestQuestId = new QuestId(1002);
		if (!character.Quests.IsActive(westForestQuestId) && !character.Quests.HasCompleted(westForestQuestId))
		{
			character.Quests.Start("SIAUL_WEST_WEST_FOREST");
			Log.Info("SIAU_WEST_START_TRACK: started SIAUL_WEST_WEST_FOREST for '{0}' after intro track.", character.Name);
		}

		RevealOpeningTitas(character);
		character.LookAround();
	}

	private static void RevealOpeningTitas(Character character)
	{
		if (character?.Map?.TryGetMonster(m => m is Npc npc && npc.DialogName == "SIAUL_WEST_CAMP_MANAGER", out var titasMonster) == true && titasMonster is Npc titasNpc)
		{
			if (character.GetMapNPCState(titasNpc) != NpcState.Normal)
				character.SetMapNPCState(titasNpc, NpcState.Normal);
		}
	}
}
