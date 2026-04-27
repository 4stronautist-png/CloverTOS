#!/usr/bin/env python3
"""
Fill static quest objectives from the extracted ToS client IES tables.

This intentionally handles only data that can be inferred with high
confidence from questprogresscheck.ies:

- kill objectives from Succ_Check_MonKill / Succ_MonKillNameN
- collect objectives where a kill grants a Succ_InvItemNameN item

Cutscenes, smartgen timing, dialog side effects, and custom scripts are
reported but not invented here.
"""

from __future__ import annotations

import argparse
import csv
import re
from pathlib import Path


ENTRY_RE = re.compile(r'^\s*\{.*\},?\s*$')
CLASS_RE = re.compile(r'className:\s*"([^"]+)"')
OBJECTIVES_RE = re.compile(r',\s*objectives:\s*\[[^\]]*\](?=\s*\})')


def read_csv_by_class(path: Path, class_field: str = "ClassName") -> dict[str, dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as f:
        return {row[class_field]: row for row in csv.DictReader(f) if row.get(class_field)}


def read_server_monster_names(monsters_txt: Path) -> dict[str, str]:
    names: dict[str, str] = {}
    pattern = re.compile(r'className:\s*"([^"]+)".*?name:\s*"([^"]*)"')
    with monsters_txt.open("r", encoding="utf-8") as f:
        for line in f:
            match = pattern.search(line)
            if match:
                names[match.group(1)] = match.group(2) or match.group(1)
    return names


def slug(value: str) -> str:
    return re.sub(r"[^a-z0-9_]+", "_", value.lower()).strip("_") or "objective"


def qpc_int(row: dict[str, str], key: str) -> int:
    try:
        return int(float(row.get(key) or "0"))
    except ValueError:
        return 0


def qpc_float(row: dict[str, str], key: str, default: float = 0) -> float:
    try:
        return float(row.get(key) or default)
    except ValueError:
        return default


def build_objectives(row: dict[str, str], monster_names: dict[str, str]) -> list[str]:
    objectives: list[str] = []

    inv_counts = {f"Succ_InvItemName{i}": qpc_int(row, f"Succ_InvItemCount{i}") for i in range(1, 5)}
    inv_items = {f"Succ_InvItemName{i}": row.get(f"Succ_InvItemName{i}", "") for i in range(1, 5)}

    if qpc_int(row, "Succ_Check_MonKill") <= 0:
        return objectives

    for i in range(1, 7):
        target = row.get(f"Succ_MonKillName{i}", "").strip()
        count = qpc_int(row, f"Succ_MonKillCount{i}")
        if not target or count <= 0:
            continue

        item_ref = row.get(f"Succ_MonKill_ItemGive{i}", "").strip()
        item = inv_items.get(item_ref, "")
        item_count = inv_counts.get(item_ref, 0)
        if item and item_count > 0:
            percent = qpc_float(row, f"Succ_MonKill_ItemPercent{i}", 1000)
            drop_chance = max(percent / 1000.0, 0.001)
            objectives.append(
                '{{ ident: "{ident}", type: "Collect", item: "{item}", dropTarget: "{target}", '
                'dropChance: {chance:g}, count: {count}, text: "Collect {item}" }}'.format(
                    ident=slug(item),
                    item=item,
                    target=target,
                    chance=drop_chance,
                    count=item_count,
                )
            )
            continue

        monster_name = monster_names.get(target, target)
        objectives.append(
            '{{ ident: "{ident}", type: "Kill", target: "{target}", count: {count}, text: "Defeat {name}" }}'.format(
                ident=slug(target),
                target=target,
                count=count,
                name=monster_name.replace('"', '\\"'),
            )
        )

    return objectives


def patch_quests(quests_path: Path, qpc: dict[str, dict[str, str]], monster_names: dict[str, str], apply: bool) -> tuple[int, list[str]]:
    lines = quests_path.read_text(encoding="utf-8").splitlines(keepends=True)
    changed = 0
    notes: list[str] = []
    out: list[str] = []

    for line in lines:
        stripped = line.strip()
        if not ENTRY_RE.match(stripped):
            out.append(line)
            continue

        match = CLASS_RE.search(line)
        if not match:
            out.append(line)
            continue

        class_name = match.group(1)
        row = qpc.get(class_name)
        if not row:
            out.append(line)
            continue

        objectives = build_objectives(row, monster_names)
        if not objectives:
            out.append(line)
            continue

        if "objectives:" in line:
            notes.append(f"skip existing objectives: {class_name}")
            out.append(line)
            continue

        insert = ", objectives: [" + ",".join(objectives) + "]"
        new_line = re.sub(r'\s*\},\s*$', insert + " },\n", line)
        if new_line == line:
            out.append(line)
            continue

        changed += 1
        notes.append(f"add objectives: {class_name} -> {len(objectives)}")
        out.append(new_line)

    if apply and changed:
        quests_path.write_text("".join(out), encoding="utf-8")

    return changed, notes


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--app", required=True, type=Path, help="Melia/Clover server app directory")
    parser.add_argument("--client-ies", required=True, type=Path, help="Extracted ies.ipf directory")
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()

    app = args.app
    client_ies = args.client_ies
    quests_path = app / "system/db/quests.txt"
    monsters_path = app / "system/db/monsters.txt"
    qpc_path = client_ies / "questprogresscheck.ies"

    qpc = read_csv_by_class(qpc_path)
    monster_names = read_server_monster_names(monsters_path)

    changed, notes = patch_quests(quests_path, qpc, monster_names, args.apply)

    mode = "applied" if args.apply else "dry-run"
    print(f"{mode}: {changed} quest rows would change" if not args.apply else f"{mode}: {changed} quest rows changed")
    for note in notes:
        if note.startswith("add objectives"):
            print(note)

    skipped = [note for note in notes if note.startswith("skip existing")]
    print(f"skipped existing objective rows: {len(skipped)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
