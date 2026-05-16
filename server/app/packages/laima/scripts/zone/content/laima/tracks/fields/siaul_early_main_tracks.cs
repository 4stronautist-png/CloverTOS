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

[TrackScript("SIAUL_WEST_MEET_NAGLIS_TRACK")]
public class SiaulWestMeetNaglisTrackScript : TrackScript
{
	protected override void Load()
	{
		SetId("SIAUL_WEST_MEET_NAGLIS_TRACK");
		SetPropertyId(PropertyName.SIAUL_WEST_MEET_NAGLIS_TRACK);
		SetCancelable(false);
	}

	public override IActor[] OnStart(Character character, Track track)
	{
		base.OnStart(character, track);

		var actors = new List<IActor>
		{
			SiaulEarlyMainTrackUtil.SpawnCutsceneNpc(character, 100301, 20016, "Search Scout", -1490, 260, -140, 0, "SIAUL_WEST_NAGLIS2"),
			SiaulEarlyMainTrackUtil.SpawnCutsceneMob(character, MonsterId.Onion_Big, "Large Kepa", -1450, 260, -110, -135),
		};

		SiaulEarlyMainTrackUtil.QueueFallbackEnd(character, track, TimeSpan.FromSeconds(4.5));
		Log.Info("SIAUL_WEST_MEET_NAGLIS_TRACK: started Search Scout/Large Kepa track for '{0}'.", character.Name);
		return actors.ToArray();
	}

	public override void OnComplete(Character character, Track track)
	{
		SiaulEarlyMainTrackUtil.RemoveTrackActorsFromClient(character, track);
		base.OnComplete(character, track);
		character.RestoreCoreHudState(true, true);
		SiaulEarlyMainTrackUtil.QueueRealCombatSpawn(character, 1014, MonsterId.Onion_Big, "Large Kepa", 101401, -1490, 260, -140, 0, 1, 180);
	}

	public override void OnCancel(Character character, Track track)
	{
		SiaulEarlyMainTrackUtil.RemoveTrackActorsFromClient(character, track);
		base.OnCancel(character, track);
		character.RestoreCoreHudState(true, true);
	}
}

[TrackScript("SIAUL_WEST_SOLDIER3_TRACK")]
public class SiaulWestSoldier3TrackScript : TrackScript
{
	protected override void Load()
	{
		SetId("SIAUL_WEST_SOLDIER3_TRACK");
		SetPropertyId(PropertyName.SIAUL_WEST_SOLDIER3_TRACK);
		SetCancelable(false);
	}

	public override IActor[] OnStart(Character character, Track track)
	{
		base.OnStart(character, track);

		var actors = new List<IActor>
		{
			SiaulEarlyMainTrackUtil.SpawnCutsceneNpc(character, 2002, 20016, "Battle Commander", -663, 322, 503, 0, "SIAUL_WEST_SOL3"),
			SiaulEarlyMainTrackUtil.SpawnCutsceneMob(character, MonsterId.Hanaming, "Hanaming", 93, 322, 589, -90),
			SiaulEarlyMainTrackUtil.SpawnCutsceneMob(character, MonsterId.Hanaming, "Hanaming", -386, 322, 357, 90),
		};

		SiaulEarlyMainTrackUtil.QueueFallbackEnd(character, track, TimeSpan.FromSeconds(4.5));
		Log.Info("SIAUL_WEST_SOLDIER3_TRACK: started Battle Commander/Hanaming track for '{0}'.", character.Name);
		return actors.ToArray();
	}

	public override void OnComplete(Character character, Track track)
	{
		SiaulEarlyMainTrackUtil.RemoveTrackActorsFromClient(character, track);
		base.OnComplete(character, track);
		character.RestoreCoreHudState(true, true);
		SiaulEarlyMainTrackUtil.QueueRealCombatSpawn(character, 1020, MonsterId.Hanaming, "Hanaming", 102001, 93, 322, 589, -90, 2, 180);
		SiaulEarlyMainTrackUtil.QueueRealCombatSpawn(character, 1020, MonsterId.Hanaming, "Hanaming", 102011, -386, 322, 357, 90, 2, 180);
	}

	public override void OnCancel(Character character, Track track)
	{
		SiaulEarlyMainTrackUtil.RemoveTrackActorsFromClient(character, track);
		base.OnCancel(character, track);
		character.RestoreCoreHudState(true, true);
	}
}

[TrackScript("SIAUL_EAST_REQUEST6_TRACK")]
public class SiaulEastRequest6TrackScript : TrackScript
{
	protected override void Load()
	{
		SetId("SIAUL_EAST_REQUEST6_TRACK");
		SetPropertyId(PropertyName.SIAUL_EAST_REQUEST6_TRACK);
		SetCancelable(false);
	}

	public override IActor[] OnStart(Character character, Track track)
	{
		base.OnStart(character, track);

		var actors = new List<IActor>
		{
			SiaulEarlyMainTrackUtil.SpawnCutsceneNpc(character, 50, 10032, "Search Scout", 1242, 130, 339, -90, "SIAUL_EAST_SOLDIER8"),
		};

		SiaulEarlyMainTrackUtil.QueueFallbackEnd(character, track, TimeSpan.FromSeconds(4.5));
		Log.Info("SIAUL_EAST_REQUEST6_TRACK: started Papaya-style scout track for '{0}'.", character.Name);
		return actors.ToArray();
	}

