from __future__ import annotations

import re
from pathlib import Path

from tos_knowledge.schema import connect


COORDINATE_POINT_RE = re.compile(r"^\S+\s+-?\d+(?:\.\d+)?\s+-?\d+(?:\.\d+)?\s+-?\d+(?:\.\d+)?(?:\s+\d+)?$")


def _rows(conn, sql: str, params=()):
    return [dict(row) for row in conn.execute(sql, params).fetchall()]


def _has_entity(conn, kind: str, class_name: str) -> bool:
    if not class_name or class_name == "ALL":
        return True
    row = conn.execute(
        "SELECT 1 FROM entities WHERE kind = ? AND lower(class_name) = lower(?) LIMIT 1",
        (kind, class_name),
    ).fetchone()
    return row is not None


def _has_npc_or_dialog(conn, dialog_name: str, map_name: str | None = None) -> bool:
    if not dialog_name:
        return False
    if map_name:
        row = conn.execute(
            """
            SELECT 1 FROM npcs
            WHERE lower(dialog_name) = lower(?) AND (map_name IS NULL OR lower(map_name) = lower(?))
            LIMIT 1
            """,
            (dialog_name, map_name),
        ).fetchone()
        if row:
            return True
    row = conn.execute(
        "SELECT 1 FROM script_symbols WHERE kind = 'dialog_function' AND lower(name) = lower(?) LIMIT 1",
        (dialog_name,),
    ).fetchone()
    if row:
        return True
    row = conn.execute(
        "SELECT 1 FROM npcs WHERE lower(dialog_name) = lower(?) LIMIT 1",
        (dialog_name,),
    ).fetchone()
    return row is not None


def _has_static_quest_dialog_fallback(conn, dialog_name: str) -> bool:
    if not dialog_name:
        return False
    row = conn.execute(
        """
        SELECT 1
        FROM quests
        WHERE lower(start_npc) = lower(?)
           OR lower(progress_npc) = lower(?)
           OR lower(end_npc) = lower(?)
        LIMIT 1
        """,
        (dialog_name, dialog_name, dialog_name),
    ).fetchone()
    return row is not None


def _target_parts(target: str | None) -> list[str]:
    if not target or target == "ALL":
        return []
    return [part.strip() for part in target.split("/") if part.strip() and part.strip() != "ALL"]


def _has_private_target(conn, quest_name: str, target: str) -> bool:
    rows = conn.execute(
        "SELECT target FROM private_encounters WHERE lower(quest_name) = lower(?)",
        (quest_name,),
    ).fetchall()
    for row in rows:
        if target in _target_parts(row["target"]):
            return True
    return False


def _track_name(track: str | None) -> str:
    if not track:
        return ""
    for part in track.split("/"):
        if part.endswith("_TRACK"):
            return part.upper()
    return ""


def _has_runtime_generic_track_fallback(app_root: Path) -> bool:
    quest_component = app_root / "src" / "ZoneServer" / "World" / "Actors" / "Characters" / "Components" / "QuestComponent.cs"
    track_component = app_root / "src" / "ZoneServer" / "World" / "Actors" / "Characters" / "Components" / "TrackComponent.cs"
    if not quest_component.exists() or not track_component.exists():
        return False
    quest_source = quest_component.read_text(encoding="utf-8", errors="ignore")
    track_source = track_component.read_text(encoding="utf-8", errors="ignore")
    return (
        "CreateGenericQuestAutoTrackActors" in quest_source
        and "QueueGenericQuestAutoTrackFollowUp" in quest_source
        and "SourceQuestId" in track_source
    )


def _anchor_name(point: str) -> tuple[str, str] | None:
    if COORDINATE_POINT_RE.match(point.strip()):
        return None
    parts = point.split()
    if len(parts) < 2:
        return None
    return parts[0], parts[1]


def _insert_issue(conn, issue: dict[str, str | None]) -> None:
    conn.execute(
        """
        INSERT INTO validation_issues(severity, category, quest_name, subject, message, suggestion, source)
        VALUES (?, ?, ?, ?, ?, ?, ?)
        """,
        (
            issue["severity"],
            issue["category"],
            issue.get("quest_name"),
            issue.get("subject"),
            issue["message"],
            issue.get("suggestion"),
            "tos-knowledge-mcp",
        ),
    )


