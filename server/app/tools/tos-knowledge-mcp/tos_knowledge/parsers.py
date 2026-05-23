from __future__ import annotations

import csv
import re
from dataclasses import dataclass
from io import StringIO
from typing import Iterable


FIELD_RE = re.compile(r'(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?P<value>"(?:[^"\\]|\\.)*"|-?\d+(?:\.\d+)?|\[(?:.*?)\])')


def unquote(value: str | None) -> str:
    if not value:
        return ""
    value = value.strip()
    if len(value) >= 2 and value[0] == '"' and value[-1] == '"':
        return bytes(value[1:-1], "utf-8").decode("unicode_escape")
    return value


def int_value(value: str | None, default: int = 0) -> int:
    try:
        return int(float(value or default))
    except (TypeError, ValueError):
        return default


def parse_list_field(value: str | None) -> list[str]:
    if not value:
        return []
    return re.findall(r'"([^"]+)"', value)


def parse_record_fields(text: str) -> dict[str, str]:
    fields: dict[str, str] = {}
    for match in FIELD_RE.finditer(text):
        fields[match.group("name")] = match.group("value")
    return fields


def parse_objectives(raw: str) -> list[dict[str, str]]:
    match = re.search(r"objectives\s*:\s*\[(?P<body>.*?)\]\s*,\s*(?:startMap|progressMap|endMap|requiredQuestName)", raw)
    if not match:
        return []
    objectives: list[dict[str, str]] = []
    for item in re.finditer(r"\{(?P<body>.*?)\}", match.group("body")):
        body = item.group("body")
        fields = parse_record_fields(body)
        parsed = {key: unquote(value) for key, value in fields.items()}
        parsed["raw"] = "{" + body.strip() + "}"
        objectives.append(parsed)
    return objectives


def parse_quest_line(line: str) -> dict[str, object] | None:
    stripped = line.strip()
    if not stripped.startswith("{"):
        return None
    fields = parse_record_fields(stripped)
    class_name = unquote(fields.get("className"))
    if not class_name:
        return None
    return {
        "id": int_value(fields.get("id")),
        "class_name": class_name,
        "category": unquote(fields.get("category")),
        "name": unquote(fields.get("name")),
        "mode": unquote(fields.get("questMode")),
        "level": int_value(fields.get("level")),
        "start_mode": unquote(fields.get("questStartMode")),
        "end_mode": unquote(fields.get("questEndMode")),
        "start_map": unquote(fields.get("startMap")),
        "progress_map": unquote(fields.get("progressMap")),
        "end_map": unquote(fields.get("endMap")),
        "start_location": unquote(fields.get("startLocation")),
        "progress_location": unquote(fields.get("progressLocation")),
        "end_location": unquote(fields.get("endLocation")),
        "start_npc": unquote(fields.get("startNPC")),
        "progress_npc": unquote(fields.get("progressNPC")),
        "end_npc": unquote(fields.get("endNPC")),
        "required": parse_list_field(fields.get("requiredQuestName")),
        "objectives": parse_objectives(stripped),
        "raw": stripped,
    }


def parse_entity_line(line: str) -> dict[str, object] | None:
    stripped = line.strip()
    if not stripped.startswith("{"):
        return None
    fields = parse_record_fields(stripped)
    class_name = unquote(fields.get("className"))
    if not class_name:
        return None
    return {
        "id": int_value(fields.get("id")),
        "class_name": class_name,
        "name": unquote(fields.get("name")),
        "raw": stripped,
    }


def split_csharp_args(text: str) -> list[str]:
    reader = csv.reader(StringIO(text), skipinitialspace=True)
    try:
        row = next(reader)
    except StopIteration:
        return []
    return [cell.strip() for cell in row]


@dataclass
class AddNpcCall:
    npc_id: int
    class_id: int
    display_name: str
    map_name: str
    x: float
    y: float
    z: float
    direction: float
    dialog_name: str
    raw: str


def float_arg(value: str) -> float:
    try:
        return float(value.strip().strip('"'))
    except ValueError:
        return 0.0


def parse_add_npc_calls(source: str) -> Iterable[AddNpcCall]:
    for match in re.finditer(r"AddNpc\s*\((?P<args>.*?)\)\s*;", source, re.S):
        raw = match.group(0)
        args = split_csharp_args(match.group("args").replace("\n", " "))
        if len(args) < 9:
            continue
        yield AddNpcCall(
            npc_id=int_value(args[0]),
            class_id=int_value(args[1]),
            display_name=unquote(args[2]),
            map_name=unquote(args[3]),
            x=float_arg(args[4]),
            y=float_arg(args[5]),
            z=float_arg(args[6]),
            direction=float_arg(args[7]),
            dialog_name=unquote(args[8]),
            raw=raw.strip(),
        )


def iter_csv_dicts(path) -> Iterable[dict[str, str]]:
    with open(path, "r", encoding="utf-8-sig", errors="replace", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            yield {key: (value or "") for key, value in row.items()}