	public override void OnComplete(Character character, Track track)
	{
		SiaulEarlyMainTrackUtil.RemoveTrackActorsFromClient(character, track);
		base.OnComplete(character, track);
		character.RestoreCoreHudState(true, true);
		character.Quests.QueueGenericQuestAutoTrackFollowUp(track);
	}

	public override void OnCancel(Character character, Track track)
	{
		SiaulEarlyMainTrackUtil.RemoveTrackActorsFromClient(character, track);
		base.OnCancel(character, track);
		character.RestoreCoreHudState(true, true);
	}
}

internal static class SiaulEarlyMainTrackUtil
{
	public static Npc SpawnCutsceneNpc(Character character, int genType, int monsterId, string name, double x, double y, double z, double direction, string dialogName)
	{
		var npc = Shortcuts.AddNpc(genType, monsterId, name, character.Map.ClassName, x, y, z, direction, dialogName);
		npc.SetVisibilty(ActorVisibility.Track, character.ObjectId);
		npc.Layer = character.Layer;
		Send.ZC_ENTER_MONSTER(character.Connection, npc);
		return npc;
	}

	public static Mob SpawnCutsceneMob(Character character, int monsterId, string name, double x, double y, double z, double direction)
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

		if (character.Map.TryGetPropertyOverrides(mob.Id, out var propertyOverrides))
			mob.ApplyOverrides(propertyOverrides);

		character.Map.AddMonster(mob);
		Send.ZC_ENTER_MONSTER(character.Connection, mob);
		return mob;
	}

	public static void RemoveTrackActorsFromClient(Character character, Track track)
	{
		if (character?.Connection == null || track?.Actors == null)
			return;

		foreach (var actor in track.Actors)
		{
			if (actor != null && actor != character)
				Send.ZC_LEAVE(character.Connection, actor);
		}
	}

	public static void QueueFallbackEnd(Character character, Track track, TimeSpan delay)
	{
		_ = Task.Run(async () =>
		{
			await Task.Delay(delay);

			if (character.Connection == null)
				return;
			if (character.Tracks.ActiveTrack?.Id != track.Id)
				return;

			Log.Warning("{0} fallback: force-ending track for '{1}' on layer {2}.", track.Id, character.Name, character.Layer);
			character.Tracks.End(track.Id);
		});
	}

	public static void QueueRealCombatSpawn(Character character, long questId, int monsterId, string name, int genType, double x, double y, double z, double direction, int count, double radius)
	{
		if (character?.Map == null)
			return;

		_ = Task.Run(async () =>
		{
			for (var attempt = 0; attempt < 8; attempt++)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(250));

				if (character.Connection == null)
					return;
				if (character.Tracks.ActiveTrack == null)
					break;
			}

			if (character.Connection == null || character.Map == null || character.Tracks.ActiveTrack != null)
				return;
			if (!character.Quests.TryGetById(questId, out var quest) || !quest.InProgress || quest.ObjectivesCompleted)
				return;

			if (character.Layer != 0)
				character.StopLayer();

			var existing = character.Map
				.GetMonsters(monster =>
					QuestCombatTargetMatches(monster.Id, monsterId) &&
					monster.Hp > 0 &&
					monster.Layer == 0 &&
					IsNear(monster, x, z, radius))
				.OfType<Mob>()
				.ToList();

			var kept = existing.Take(count).ToList();
			var extras = existing.Skip(count).ToList();

			foreach (var mob in kept)
				ConfigureCombatMob(character, mob);

			foreach (var mob in extras)
			{
				if (character.Connection != null)
					Send.ZC_LEAVE(character.Connection, mob);

				character.Map.RemoveMonster(mob);
			}

			var missing = Math.Max(0, count - kept.Count);
			for (var i = 0; i < missing; i++)
			{
				var angle = (Math.PI * 2 / Math.Max(1, count)) * i;
				var offset = count == 1 ? 0 : 55;
				var mob = Shortcuts.AddMonster(genType + i, monsterId, name, character.Map.ClassName, x + Math.Cos(angle) * offset, y, z + Math.Sin(angle) * offset, direction);
				ConfigureCombatMob(character, mob);

				if (character.Connection != null)
					Send.ZC_ENTER_MONSTER(character.Connection, mob);
			}

			character.Quests.SyncStaticQuestNpcStates();
			character.Quests.UpdateClient();
			character.RestoreCoreHudState(true, true);
			character.LookAround();
			Log.Info("Papaya early main track: prepared {0} real combat actor(s) for quest {1} and '{2}' (removed {3} duplicate actor(s)).", Math.Max(count, kept.Count + missing), questId, character.Name, extras.Count);
		});
	}

	private static void ConfigureCombatMob(Character character, Mob mob)
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
			mob.ApplyOverrides(propertyOverrides, syncClient: true);
	}

	private static bool IsNear(IMonster monster, double x, double z, double radius)
	{
		var dx = monster.Position.X - x;
		var dz = monster.Position.Z - z;
		return (dx * dx) + (dz * dz) <= radius * radius;
	}

	private static bool QuestCombatTargetMatches(int monsterId, int requestedMonsterId)
	{
		return Mob.IsSameMonsterFamily(monsterId, requestedMonsterId);
	}
}
