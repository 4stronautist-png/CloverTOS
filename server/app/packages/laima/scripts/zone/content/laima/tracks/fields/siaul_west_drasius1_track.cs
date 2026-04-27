using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Melia.Shared.Game.Const;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Scripting;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.CombatEntities.Components;
using Melia.Zone.World.Actors.Monsters;
using Melia.Zone.World.Quests;
using Melia.Zone.World.Tracks;
using Yggdrasil.Logging;

[TrackScript("SIAUL_WEST_DRASIUS1_TRACK")]
public class SiaulWestDrasius1TrackScript : TrackScript
{
	protected override void Load()
	{
		SetId("SIAUL_WEST_DRASIUS1_TRACK");
		SetPropertyId(PropertyName.SIAUL_WEST_DRASIUS1_TRACK);
		SetCancelable(false);
	}

	public override IActor[] OnStart(Character character, Track track)
	{
		base.OnStart(character, track);

		var actors = new List<IActor>();

		actors.Add(SpawnCutsceneNpc(character, 1, 20063, "Scout", -1121, 260, -528, -99, "SIALUL_WEST_DRASIUS"));
		actors.Add(SpawnCutsceneKepa(character, 400001, "Kepa", -1168, 260, -579, 40));
		actors.Add(SpawnCutsceneKepa(character, 400001, "Kepa", -1072, 260, -594, -35));
		actors.Add(SpawnCutsceneKepa(character, 400001, "Kepa", -1216, 260, -492, 90));
		actors.Add(SpawnCutsceneKepa(character, 400001, "Kepa", -1024, 260, -471, -90));

		Log.Info(
			"SIAUL_WEST_DRASIUS1_TRACK: started Scout/Kepa encounter for '{0}' on layer {1}. ActorCount={2}",
			character.Name,
			character.Layer,
			actors.Count
		);

		_ = Task.Run(async () =>
		{
			await Task.Delay(TimeSpan.FromSeconds(4));

			if (character.Connection == null)
				return;
			if (character.Tracks.ActiveTrack?.Id != track.Id)
				return;

			Log.Warning(
				"SIAUL_WEST_DRASIUS1_TRACK fallback: force-ending Scout/Kepa track for '{0}' on layer {1}.",
				character.Name,
				character.Layer
			);

			character.Tracks.End(track.Id);
		});

		return actors.ToArray();
	}

	public override void OnComplete(Character character, Track track)
	{
		base.OnComplete(character, track);

		Send.ZC_NORMAL.SetupCutscene(character, false, false, false);
		SpawnCombatKepas(character);
		character.LookAround();
	}

	private static Npc SpawnCutsceneNpc(Character character, int genType, int monsterId, string name, double x, double y, double z, double direction, string dialogName)
	{
		var npc = Shortcuts.AddNpc(genType, monsterId, name, character.Map.ClassName, x, y, z, direction, dialogName);
		npc.SetVisibilty(ActorVisibility.Track, character.ObjectId);
		npc.Layer = character.Layer;
		Send.ZC_ENTER_MONSTER(character.Connection, npc);

		return npc;
	}

	private static Mob SpawnCutsceneKepa(Character character, int monsterId, string name, double x, double y, double z, double direction)
	{
		var mob = new Mob(monsterId, RelationType.Enemy)
		{
			Name = name,
			Position = new Position((float)x, (float)y, (float)z),
			Direction = new Direction(direction),
			Layer = character.Layer,
		};
		mob.SpawnPosition = mob.Position;
		mob.SetVisibilty(ActorVisibility.Track, character.ObjectId);
		mob.Components.Add(new AiComponent(mob, "BasicMonster"));
		character.Map.AddMonster(mob);
		Send.ZC_ENTER_MONSTER(character.Connection, mob);

		return mob;
	}

	private static void SpawnCombatKepas(Character character)
	{
		SpawnCombatKepa(character, 24111, -1168, 260, -579, 40);
		SpawnCombatKepa(character, 24112, -1072, 260, -594, -35);
		SpawnCombatKepa(character, 24113, -1216, 260, -492, 90);
		SpawnCombatKepa(character, 24114, -1024, 260, -471, -90);
	}

	private static void SpawnCombatKepa(Character character, int genType, double x, double y, double z, double direction)
	{
		var mob = Shortcuts.AddMonster(genType, 400001, "Kepa", character.Map.ClassName, x, y, z, direction);
		mob.Layer = character.Layer;
		mob.SpawnPosition = mob.Position;
		mob.InsertHate(character);
		mob.Tendency = TendencyType.Aggressive;

		if (character.Connection != null)
			Send.ZC_ENTER_MONSTER(character.Connection, mob);
	}
}
