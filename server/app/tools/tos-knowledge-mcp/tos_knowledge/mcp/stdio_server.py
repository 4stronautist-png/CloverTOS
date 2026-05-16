from __future__ import annotations

import json
import sys
import traceback
from pathlib import Path
from typing import Any

from tos_knowledge.repository import KnowledgeRepository
from tos_knowledge.validators.quest_flow import run_validations
from tos_knowledge.web_cache import cache_url


def _tool(name: str, description: str, schema: dict[str, Any]) -> dict[str, Any]:
    return {"name": name, "description": description, "inputSchema": schema}


TOOLS = [
    _tool(
        "quest_lookup",
        "Look up one CloverTOS quest by className or display name.",
        {"type": "object", "properties": {"query": {"type": "string"}}, "required": ["query"]},
    ),
    _tool(
        "quest_graph",
        "Return quest_auto and prerequisite graph around a quest.",
        {
            "type": "object",
            "properties": {"start": {"type": "string"}, "depth": {"type": "integer", "default": 8}},
            "required": ["start"],
        },
    ),
    _tool(
        "audit_chain",
        "Recursively audit quest_auto and prerequisite successors from a quest, returning blockers in the chain.",
        {
            "type": "object",
            "properties": {
                "start": {"type": "string"},
                "depth": {"type": "integer", "default": 20},
                "main_only": {"type": "boolean", "default": True}
            },
            "required": ["start"],
        },
    ),
    _tool(
        "diagnose_blocked_quest",
        "Explain why a quest may block progression and return local evidence.",
        {"type": "object", "properties": {"quest": {"type": "string"}}, "required": ["quest"]},
    ),
    _tool(
        "validate_main_chain",
        "Return current validation issue summary for questing.",
        {
            "type": "object",
            "properties": {
                "limit": {"type": "integer", "default": 200},
                "scope": {"type": "string", "enum": ["main", "all"], "default": "main"}
            }
        },
    ),
    _tool(
        "find_missing_dialogs",
        "List NPCDIALOG quests whose dialog/spawn evidence is missing.",
        {"type": "object", "properties": {"limit": {"type": "integer", "default": 200}}},
    ),
    _tool(
        "find_missing_spawns",
        "List kill/drop/private encounter spawn risks, optionally filtered by map.",
        {"type": "object", "properties": {"map": {"type": "string"}, "limit": {"type": "integer", "default": 200}}},
    ),
    _tool(
        "compare_sources",
        "Compare Clover local quest data with indexed IPF/public/manual evidence.",
        {"type": "object", "properties": {"quest": {"type": "string"}}, "required": ["quest"]},
    ),
    _tool(
        "list_sources",
        "List configured knowledge sources and their trust order.",
        {"type": "object", "properties": {}},
    ),
    _tool(
        "cache_public_url",
        "Fetch one allowlisted public TOS-related URL into data/web_cache for later evidence ingestion.",
        {"type": "object", "properties": {"url": {"type": "string"}}, "required": ["url"]},
    ),
]


class JsonRpcServer:
    def __init__(self, app_root: Path, db_path: Path):
        self.app_root = app_root
        self.repo = KnowledgeRepository(db_path)

    def _response(self, request_id, result=None, error=None) -> dict:
        payload = {"jsonrpc": "2.0", "id": request_id}
        if error is not None:
            payload["error"] = error
        else:
            payload["result"] = result
        return payload

    @staticmethod
    def _content(value: Any) -> dict[str, Any]:
        return {"content": [{"type": "text", "text": json.dumps(value, indent=2, sort_keys=True)}]}

    def call_tool(self, name: str, arguments: dict[str, Any]) -> dict[str, Any]:
        if name == "quest_lookup":
            return self._content(self.repo.quest_lookup(arguments["query"]))
        if name == "quest_graph":
            return self._content(self.repo.quest_graph(arguments["start"], int(arguments.get("depth", 8))))
        if name == "audit_chain":
            return self._content(self.repo.chain_audit(
                arguments["start"],
                int(arguments.get("depth", 20)),
                bool(arguments.get("main_only", True)),
            ))
        if name == "diagnose_blocked_quest":
            return self._content(self.repo.diagnose_quest(arguments["quest"]))
        if name == "validate_main_chain":
            run_validations(self.app_root, self.repo.db_path, arguments.get("scope", "main"))
            return self._content(self.repo.validation_summary(int(arguments.get("limit", 200))))
        if name == "find_missing_dialogs":
            return self._content(self.repo.missing_dialogs(int(arguments.get("limit", 200))))
        if name == "find_missing_spawns":
            return self._content(self.repo.missing_spawns(arguments.get("map"), int(arguments.get("limit", 200))))
        if name == "compare_sources":
            return self._content(self.repo.compare_sources(arguments["quest"]))
        if name == "list_sources":
            return self._content(self.repo.list_sources())
        if name == "cache_public_url":
            return self._content(cache_url(self.app_root, arguments["url"]))
        raise ValueError(f"Unknown tool: {name}")

    def handle(self, request: dict[str, Any]) -> dict[str, Any] | None:
        method = request.get("method")
        request_id = request.get("id")
        try:
            if method == "initialize":
                return self._response(
                    request_id,
                    {
                        "protocolVersion": "2024-11-05",
                        "capabilities": {"tools": {}},
                        "serverInfo": {"name": "tos-knowledge-mcp", "version": "0.1.0"},
                    },
                )
            if method == "notifications/initialized":
                return None
            if method == "tools/list":
                return self._response(request_id, {"tools": TOOLS})
            if method == "tools/call":
                params = request.get("params") or {}
                return self._response(request_id, self.call_tool(params.get("name"), params.get("arguments") or {}))
            return self._response(request_id, error={"code": -32601, "message": f"Method not found: {method}"})
        except Exception as exc:
            return self._response(
                request_id,
                error={"code": -32000, "message": str(exc), "data": traceback.format_exc(limit=8)},
            )


def run_stdio_server(app_root: Path, db_path: Path) -> None:
    server = JsonRpcServer(app_root, db_path)
    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            request = json.loads(line)
            response = server.handle(request)
        except Exception as exc:
            response = {"jsonrpc": "2.0", "id": None, "error": {"code": -32700, "message": str(exc)}}
        if response is not None:
            sys.stdout.write(json.dumps(response, separators=(",", ":")) + "\n")
            sys.stdout.flush()
