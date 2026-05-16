using System;
using System.Collections.Generic;
using System.Linq;
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
		RemoveTrackActorsFromClient(character, track);

		base.OnComplete(character, track);

		character.RestoreCoreHudState(true, true);
		QueueRealScoutEncounter(character, true);
	}

	public override void OnCancel(Character character, Track track)
	{
		RemoveTrackActorsFromClient(character, track);
		base.OnCancel(character, track);
		QueueRealScoutEncounter(character, false);
	}

	private static void QueueRealScoutEncounter(Character character, bool spawnKepas)
	{
		if (character?.Map == null)
			return;

		_ = Task.Run(async () =>
		{
			for (var attempt = 0; attempt < 8; attempt++)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(250));

				if (character.Connection == null || character.MapId != 1021)
					return;

				if (character.Tracks.ActiveTrack == null)
					break;
			}

			if (character.Connection == null || character.MapId != 1021 || character.Tracks.ActiveTrack != null)
				return;

			if (character.Layer != 0)
				character.StopLayer();

			if (spawnKepas)
				SpawnCombatKepas(character);

			ReenterRealScout(character);
			character.LookAround();
		});
	}

	private static void RemoveTrackActorsFromClient(Character character, Track track)
	{
		if (character?.Connection == null || track?.Actors == null)
			return;

		foreach (var actor in track.Actors)
		{
			if (actor != null && actor != character)
				Send.ZC_LEAVE(character.Connection, actor);
		}
	}

	private static void ReenterRealScout(Character character)
	{
		if (character?.Connection == null || character.MapId != 1021)
			return;

		if (character.Map.TryGetMonster(m => m is Npc npc && npc.DialogName == "SIALUL_WEST_DRASIUS" && npc.Layer == 0, out var scoutMonster) && scoutMonster is Npc scoutNpc)
		{
			character.SetMapNPCState(scoutNpc, NpcState.Normal);
			Send.ZC_ENTER_MONSTER(character.Connection, scoutNpc);
			Log.Info("SIAUL_WEST_DRASIUS1_TRACK: re-entered real Scout for '{0}' after track cleanup. handle={1}", character.Name, scoutNpc.Handle);
		}
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
		if (character?.Map == null)
			return;

		var existingKepas = character.Map
			.GetMonsters(monster =>
				monster.Id == 400001 &&
				monster.Hp > 0 &&
				monster.Layer == 0 &&
				IsNearScoutKepaEncounter(monster))
			.OfType<Mob>()
			.ToList();

		foreach (var kepa in existingKepas)
			ConfigureCombatKepa(character, kepa);

		var missingCount = Math.Max(0, 4 - existingKepas.Count);
		if (missingCount <= 0)
		{
			Log.Info("SIAUL_WEST_DRASIUS1_TRACK: reused {0} real-layer Kepa(s) for '{1}'.", existingKepas.Count, character.Name);
			return;
		}

		var spawns = new[]
		{
			(GenType: 24111, X: -1168d, Y: 260d, Z: -579d, Direction: 40d),
			(GenType: 24112, X: -1072d, Y: 260d, Z: -594d, Direction: -35d),
			(GenType: 24113, X: -1216d, Y: 260d, Z: -492d, Direction: 90d),
			(GenType: 24114, X: -1024d, Y: 260d, Z: -471d, Direction: -90d),
		};

		for (var i = 0; i < missingCount; i++)
		{
			var spawn = spawns[(existingKepas.Count + i) % spawns.Length];
			SpawnCombatKepa(character, spawn.GenType, spawn.X, spawn.Y, spawn.Z, spawn.Direction);
		}

		Log.Info("SIAUL_WEST_DRASIUS1_TRACK: spawned {0} combat Kepa(s) for '{1}' on real layer.", missingCount, character.Name);
	}

	private static void SpawnCombatKepa(Character character, int genType, double x, double y, double z, double direction)
	{
		var mob = Shortcuts.AddMonster(genType, 400001, "Kepa", character.Map.ClassName, x, y, z, direction);
		mob.Layer = 0;
		mob.SpawnPosition = mob.Position;
		ConfigureCombatKepa(character, mob);

		if (character.Connection != null)
			Send.ZC_ENTER_MONSTER(character.Connection, mob);
	}

	private static void ConfigureCombatKepa(Character character, Mob mob)
	{
		if (character?.Map == null || mob == null)
			return;

		mob.Layer = 0;
		mob.SpawnPosition = mob.Position;

		if (!mob.Components.Has<MovementComponent>())
			mob.Components.Add(new MovementComponent(mob));

		if (!mob.Components.Has<AiComponent>())
			mob.Components.Add(new AiComponent(mob, "BasicMonster"));

		mob.MonsterType = RelationType.Enemy;
		mob.SetTarget(character);
		mob.InsertHate(character, 5000);
		mob.Tendency = TendencyType.Aggressive;
		mob.FromGround = true;

		if (character.Map.TryGetPropertyOverrides(mob.Id, out var propertyOverrides))
			mob.ApplyOverrides(propertyOverrides);
	}

	private static bool IsNearScoutKepaEncounter(IMonster monster)
	{
		var dx = monster.Position.X - -1121;
		var dz = monster.Position.Z - -528;
		return (dx * dx) + (dz * dz) <= 260 * 260;
	}
}
