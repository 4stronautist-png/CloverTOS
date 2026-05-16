from __future__ import annotations

import json
import re
from datetime import datetime, timezone
from pathlib import Path

from tos_knowledge.parsers import iter_csv_dicts, parse_add_npc_calls, parse_entity_line, parse_quest_line
from tos_knowledge.paths import repo_root_from_app, source_registry_path
from tos_knowledge.schema import connect, initialize, reset_generated_tables


def _read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def _rel(app_root: Path, path: Path) -> str:
    try:
        return str(path.relative_to(app_root)).replace("\\", "/")
    except ValueError:
        return str(path).replace("\\", "/")


def load_source_registry(app_root: Path) -> list[dict[str, object]]:
    path = source_registry_path(app_root)
    if not path.exists():
        return []
    return json.loads(_read_text(path))


def ingest_source_registry(conn, app_root: Path) -> int:
    count = 0
    for source in load_source_registry(app_root):
        conn.execute(
            """
            INSERT OR REPLACE INTO sources(id, kind, trust_rank, url, local_path, notes)
            VALUES (?, ?, ?, ?, ?, ?)
            """,
            (
                source.get("id"),
                source.get("kind", "unknown"),
                int(source.get("trust_rank", 100)),
                source.get("url"),
                source.get("local_path"),
                source.get("notes"),
            ),
        )
        count += 1
    return count


def ingest_quests(conn, app_root: Path) -> dict[str, int]:
    quest_path = app_root / "system" / "db" / "quests.txt"
    quest_count = objective_count = requirement_count = 0
    for line in quest_path.read_text(encoding="utf-8", errors="replace").splitlines():
        quest = parse_quest_line(line)
        if not quest:
            continue
        conn.execute(
            """
            INSERT OR REPLACE INTO quests(
                class_name, id, name, category, mode, level, start_mode, end_mode,
                start_map, progress_map, end_map, start_location, progress_location,
                end_location, start_npc, progress_npc, end_npc, raw, source
            )
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (
                quest["class_name"],
                quest["id"],
                quest["name"],
                quest["category"],
                quest["mode"],
                quest["level"],
                quest["start_mode"],
                quest["end_mode"],
                quest["start_map"],
                quest["progress_map"],
                quest["end_map"],
                quest["start_location"],
                quest["progress_location"],
                quest["end_location"],
                quest["start_npc"],
                quest["progress_npc"],
                quest["end_npc"],
                quest["raw"],
                "clover:system/db/quests.txt",
            ),
        )
        quest_count += 1
        for required in quest["required"]:
            conn.execute(
                """
                INSERT OR REPLACE INTO quest_requirements(quest_name, required_quest_name, source)
                VALUES (?, ?, ?)
                """,
                (quest["class_name"], required, "clover:system/db/quests.txt"),
            )
            requirement_count += 1
        for objective in quest["objectives"]:
            conn.execute(
                """
                INSERT OR REPLACE INTO quest_objectives(
                    quest_name, ident, type, target, item, drop_target, count, text, raw, source
                )
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    quest["class_name"],
                    objective.get("ident") or f"objective_{objective_count}",
                    objective.get("type"),
                    objective.get("target"),
                    objective.get("item"),
                    objective.get("dropTarget"),
                    int(objective.get("count") or 0),
                    objective.get("text"),
                    objective.get("raw", ""),
                    "clover:system/db/quests.txt",
                ),
            )
            objective_count += 1
    return {"quests": quest_count, "objectives": objective_count, "requirements": requirement_count}


def ingest_entities(conn, app_root: Path) -> dict[str, int]:
    files = {
        "map": app_root / "system" / "db" / "maps.txt",
        "monster": app_root / "system" / "db" / "monsters.txt",
        "item": app_root / "system" / "db" / "items.txt",
    }
    counts: dict[str, int] = {}
    for kind, path in files.items():
        count = 0
        if not path.exists():
            counts[kind] = 0
            continue
        for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
            entity = parse_entity_line(line)
            if not entity:
                continue
            conn.execute(
                """
                INSERT OR REPLACE INTO entities(kind, class_name, id, name, raw, source)
                VALUES (?, ?, ?, ?, ?, ?)
                """,
                (kind, entity["class_name"], entity["id"], entity["name"], entity["raw"], f"clover:{_rel(app_root, path)}"),
            )
            count += 1
        counts[kind] = count
    return counts


