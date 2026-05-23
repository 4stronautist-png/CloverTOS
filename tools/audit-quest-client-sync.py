#!/usr/bin/env python3
import csv
import pathlib
import re


ROOT = pathlib.Path(__file__).resolve().parents[1]
CLIENT_QUESTS = ROOT.parent / "tools" / "ipf_extract_work" / "extract" / "ies.ipf" / "questprogresscheck.ies"
SERVER_QUESTS = ROOT / "server" / "app" / "system" / "db" / "quests.txt"
SERVER_MONSTERS = ROOT / "server" / "app" / "system" / "db" / "monsters.txt"
LAIMA_MOBS = ROOT / "server" / "app" / "packages" / "laima" / "scripts" / "zone" / "content" / "laima" / "mobs"
MONSTER_IDS = ROOT / "server" / "app" / "packages" / "laima" / "scripts" / "zone" / "const" / "MonsterId.cs"


def read_client_rows():
	with CLIENT_QUESTS.open(newline="", encoding="utf-8-sig") as f:
		return {row["ClassName"]: row for row in csv.DictReader(f) if row.get("ClassName")}


def read_server_quests():
	result = []
	for line in SERVER_QUESTS.read_text(encoding="utf-8").splitlines():
		class_match = re.search(r'className: "([^"]+)"', line)
		if not class_match:
			continue

		id_match = re.search(r"id: (\d+)", line)
		mode_match = re.search(r'questMode: "([^"]*)"', line)
		prog_match = re.search(r'progressLocation: "([^"]*)"', line)
		prog_map_match = re.search(r'progressMap: "([^"]*)"', line)
		targets = re.findall(r'type: "(?:Kill|Collect)"[^\]]*?(?:target|dropTarget): "([^"]+)"', line)

		result.append({
			"id": int(id_match.group(1)) if id_match else 0,
			"className": class_match.group(1),
			"mode": mode_match.group(1) if mode_match else "",
			"progressLocation": prog_match.group(1) if prog_match else "",
			"progressMap": prog_map_match.group(1) if prog_map_match else "",
			"targets": targets,
			"line": line,
		})

	return result


def client_kill_targets(row):
	result = []
	for i in range(1, 7):
		value = (row.get(f"Succ_MonKillName{i}") or "").strip()
		if value:
			result.append(value)
	return result


def read_monster_classes():
	result = set()
	id_to_class = {}
	for line in SERVER_MONSTERS.read_text(encoding="utf-8").splitlines():
		class_match = re.search(r'className: "([^"]+)"', line)
		id_match = re.search(r"monsterId: (\d+)", line)
		if class_match:
			result.add(class_match.group(1))
			if id_match:
				id_to_class[int(id_match.group(1))] = class_match.group(1)
	return result, id_to_class


def read_monster_ids():
	result = {}
	for line in MONSTER_IDS.read_text(encoding="utf-8").splitlines():
		match = re.search(r"public const int (\w+) = (\d+);", line)
		if match:
			result[match.group(1)] = int(match.group(2))
	return result


def read_mob_spawns(monster_id_to_class):
	id_to_monster = {}
	map_to_monsters = {}
	monster_ids = read_monster_ids()

	for path in LAIMA_MOBS.rglob("*.cs"):
		text = path.read_text(encoding="utf-8", errors="ignore")
		for match in re.finditer(r'AddSpawner\("([^"]+)",\s*MonsterId\.(\w+)', text):
			enum_name = match.group(2)
			monster_id = monster_ids.get(enum_name)
			id_to_monster[match.group(1)] = monster_id_to_class.get(monster_id, enum_name)

		for match in re.finditer(r'AddSpawnPoint\("([^"]+)",\s*"([^"]+)"', text):
			monster = id_to_monster.get(match.group(1))
			if monster:
				map_to_monsters.setdefault(match.group(2), set()).add(monster)

	return map_to_monsters


def target_parts(target):
	return [part for part in target.split("/") if part and part != "ALL"]


def maps_for_quest(quest):
	result = set()
	if quest["progressMap"]:
		result.add(quest["progressMap"])

	tokens = quest["progressLocation"].split()
	for token in tokens:
		if token.startswith(("f_", "d_", "c_", "mission_", "id_")):
			result.add(token)

	return result


def main():
	client_rows = read_client_rows()
	server_quests = read_server_quests()

	missing_client_rows = []
	target_diffs = []
	progress_diffs = []
	missing_target_spawns = []
	monster_classes, monster_id_to_class = read_monster_classes()
	map_to_monsters = read_mob_spawns(monster_id_to_class)

	for quest in server_quests:
		row = client_rows.get(quest["className"])
		if row is None:
			missing_client_rows.append(quest)
			continue

		client_targets = client_kill_targets(row)
		if client_targets and quest["targets"] and client_targets != quest["targets"][:len(client_targets)]:
			target_diffs.append((quest, client_targets))

		client_progress = (row.get("ProgLocation") or "").strip()
		if client_progress and quest["progressLocation"] and client_progress != quest["progressLocation"]:
			progress_diffs.append((quest, client_progress))

		quest_maps = maps_for_quest(quest)
		if quest_maps:
			for target in quest["targets"]:
				for part in target_parts(target):
					if part not in monster_classes:
						continue
					if not any(part in map_to_monsters.get(map_name, set()) for map_name in quest_maps):
						missing_target_spawns.append((quest, part, sorted(quest_maps)))

	print(f"server_quests={len(server_quests)} client_rows={len(client_rows)}")
	print(f"missing_client_rows={len(missing_client_rows)}")
	print(f"target_diffs={len(target_diffs)}")
	for quest, client_targets in target_diffs[:250]:
		print(f"TARGET id={quest['id']} class={quest['className']} mode={quest['mode']} server={quest['targets']} client={client_targets}")

	print(f"progress_diffs={len(progress_diffs)}")
	for quest, client_progress in progress_diffs[:250]:
		print(f"PROGRESS id={quest['id']} class={quest['className']} mode={quest['mode']} server={quest['progressLocation']} client={client_progress}")

	print(f"missing_static_target_spawns={len(missing_target_spawns)}")
	print("runtime_objective_spawn_fallback=enabled in QuestComponent for active Kill/Collect objectives")
	for quest, target, quest_maps in missing_target_spawns[:250]:
		print(f"SPAWN id={quest['id']} class={quest['className']} mode={quest['mode']} target={target} maps={quest_maps}")


if __name__ == "__main__":
	main()
