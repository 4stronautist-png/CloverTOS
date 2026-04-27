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

		// Use a knight-style NPC model here, not the generic kingdom guard.
		// The previous actor id (150219) rendered as a Royal Army Guard, which
		// is why the cutscene showed an extra guard instead of Titas.
		var titas = SpawnCutsceneNpc(character, 25001, 20113, "Knight Titas", -652, 260, -952, 180, "SIAUL_WEST_CAMP_MANAGER");
		actors.Add(titas);

		var guardLeft = SpawnCutsceneNpc(character, 25002, 40202, "Sentinel", -666, 260, -962, 135);
		actors.Add(guardLeft);

		var guardRight = SpawnCutsceneNpc(character, 25003, 40202, "Sentinel", -639, 260, -938, 225);
		actors.Add(guardRight);

		Log.Info(
			"SIAU_WEST_START_TRACK: spawned actors for '{0}' on layer {1} -> Titas={2}, GuardL={3}, GuardR={4}, ActorCount={5}",
			character.Name,
			character.Layer,
			titas.Handle,
			guardLeft.Handle,
			guardRight.Handle,
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

		Send.ZC_NORMAL.SetupCutscene(character, false, false, false);

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
