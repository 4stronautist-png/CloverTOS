from __future__ import annotations

from pathlib import Path


def find_app_root(start: Path) -> Path:
    start = start.resolve()
    candidates = [start]
    candidates.extend(start.parents)
    for candidate in candidates:
        if (candidate / "Melia.sln").exists() and (candidate / "system" / "db" / "quests.txt").exists():
            return candidate
    raise FileNotFoundError(f"Unable to locate CloverTOS server/app root from {start}")


def module_root(app_root: Path) -> Path:
    return app_root / "tools" / "tos-knowledge-mcp"


def data_root(app_root: Path) -> Path:
    path = module_root(app_root) / "data"
    path.mkdir(parents=True, exist_ok=True)
    return path


def knowledge_db_path(app_root: Path) -> Path:
    return data_root(app_root) / "tos_knowledge.sqlite"


def source_registry_path(app_root: Path) -> Path:
    return module_root(app_root) / "sources" / "source_registry.json"


def repo_root_from_app(app_root: Path) -> Path:
    return app_root.parents[2] if len(app_root.parents) >= 3 else app_root