def ingest_quest_auto(conn, app_root: Path) -> int:
    path = app_root / "system" / "db" / "quest_auto.txt"
    if not path.exists():
        return 0
    rows = json.loads(_read_text(path))
    count = 0
    for row in rows:
        quest_name = row.get("questName", "")
        next_names = row.get("successNextQuestNames") or []
        if not next_names:
            conn.execute(
                """
                INSERT OR REPLACE INTO quest_auto_edges(quest_name, next_quest_name, track, track_auto_complete, source)
                VALUES (?, ?, ?, ?, ?)
                """,
                (quest_name, "", row.get("track", ""), int(bool(row.get("trackAutoComplete"))), "clover:system/db/quest_auto.txt"),
            )
            count += 1
            continue
        for next_quest in next_names:
            conn.execute(
                """
                INSERT OR REPLACE INTO quest_auto_edges(quest_name, next_quest_name, track, track_auto_complete, source)
                VALUES (?, ?, ?, ?, ?)
                """,
                (quest_name, next_quest, row.get("track", ""), int(bool(row.get("trackAutoComplete"))), "clover:system/db/quest_auto.txt"),
            )
            count += 1
    return count


def ingest_private_encounters(conn, app_root: Path) -> int:
    path = app_root / "system" / "db" / "private_encounters.txt"
    if not path.exists():
        return 0
    rows = json.loads(_read_text(path))
    count = 0
    for row in rows:
        points = row.get("mapPointGroup") or []
        for point in points:
            conn.execute(
                """
                INSERT OR REPLACE INTO private_encounters(
                    quest_name, map_name, target, min_spawn_count, map_point, raw, source
                )
                VALUES (?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    row.get("questName", ""),
                    row.get("mapName", ""),
                    row.get("target", ""),
                    int(row.get("minSpawnCount") or 0),
                    point,
                    json.dumps(row, sort_keys=True),
                    "clover:system/db/private_encounters.txt",
                ),
            )
            count += 1
    return count


def ingest_npc_spawns(conn, app_root: Path) -> int:
    root = app_root / "packages" / "laima" / "scripts" / "zone" / "content" / "laima" / "npcs"
    count = 0
    if not root.exists():
        return count
    for path in root.rglob("*.cs"):
        source = _read_text(path)
        for npc in parse_add_npc_calls(source):
            if not npc.dialog_name:
                continue
            conn.execute(
                """
                INSERT OR REPLACE INTO npcs(
                    dialog_name, map_name, npc_id, class_id, display_name, x, y, z,
                    direction, script_path, raw, source
                )
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    npc.dialog_name,
                    npc.map_name,
                    npc.npc_id,
                    npc.class_id,
                    npc.display_name,
                    npc.x,
                    npc.y,
                    npc.z,
                    npc.direction,
                    _rel(app_root, path),
                    npc.raw,
                    "clover:npc-scripts",
                ),
            )
            count += 1
    return count


