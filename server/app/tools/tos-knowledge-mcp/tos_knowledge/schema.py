from __future__ import annotations

import sqlite3
from pathlib import Path


SCHEMA = """
PRAGMA journal_mode=DELETE;

CREATE TABLE IF NOT EXISTS metadata (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS sources (
    id TEXT PRIMARY KEY,
    kind TEXT NOT NULL,
    trust_rank INTEGER NOT NULL,
    url TEXT,
    local_path TEXT,
    notes TEXT
);

CREATE TABLE IF NOT EXISTS quests (
    class_name TEXT PRIMARY KEY,
    id INTEGER,
    name TEXT,
    category TEXT,
    mode TEXT,
    level INTEGER,
    start_mode TEXT,
    end_mode TEXT,
    start_map TEXT,
    progress_map TEXT,
    end_map TEXT,
    start_location TEXT,
    progress_location TEXT,
    end_location TEXT,
    start_npc TEXT,
    progress_npc TEXT,
    end_npc TEXT,
    raw TEXT NOT NULL,
    source TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS quest_requirements (
    quest_name TEXT NOT NULL,
    required_quest_name TEXT NOT NULL,
    source TEXT NOT NULL,
    PRIMARY KEY (quest_name, required_quest_name, source)
);

CREATE TABLE IF NOT EXISTS quest_objectives (
    quest_name TEXT NOT NULL,
    ident TEXT NOT NULL,
    type TEXT,
    target TEXT,
    item TEXT,
    drop_target TEXT,
    count INTEGER,
    text TEXT,
    raw TEXT NOT NULL,
    source TEXT NOT NULL,
    PRIMARY KEY (quest_name, ident, source)
);

CREATE TABLE IF NOT EXISTS quest_auto_edges (
    quest_name TEXT NOT NULL,
    next_quest_name TEXT NOT NULL,
    track TEXT,
    track_auto_complete INTEGER NOT NULL DEFAULT 0,
    source TEXT NOT NULL,
    PRIMARY KEY (quest_name, next_quest_name, source)
);

CREATE TABLE IF NOT EXISTS tracks (
    track_name TEXT PRIMARY KEY,
    script_path TEXT,
    evidence TEXT
);

CREATE TABLE IF NOT EXISTS entities (
    kind TEXT NOT NULL,
    class_name TEXT NOT NULL,
    id INTEGER,
    name TEXT,
    raw TEXT,
    source TEXT NOT NULL,
    PRIMARY KEY (kind, class_name, source)
);

CREATE TABLE IF NOT EXISTS npcs (
    dialog_name TEXT NOT NULL,
    map_name TEXT,
    npc_id INTEGER,
    class_id INTEGER,
    display_name TEXT,
    x REAL,
    y REAL,
    z REAL,
    direction REAL,
    script_path TEXT,
    raw TEXT,
    source TEXT NOT NULL,
    PRIMARY KEY (dialog_name, map_name, npc_id, script_path)
);

CREATE TABLE IF NOT EXISTS script_symbols (
    kind TEXT NOT NULL,
    name TEXT NOT NULL,
    path TEXT NOT NULL,
    line INTEGER,
    evidence TEXT,
    PRIMARY KEY (kind, name, path)
);

CREATE TABLE IF NOT EXISTS private_encounters (
    quest_name TEXT NOT NULL,
    map_name TEXT,
    target TEXT,
    min_spawn_count INTEGER,
    map_point TEXT NOT NULL,
    raw TEXT NOT NULL,
    source TEXT NOT NULL,
    PRIMARY KEY (quest_name, target, map_point)
);

CREATE TABLE IF NOT EXISTS ipf_rows (
    source_file TEXT NOT NULL,
    class_name TEXT NOT NULL,
    name TEXT,
    map_name TEXT,
    npc_name TEXT,
    payload TEXT NOT NULL,
    PRIMARY KEY (source_file, class_name)
);

CREATE TABLE IF NOT EXISTS evidence (
    subject_kind TEXT NOT NULL,
    subject_key TEXT NOT NULL,
    source_id TEXT NOT NULL,
    confidence INTEGER NOT NULL,
    url TEXT,
    local_path TEXT,
    summary TEXT NOT NULL,
    captured_at TEXT,
    payload TEXT,
    PRIMARY KEY (subject_kind, subject_key, source_id, summary)
);

CREATE TABLE IF NOT EXISTS validation_issues (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    severity TEXT NOT NULL,
    category TEXT NOT NULL,
    quest_name TEXT,
    subject TEXT,
    message TEXT NOT NULL,
    suggestion TEXT,
    source TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_quests_mode ON quests(mode);
CREATE INDEX IF NOT EXISTS idx_quests_name ON quests(name);
CREATE INDEX IF NOT EXISTS idx_objectives_target ON quest_objectives(target);
CREATE INDEX IF NOT EXISTS idx_npcs_dialog ON npcs(dialog_name);
CREATE INDEX IF NOT EXISTS idx_private_quest ON private_encounters(quest_name);
CREATE INDEX IF NOT EXISTS idx_issues_quest ON validation_issues(quest_name);
"""


def connect(db_path: Path) -> sqlite3.Connection:
    db_path.parent.mkdir(parents=True, exist_ok=True)
    conn = sqlite3.connect(db_path, timeout=30)
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA foreign_keys=ON")
    return conn


def initialize(conn: sqlite3.Connection) -> None:
    conn.executescript(SCHEMA)


def reset_generated_tables(conn: sqlite3.Connection) -> None:
    for table in [
        "metadata",
        "sources",
        "quests",
        "quest_requirements",
        "quest_objectives",
        "quest_auto_edges",
        "tracks",
        "entities",
        "npcs",
        "script_symbols",
        "private_encounters",
        "ipf_rows",
        "evidence",
        "validation_issues",
    ]:
        conn.execute(f"DELETE FROM {table}")
