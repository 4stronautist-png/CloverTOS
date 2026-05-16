from __future__ import annotations

import sqlite3
import json
from pathlib import Path

from tos_knowledge.schema import connect


class KnowledgeRepository:
    def __init__(self, db_path: Path):
        self.db_path = db_path

    def _connect(self) -> sqlite3.Connection:
        return connect(self.db_path)

    @staticmethod
    def _dicts(rows) -> list[dict]:
        return [dict(row) for row in rows]

    def quest_lookup(self, query: str) -> dict:
        with self._connect() as conn:
            row = conn.execute(
                """
                SELECT * FROM quests
                WHERE lower(class_name) = lower(?)
                   OR lower(name) = lower(?)
                   OR lower(name) LIKE lower(?)
                ORDER BY CASE WHEN lower(class_name) = lower(?) THEN 0 ELSE 1 END, id
                LIMIT 1
                """,
                (query, query, f"%{query}%", query),
            ).fetchone()
            if not row:
                return {"found": False, "query": query}
            quest = dict(row)
            quest_name = quest["class_name"]
            return {
                "found": True,
                "quest": quest,
                "requirements": self._dicts(conn.execute("SELECT * FROM quest_requirements WHERE quest_name = ?", (quest_name,))),
                "objectives": self._dicts(conn.execute("SELECT * FROM quest_objectives WHERE quest_name = ?", (quest_name,))),
                "quest_auto": self._dicts(conn.execute("SELECT * FROM quest_auto_edges WHERE quest_name = ?", (quest_name,))),
                "private_encounters": self._dicts(conn.execute("SELECT * FROM private_encounters WHERE quest_name = ?", (quest_name,))),
                "issues": self._dicts(conn.execute("SELECT * FROM validation_issues WHERE quest_name = ? ORDER BY severity, category", (quest_name,))),
            }

    def quest_graph(self, start: str, depth: int = 8) -> dict:
        depth = max(1, min(depth, 50))
        with self._connect() as conn:
            lookup = self.quest_lookup(start)
            if not lookup.get("found"):
                return lookup
            start_name = lookup["quest"]["class_name"]
            edges: list[dict] = []
            seen = {start_name.lower()}
            frontier = [start_name]
            for _ in range(depth):
                next_frontier: list[str] = []
                for quest_name in frontier:
                    rows = self._dicts(
                        conn.execute(
                            """
                            SELECT quest_name, next_quest_name, track, track_auto_complete
                            FROM quest_auto_edges
                            WHERE lower(quest_name) = lower(?) AND next_quest_name <> ''
                            """,
                            (quest_name,),
                        )
                    )
                    req_rows = self._dicts(
                        conn.execute(
                            """
                            SELECT required_quest_name AS quest_name, quest_name AS next_quest_name, '' AS track, 0 AS track_auto_complete
                            FROM quest_requirements
                            WHERE lower(required_quest_name) = lower(?)
                            """,
                            (quest_name,),
                        )
                    )
                    for edge in rows + req_rows:
                        edges.append(edge)
                        target = edge["next_quest_name"]
                        if target and target.lower() not in seen:
                            seen.add(target.lower())
                            next_frontier.append(target)
                frontier = next_frontier
                if not frontier:
                    break
            quests = self._dicts(
                conn.execute(
                    f"SELECT class_name, id, name, mode, level, start_map, end_map FROM quests WHERE lower(class_name) IN ({','.join('?' for _ in seen)}) ORDER BY id",
                    tuple(seen),
                )
            )
            return {"start": start_name, "depth": depth, "quests": quests, "edges": edges}

    def chain_audit(self, start: str, depth: int = 20, main_only: bool = True) -> dict:
        depth = max(1, min(depth, 200))
        with self._connect() as conn:
            lookup = self.quest_lookup(start)
            if not lookup.get("found"):
                return lookup

            start_name = lookup["quest"]["class_name"]
            seen = {start_name.lower()}
            order = [start_name]
            frontier = [(start_name, 0)]
            edges: list[dict] = []

            while frontier:
                quest_name, distance = frontier.pop(0)
                if distance >= depth:
                    continue

                auto_rows = self._dicts(
                    conn.execute(
                        """
                        SELECT qa.quest_name, qa.next_quest_name, qa.track, qa.track_auto_complete, 'quest_auto' AS edge_type
                        FROM quest_auto_edges qa
                        LEFT JOIN quests q ON lower(q.class_name) = lower(qa.next_quest_name)
                        WHERE lower(qa.quest_name) = lower(?)
                          AND qa.next_quest_name <> ''
                          AND (? = 0 OR q.mode = 'MAIN')
                        """,
                        (quest_name, int(main_only)),
                    )
                )
                req_rows = self._dicts(
                    conn.execute(
                        """
                        SELECT r.required_quest_name AS quest_name, r.quest_name AS next_quest_name,
                               '' AS track, 0 AS track_auto_complete, 'required_by' AS edge_type
                        FROM quest_requirements r
                        JOIN quests q ON lower(q.class_name) = lower(r.quest_name)
                        WHERE lower(r.required_quest_name) = lower(?)
                          AND (? = 0 OR q.mode = 'MAIN')
                        """,
                        (quest_name, int(main_only)),
                    )
                )

                for edge in auto_rows + req_rows:
                    target = edge["next_quest_name"]
                    edges.append(edge)
                    if not target or target.lower() in seen:
                        continue
                    seen.add(target.lower())
                    order.append(target)
                    frontier.append((target, distance + 1))

            placeholders = ",".join("?" for _ in seen)
            quest_rows = self._dicts(
                conn.execute(
                    f"SELECT * FROM quests WHERE lower(class_name) IN ({placeholders})",
                    tuple(seen),
                )
            )
            quest_by_name = {row["class_name"].lower(): row for row in quest_rows}
            quests = [quest_by_name[name.lower()] for name in order if name.lower() in quest_by_name]

            issues = self._dicts(
                conn.execute(
                    f"""
                    SELECT *
                    FROM validation_issues
                    WHERE lower(quest_name) IN ({placeholders})
                    ORDER BY severity, category, quest_name
                    """,
                    tuple(seen),
                )
            )
            issue_summary = self._dicts(
                conn.execute(
                    f"""
                    SELECT severity, category, COUNT(*) AS count
                    FROM validation_issues
                    WHERE lower(quest_name) IN ({placeholders})
                    GROUP BY severity, category
                    ORDER BY severity, category
                    """,
                    tuple(seen),
                )
            )

            return {
                "start": start_name,
                "depth": depth,
                "main_only": main_only,
                "quest_count": len(quests),
                "quests": quests,
                "edges": edges,
                "issue_summary": issue_summary,
                "issues": issues,
            }

    def missing_dialogs(self, limit: int = 200) -> dict:
        with self._connect() as conn:
            rows = self._dicts(
                conn.execute(
                    """
                    SELECT * FROM validation_issues
                    WHERE category IN ('unresolved_dialog_actor', 'missing_npc_field')
                    ORDER BY severity, quest_name
                    LIMIT ?
                    """,
                    (limit,),
                )
            )
            return {"count": len(rows), "issues": rows}

    def missing_spawns(self, map_name: str | None = None, limit: int = 200) -> dict:
        with self._connect() as conn:
            if map_name:
                rows = self._dicts(
                    conn.execute(
                        """
                        SELECT * FROM validation_issues
                        WHERE category IN ('unresolved_kill_target', 'unresolved_private_anchor')
                          AND (subject LIKE ? OR message LIKE ?)
                        ORDER BY severity, quest_name
                        LIMIT ?
                        """,
                        (f"%{map_name}%", f"%{map_name}%", limit),
                    )
                )
            else:
                rows = self._dicts(
                    conn.execute(
                        """
                        SELECT * FROM validation_issues
                        WHERE category IN ('unresolved_kill_target', 'unresolved_drop_target', 'unresolved_private_anchor')
                        ORDER BY severity, quest_name
                        LIMIT ?
                        """,
                        (limit,),
                    )
                )
            return {"count": len(rows), "issues": rows}

    def validation_summary(self, limit: int = 200) -> dict:
        with self._connect() as conn:
            totals = self._dicts(
                conn.execute(
                    "SELECT severity, category, COUNT(*) AS count FROM validation_issues GROUP BY severity, category ORDER BY severity, category"
                )
            )
            issues = self._dicts(conn.execute("SELECT * FROM validation_issues ORDER BY severity, category, quest_name LIMIT ?", (limit,)))
            return {"totals": totals, "issues": issues}

    def list_sources(self) -> dict:
        with self._connect() as conn:
            return {"sources": self._dicts(conn.execute("SELECT * FROM sources ORDER BY trust_rank, id"))}

    def compare_sources(self, quest: str) -> dict:
        lookup = self.quest_lookup(quest)
        if not lookup.get("found"):
            return lookup
        quest_name = lookup["quest"]["class_name"]
        with self._connect() as conn:
            ipf_rows = self._dicts(
                conn.execute(
                    """
                    SELECT source_file, class_name, name, map_name, npc_name, payload
                    FROM ipf_rows
                    WHERE lower(class_name) = lower(?) OR lower(name) = lower(?)
                    """,
                    (quest_name, lookup["quest"].get("name") or ""),
                )
            )
            return {
                "quest": lookup["quest"],
                "local": lookup,
                "ipf": [self._summarize_ipf_row(row) for row in ipf_rows],
                "evidence": self._dicts(
                    conn.execute(
                        "SELECT * FROM evidence WHERE subject_key IN (?, ?) ORDER BY confidence DESC",
                        (quest_name, lookup["quest"].get("name") or ""),
                    )
                ),
            }

    def diagnose_quest(self, quest: str) -> dict:
        lookup = self.quest_lookup(quest)
        if not lookup.get("found"):
            return lookup
        quest_name = lookup["quest"]["class_name"]
        graph = self.quest_graph(quest_name, depth=4)
        source_compare = self.compare_sources(quest_name)
        return {
            "quest": lookup["quest"],
            "requirements": lookup["requirements"],
            "objectives": lookup["objectives"],
            "quest_auto": lookup["quest_auto"],
            "private_encounters": lookup["private_encounters"],
            "issues": lookup["issues"],
            "near_graph": graph,
            "source_compare": source_compare,
        }

    @staticmethod
    def _summarize_ipf_row(row: dict) -> dict:
        try:
            payload = json.loads(row.get("payload") or "{}")
        except json.JSONDecodeError:
            payload = {}
        keep = [
            "ClassID",
            "ClassName",
            "Name",
            "QuestMode",
            "QuestGroup",
            "QuestStartMode",
            "QuestEndMode",
            "StartMap",
            "StartNPC",
            "StartLocation",
            "ProgMap",
            "ProgNPC",
            "ProgLocation",
            "EndMap",
            "EndNPC",
            "EndLocation",
            "QuestName1",
            "QuestCount1",
            "Succ_Check_MonKill",
            "Succ_MonKillName1",
            "Succ_MonKillCount1",
            "Succ_Check_InvItem",
            "Succ_InvItemName1",
            "Succ_InvItemCount1",
        ]
        summary = {key: payload.get(key, "") for key in keep if payload.get(key, "") != ""}
        return {
            "source_file": row.get("source_file"),
            "class_name": row.get("class_name"),
            "name": row.get("name"),
            "map_name": row.get("map_name"),
            "npc_name": row.get("npc_name"),
            "summary": summary,
        }