def ingest_script_symbols(conn, app_root: Path) -> dict[str, int]:
    counts = {"dialog_function": 0, "track_script": 0, "quest_component_reference": 0}
    npc_functions = app_root / "src" / "ZoneServer" / "Scripting" / "Shared" / "NPCFunctions.cs"
    if npc_functions.exists():
        lines = _read_text(npc_functions).splitlines()
        for index, line in enumerate(lines):
            if "[DialogFunction" not in line:
                continue
            window = "\n".join(lines[index : index + 4])
            match = re.search(r"Task\s+([A-Za-z0-9_]+)\s*\(", window)
            if not match:
                continue
            names = {match.group(1)}
            names.update(re.findall(r'DialogFunction\("([^"]+)"\)', window))
            for name in names:
                conn.execute(
                    """
                    INSERT OR REPLACE INTO script_symbols(kind, name, path, line, evidence)
                    VALUES (?, ?, ?, ?, ?)
                    """,
                    ("dialog_function", name, _rel(app_root, npc_functions), index + 1, window.strip()),
                )
                counts["dialog_function"] += 1

    track_root = app_root / "packages" / "laima" / "scripts" / "zone" / "content" / "laima" / "tracks"
    if track_root.exists():
        for path in track_root.rglob("*.cs"):
            source = _read_text(path)
            names = {path.stem.upper()}
            names.update(re.findall(r'"([A-Z0-9_]+_TRACK)"', source))
            names.update(re.findall(r"class\s+([A-Za-z0-9_]*Track[A-Za-z0-9_]*)", source))
            for name in names:
                conn.execute(
                    """
                    INSERT OR REPLACE INTO tracks(track_name, script_path, evidence)
                    VALUES (?, ?, ?)
                    """,
                    (name.upper(), _rel(app_root, path), "track script file"),
                )
                counts["track_script"] += 1

    quest_component = app_root / "src" / "ZoneServer" / "World" / "Actors" / "Characters" / "Components" / "QuestComponent.cs"
    if quest_component.exists():
        source = _read_text(quest_component)
        for name in sorted(set(re.findall(r'"([A-Z0-9_]{4,})"', source))):
            if "_" not in name:
                continue
            conn.execute(
                """
                INSERT OR REPLACE INTO script_symbols(kind, name, path, line, evidence)
                VALUES (?, ?, ?, ?, ?)
                """,
                ("quest_component_reference", name, _rel(app_root, quest_component), None, "string reference in QuestComponent"),
            )
            counts["quest_component_reference"] += 1
    return counts


def ingest_ipf(conn, app_root: Path) -> int:
    repo_root = repo_root_from_app(app_root)
    candidates = [
        repo_root / "tools" / "ipf_extract_work" / "extract" / "ies.ipf" / "request.ies",
        repo_root / "tools" / "ipf_extract_work" / "extract" / "ies.ipf" / "questprogresscheck.ies",
        repo_root / "tools" / "ipf_extract_work" / "extract" / "ies.ipf" / "questprogressnpc.ies",
        repo_root / "tools" / "ipf_extract_work" / "extract" / "ies.ipf" / "sessionobject_request.ies",
    ]
    count = 0
    for path in candidates:
        if not path.exists():
            continue
        for row in iter_csv_dicts(path):
            class_name = row.get("ClassName") or row.get("QuestName") or row.get("Name") or ""
            if not class_name:
                continue
            payload = json.dumps(row, ensure_ascii=True, sort_keys=True)
            conn.execute(
                """
                INSERT OR REPLACE INTO ipf_rows(source_file, class_name, name, map_name, npc_name, payload)
                VALUES (?, ?, ?, ?, ?, ?)
                """,
                (
                    str(path.relative_to(repo_root)).replace("\\", "/"),
                    class_name,
                    row.get("Name", ""),
                    row.get("StartMap", "") or row.get("Map", ""),
                    row.get("StartNPC", "") or row.get("RequestNPC", ""),
                    payload,
                ),
            )
            count += 1
    return count


def build_database(app_root: Path, db_path: Path, include_ipf: bool = True) -> dict[str, object]:
    conn = connect(db_path)
    initialize(conn)
    reset_generated_tables(conn)
    now = datetime.now(timezone.utc).isoformat()
    conn.execute("INSERT OR REPLACE INTO metadata(key, value) VALUES (?, ?)", ("built_at", now))
    conn.execute("INSERT OR REPLACE INTO metadata(key, value) VALUES (?, ?)", ("app_root", str(app_root)))

    summary: dict[str, object] = {"database": str(db_path), "built_at": now}
    summary["sources"] = ingest_source_registry(conn, app_root)
    summary.update(ingest_quests(conn, app_root))
    summary["entities"] = ingest_entities(conn, app_root)
    summary["quest_auto_edges"] = ingest_quest_auto(conn, app_root)
    summary["private_encounters"] = ingest_private_encounters(conn, app_root)
    summary["npc_spawns"] = ingest_npc_spawns(conn, app_root)
    summary["script_symbols"] = ingest_script_symbols(conn, app_root)
    summary["ipf_rows"] = ingest_ipf(conn, app_root) if include_ipf else 0
    conn.commit()
    conn.close()
    return summary
