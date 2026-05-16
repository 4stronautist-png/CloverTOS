#!/usr/bin/env python3
"""
Generate Clover quest compatibility data from the extracted Papaya IES files.

The generated files are intentionally server DB files, not hand-authored fixes.
Papaya stays the source of truth, and this script can be rerun after a new IPF
extract to refresh quest tracking/encounter behavior.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
MELIA_ROOT = ROOT.parent
PAPAYA_IES = MELIA_ROOT / "tools" / "ipf_extract_work" / "extract" / "ies.ipf"
MONGEN_IES = MELIA_ROOT / "tools" / "ipf_work" / "extract" / "ies_mongen.ipf"
DB_ROOT = ROOT / "server" / "app" / "system" / "db"
REPORT_PATH = ROOT / "server" / "app" / "system" / "db" / "papaya_quest_pack_report.md"


def read_csv(path: Path) -> list[dict[str, str]]:
	with path.open("r", encoding="utf-8-sig", newline="") as f:
		return list(csv.DictReader(f))


def is_none(value: str | None) -> bool:
	return value is None or value.strip() == "" or value.strip().lower() == "none"


def clean(value: str | None) -> str:
	return "" if value is None else value.strip()


def read_existing_quest_ids() -> dict[str, int]:
	text = (DB_ROOT / "quests.txt").read_text(encoding="utf-8")
	result: dict[str, int] = {}
	for match in re.finditer(r"id:\s*(\d+),\s*className:\s*\"([^\"]+)\"", text):
		result[match.group(2)] = int(match.group(1))
	return result


def first_map_token(location: str) -> str:
	if is_none(location):
		return ""

	parts = location.split()
	for part in parts:
		if part.startswith(("f_", "d_", "c_", "p_", "mission_", "id_")):
			return part
	return ""


def split_map_point_groups(value: str) -> list[str]:
	if is_none(value):
		return []

	parts = value.split()
	starts = [i for i, part in enumerate(parts) if part.startswith(("f_", "d_", "c_", "p_", "mission_", "id_"))]
	if not starts:
		return [value.strip()]

	groups: list[str] = []
	for index, start in enumerate(starts):
		end = starts[index + 1] if index + 1 < len(starts) else len(parts)
		group = " ".join(parts[start:end]).strip()
		if group:
			groups.append(group)
	return groups


def numeric_map_point(group: str) -> bool:
	parts = group.split()
	return len(parts) >= 5 and all(is_number(part) for part in parts[1:4])


def is_number(value: str) -> bool:
	try:
		float(value)
		return True
	except ValueError:
		return False


def load_anchor_points() -> dict[tuple[str, str], list[str]]:
	result: dict[tuple[str, str], list[str]] = {}
	for path in MONGEN_IES.glob("anchor_*.ies"):
		map_name = path.stem.removeprefix("anchor_")
		for row in read_csv(path):
			name = clean(row.get("Name"))
			if not name:
				continue
			group = f"{map_name} {clean(row.get('PosX'))} {clean(row.get('PosY'))} {clean(row.get('PosZ'))} {clean(row.get('AnchorRange')) or '100'}"
			result.setdefault((map_name.lower(), name.lower()), []).append(group)
	return result


def resolve_named_groups(groups: list[str], anchors: dict[tuple[str, str], list[str]]) -> tuple[list[str], int]:
	resolved: list[str] = []
	named_fallbacks = 0

	for group in groups:
		if numeric_map_point(group):
			resolved.append(group)
			continue

		parts = group.split()
		if len(parts) >= 3:
			map_name = parts[0]
			name = parts[1]
			key = (map_name.lower(), name.lower())
			if key in anchors:
				resolved.extend(anchors[key])
				continue

		resolved.append(group)
		named_fallbacks += 1

	return dedupe(resolved), named_fallbacks


def dedupe(values: list[str]) -> list[str]:
	seen: set[str] = set()
	result: list[str] = []
	for value in values:
		key = value.lower()
		if key in seen:
			continue
		seen.add(key)
		result.append(value)
	return result


def collect_session_map_points() -> dict[str, list[str]]:
	result: dict[str, list[str]] = {}
	for row in read_csv(PAPAYA_IES / "sessionobject.ies"):
		quest_name = clean(row.get("QuestName"))
		if not quest_name:
			continue
		groups = [clean(row.get(f"QuestMapPointGroup{i}")) for i in range(1, 11)]
		groups = [group for group in groups if not is_none(group)]
		if groups:
			result[quest_name] = groups
	return result


def generate_private_encounters() -> tuple[list[dict], dict[str, int]]:
	quest_ids = read_existing_quest_ids()
	session_points = collect_session_map_points()
	anchors = load_anchor_points()

	entries: list[dict] = []
	stats = {
		"papaya_layer_kill_rows": 0,
		"written_private_encounters": 0,
		"numeric_or_anchor_points": 0,
		"named_fallback_points": 0,
		"missing_from_clover_quests": 0,
	}

	for row in read_csv(PAPAYA_IES / "questprogresscheck.ies"):
		quest_name = clean(row.get("ClassName"))
		if is_none(quest_name) or clean(row.get("Succ_Kill_Layer")).lower() != "layer":
			continue

		targets = [clean(row.get(f"Succ_MonKillName{i}")) for i in range(1, 7)]
		targets = [target for target in targets if not is_none(target)]
		if not targets:
			continue

		stats["papaya_layer_kill_rows"] += 1
		if quest_name not in quest_ids:
			stats["missing_from_clover_quests"] += 1
			continue

		map_groups = split_map_point_groups(clean(row.get("ProgLocation")))
		if not map_groups:
			map_groups = session_points.get(quest_name, [])
		if not map_groups:
			map_groups = split_map_point_groups(clean(row.get("StartLocation")))
		if not map_groups:
			map_groups = split_map_point_groups(clean(row.get("EndLocation")))

		map_groups, named_fallbacks = resolve_named_groups(map_groups, anchors)
		stats["named_fallback_points"] += named_fallbacks
		stats["numeric_or_anchor_points"] += max(0, len(map_groups) - named_fallbacks)

		map_name = clean(row.get("ProgMap")) or first_map_token(" ".join(map_groups)) or clean(row.get("StartMap")) or clean(row.get("EndMap"))
		counts = [int(clean(row.get(f"Succ_MonKillCount{i}")) or "0") for i in range(1, 7) if clean(row.get(f"Succ_MonKillCount{i}")).isdigit()]
		min_spawn_count = max(1, min(max(counts) if counts else 1, 3))

		entries.append({
			"id": quest_ids[quest_name] * 100 + 1,
			"questName": quest_name,
			"mapName": map_name,
			"target": "/".join(dedupe(targets)),
			"minSpawnCount": min_spawn_count,
			"mapPointGroup": map_groups,
		})

	stats["written_private_encounters"] = len(entries)
	return entries, stats


def parse_next_quests(row: dict[str, str]) -> list[str]:
	result: list[str] = []
	for i in range(1, 7):
		value = clean(row.get(f"Success_NextQuestName{i}"))
		if not is_none(value):
			result.append(value)
	return dedupe(result)


def generate_quest_auto() -> tuple[list[dict], dict[str, int]]:
	quest_ids = read_existing_quest_ids()
	entries: list[dict] = []
	stats = {
		"papaya_auto_rows": 0,
		"written_auto_rows": 0,
		"tracks": 0,
		"success_next_links": 0,
		"missing_from_clover_quests": 0,
	}

	for row in read_csv(PAPAYA_IES / "questprogresscheck_auto.ies"):
		quest_name = clean(row.get("ClassName"))
		if is_none(quest_name):
			continue

		track = clean(row.get("Track1"))
		next_quests = parse_next_quests(row)
		if is_none(track) and not next_quests:
			continue

		stats["papaya_auto_rows"] += 1
		if quest_name not in quest_ids:
			stats["missing_from_clover_quests"] += 1
			continue

		entries.append({
			"id": quest_ids[quest_name],
			"questName": quest_name,
			"track": track,
			"trackAutoComplete": clean(row.get("Track_Auto_Complete")).upper() == "YES",
			"successNextQuestNames": next_quests,
		})
		if not is_none(track):
			stats["tracks"] += 1
		stats["success_next_links"] += len(next_quests)

	stats["written_auto_rows"] = len(entries)
	return entries, stats


def write_json(path: Path, entries: list[dict]) -> None:
	path.write_text(json.dumps(entries, ensure_ascii=False, indent="\t") + "\n", encoding="utf-8")


def write_report(private_stats: dict[str, int], auto_stats: dict[str, int]) -> None:
	lines = [
		"# Papaya Quest Pack Import Report",
		"",
		"Generated from extracted Papaya IES files.",
		"",
		"## Private encounters",
		"",
	]
	for key, value in private_stats.items():
		lines.append(f"- {key}: {value}")
	lines.extend(["", "## Quest auto", ""])
	for key, value in auto_stats.items():
		lines.append(f"- {key}: {value}")
	lines.append("")
	REPORT_PATH.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
	parser = argparse.ArgumentParser()
	parser.add_argument("--write", action="store_true", help="write generated DB files")
	args = parser.parse_args()

	private_entries, private_stats = generate_private_encounters()
	auto_entries, auto_stats = generate_quest_auto()

	if args.write:
		write_json(DB_ROOT / "private_encounters.txt", private_entries)
		write_json(DB_ROOT / "quest_auto.txt", auto_entries)
		write_report(private_stats, auto_stats)

	print(json.dumps({
		"private_encounters": private_stats,
		"quest_auto": auto_stats,
	}, indent=2))


if __name__ == "__main__":
	main()