def add_issue(
    issues: list[dict[str, str | None]],
    severity: str,
    category: str,
    message: str,
    quest_name: str | None = None,
    subject: str | None = None,
    suggestion: str | None = None,
) -> None:
    issues.append(
        {
            "severity": severity,
            "category": category,
            "quest_name": quest_name,
            "subject": subject,
            "message": message,
            "suggestion": suggestion,
        }
    )


def run_validations(app_root: Path, db_path: Path, scope: str = "main") -> list[dict[str, str | None]]:
    conn = connect(db_path)
    conn.execute("DELETE FROM validation_issues")
    issues: list[dict[str, str | None]] = []

    quests = _rows(conn, "SELECT * FROM quests")
    quest_names = {quest["class_name"].lower() for quest in quests}
    if scope == "main":
        scoped_quests = [quest for quest in quests if quest["mode"] == "MAIN"]
    else:
        scoped_quests = quests
    scoped_names = {quest["class_name"].lower() for quest in scoped_quests}
    has_generic_track_fallback = _has_runtime_generic_track_fallback(app_root)

    for req in _rows(conn, "SELECT * FROM quest_requirements"):
        if scope == "main" and req["quest_name"].lower() not in scoped_names:
            continue
        if req["required_quest_name"].lower() not in quest_names:
            add_issue(
                issues,
                "error",
                "missing_prerequisite",
                f"Prerequisite quest does not exist: {req['required_quest_name']}",
                req["quest_name"],
                req["required_quest_name"],
                "Add the missing quest row or repair requiredQuestName.",
            )

    for quest in scoped_quests:
        quest_name = quest["class_name"]
        if quest["mode"] == "MAIN":
            for map_field in ["start_map", "progress_map", "end_map"]:
                map_name = quest.get(map_field)
                if map_name and map_name != "None" and not _has_entity(conn, "map", map_name):
                    add_issue(
                        issues,
                        "error",
                        "missing_map",
                        f"{map_field} references missing map {map_name}",
                        quest_name,
                        map_name,
                        "Add or correct the map className in maps.txt.",
                    )

        for mode_field, npc_field, map_field in [
            ("start_mode", "start_npc", "start_map"),
            ("end_mode", "end_npc", "end_map"),
        ]:
            if quest.get(mode_field) != "NPCDIALOG":
                continue
            npc = quest.get(npc_field)
            if not npc:
                add_issue(
                    issues,
                    "error",
                    "missing_npc_field",
                    f"{mode_field}=NPCDIALOG but {npc_field} is empty",
                    quest_name,
                    npc_field,
                    "Populate the NPC field or change the quest mode.",
                )
            elif not _has_npc_or_dialog(conn, npc, quest.get(map_field)):
                if _has_static_quest_dialog_fallback(conn, npc):
                    add_issue(
                        issues,
                        "warning",
                        "fallback_dialog_actor",
                        f"{npc_field} {npc} relies on the runtime static quest dialog fallback",
                        quest_name,
                        npc,
                        "Add a static AddNpc/DialogFunction later if Papaya-accurate placement or bespoke dialog is required.",
                    )
                else:
                    add_issue(
                        issues,
                        "error",
                        "unresolved_dialog_actor",
                        f"{npc_field} {npc} has no static spawn or dialog function evidence",
                        quest_name,
                        npc,
                        "Add an AddNpc spawn with this dialog name, add a DialogFunction, or mark as bridge/fallback.",
                    )

    objective_rows = _rows(
        conn,
        """
        SELECT o.*
        FROM quest_objectives o
        JOIN quests q ON lower(q.class_name) = lower(o.quest_name)
        WHERE ? = 'all' OR q.mode = 'MAIN'
        """,
        (scope,),
    )
    for objective in objective_rows:
        quest_name = objective["quest_name"]
        obj_type = objective["type"] or ""
        if obj_type == "Kill":
            for target in _target_parts(objective["target"]):
                if not _has_entity(conn, "monster", target) and not _has_private_target(conn, quest_name, target):
                    add_issue(
                        issues,
                        "error",
                        "unresolved_kill_target",
                        f"Kill target has no monster row or private encounter evidence: {target}",
                        quest_name,
                        target,
                        "Add the monster, map the correct target, or create private_encounters.txt coverage.",
                    )
        if obj_type == "Collect":
            item = objective["item"]
            if item and not item.isdigit() and not _has_entity(conn, "item", item):
                add_issue(
                    issues,
                    "error",
                    "unresolved_collect_item",
                    f"Collect item is not present in items.txt: {item}",
                    quest_name,
                    item,
                    "Add/sync the item row or correct the objective item.",
                )
            for target in _target_parts(objective["drop_target"]):
                if not _has_entity(conn, "monster", target) and not _has_private_target(conn, quest_name, target):
                    add_issue(
                        issues,
                        "error",
                        "unresolved_drop_target",
                        f"Drop target has no monster row or private encounter evidence: {target}",
                        quest_name,
                        target,
                        "Add the monster, map the correct drop target, or create private_encounters.txt coverage.",
                    )

    for edge in _rows(conn, "SELECT * FROM quest_auto_edges"):
        quest_name = edge["quest_name"]
        if scope == "main" and quest_name and quest_name.lower() not in scoped_names:
            continue
        if quest_name and quest_name.lower() not in quest_names:
            add_issue(
                issues,
                "error",
                "quest_auto_missing_source",
                f"quest_auto source quest is missing: {quest_name}",
                quest_name,
                quest_name,
                "Remove stale quest_auto entry or add the quest.",
            )
        next_quest = edge["next_quest_name"]
        if next_quest and next_quest.lower() not in quest_names:
            add_issue(
                issues,
                "error",
                "quest_auto_missing_target",
                f"quest_auto target quest is missing: {next_quest}",
                quest_name,
                next_quest,
                "Repair successNextQuestNames or add the target quest.",
            )
        track_name = _track_name(edge["track"])
        if track_name:
            row = conn.execute("SELECT 1 FROM tracks WHERE track_name = ? LIMIT 1", (track_name,)).fetchone()
            if not row:
                if has_generic_track_fallback:
                    add_issue(
                        issues,
                        "info",
                        "generic_track_fallback",
                        f"quest_auto track has no bespoke TrackScript but is covered by the runtime generic quest_auto fallback: {track_name}",
                        quest_name,
                        track_name,
                        "Add a bespoke TrackScript only when Papaya-identical choreography is required.",
                    )
                else:
                    add_issue(
                        issues,
                        "warning",
                        "missing_track_script",
                        f"quest_auto references track without local track script evidence: {track_name}",
                        quest_name,
                        track_name,
                        "Add the track script, implement fallback completion, or confirm this is client/native-only.",
                    )

    for encounter in _rows(conn, "SELECT * FROM private_encounters"):
        if scope == "main" and encounter["quest_name"].lower() not in scoped_names:
            continue
        if encounter["quest_name"].lower() not in quest_names:
            add_issue(
                issues,
                "error",
                "private_encounter_missing_quest",
                f"Private encounter references missing quest: {encounter['quest_name']}",
                encounter["quest_name"],
                encounter["target"],
                "Remove stale encounter or add the quest.",
            )
        for target in _target_parts(encounter["target"]):
            if not _has_entity(conn, "monster", target):
                add_issue(
                    issues,
                    "warning",
                    "private_encounter_missing_monster",
                    f"Private encounter target has no monster row: {target}",
                    encounter["quest_name"],
                    target,
                    "Sync monster data or correct the target.",
                )
        anchor = _anchor_name(encounter["map_point"])
        if anchor:
            map_name, name = anchor
            if not _has_npc_or_dialog(conn, name, map_name):
                add_issue(
                    issues,
                    "warning",
                    "unresolved_private_anchor",
                    f"Private encounter anchor has no static actor evidence: {encounter['map_point']}",
                    encounter["quest_name"],
                    encounter["map_point"],
                    "Prefer explicit coordinates or add static anchor resolution evidence.",
                )

    issues.sort(key=lambda issue: (0 if issue["severity"] == "error" else 1, issue["category"], issue.get("quest_name") or ""))
    for issue in issues:
        _insert_issue(conn, issue)
    conn.commit()
    conn.close()
    return issues
