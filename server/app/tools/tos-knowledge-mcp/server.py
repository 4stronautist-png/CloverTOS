#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from tos_knowledge.ingest.clover import build_database
from tos_knowledge.mcp.stdio_server import run_stdio_server
from tos_knowledge.paths import find_app_root, knowledge_db_path
from tos_knowledge.repository import KnowledgeRepository
from tos_knowledge.validators.quest_flow import run_validations
from tos_knowledge.web_cache import cache_url


def command_build(args: argparse.Namespace) -> int:
    app_root = find_app_root(Path(args.app_root) if args.app_root else ROOT)
    db_path = Path(args.db) if args.db else knowledge_db_path(app_root)
    summary = build_database(app_root, db_path, include_ipf=not args.no_ipf)
    print(json.dumps(summary, indent=2, sort_keys=True))
    return 0


def command_validate(args: argparse.Namespace) -> int:
    app_root = find_app_root(Path(args.app_root) if args.app_root else ROOT)
    db_path = Path(args.db) if args.db else knowledge_db_path(app_root)
    if args.rebuild or not db_path.exists():
        build_database(app_root, db_path, include_ipf=not args.no_ipf)
    issues = run_validations(app_root, db_path, scope=args.scope)
    result = {
        "database": str(db_path),
        "issue_count": len(issues),
        "errors": sum(1 for issue in issues if issue["severity"] == "error"),
        "warnings": sum(1 for issue in issues if issue["severity"] == "warning"),
        "issues": issues[: args.limit],
    }
    print(json.dumps(result, indent=2, sort_keys=True))
    if args.fail_on_error and result["errors"] > 0:
        return 1
    return 0


def command_diagnose(args: argparse.Namespace) -> int:
    app_root = find_app_root(Path(args.app_root) if args.app_root else ROOT)
    db_path = Path(args.db) if args.db else knowledge_db_path(app_root)
    if args.rebuild or not db_path.exists():
        build_database(app_root, db_path, include_ipf=not args.no_ipf)
        run_validations(app_root, db_path)
    repo = KnowledgeRepository(db_path)
    print(json.dumps(repo.diagnose_quest(args.quest), indent=2, sort_keys=True))
    return 0


def command_audit_chain(args: argparse.Namespace) -> int:
    app_root = find_app_root(Path(args.app_root) if args.app_root else ROOT)
    db_path = Path(args.db) if args.db else knowledge_db_path(app_root)
    if args.rebuild or not db_path.exists():
        build_database(app_root, db_path, include_ipf=not args.no_ipf)
        run_validations(app_root, db_path)
    repo = KnowledgeRepository(db_path)
    audit = repo.chain_audit(args.quest, depth=args.depth, main_only=not args.include_side)
    if args.summary:
        errors = [issue for issue in audit.get("issues", []) if issue.get("severity") == "error"]
        audit = {
            "start": audit.get("start"),
            "depth": audit.get("depth"),
            "quest_count": audit.get("quest_count"),
            "issue_summary": audit.get("issue_summary"),
            "error_count": len(errors),
            "errors": errors[: args.limit],
        }
    print(json.dumps(audit, indent=2, sort_keys=True))
    return 0


def command_crawl(args: argparse.Namespace) -> int:
    app_root = find_app_root(Path(args.app_root) if args.app_root else ROOT)
    result = cache_url(app_root, args.url)
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0


def command_mcp(args: argparse.Namespace) -> int:
    app_root = find_app_root(Path(args.app_root) if args.app_root else ROOT)
    db_path = Path(args.db) if args.db else knowledge_db_path(app_root)
    if args.rebuild or not db_path.exists():
        build_database(app_root, db_path, include_ipf=not args.no_ipf)
        run_validations(app_root, db_path)
    run_stdio_server(app_root, db_path)
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="CloverTOS Tree of Savior knowledge MCP")
    parser.add_argument("--app-root", default=None, help="CloverTOS server/app root")
    parser.add_argument("--db", default=None, help="SQLite database path")
    sub = parser.add_subparsers(dest="command", required=True)

    build = sub.add_parser("build", help="Rebuild the knowledge database")
    build.add_argument("--no-ipf", action="store_true", help="Skip extracted IPF/IES indexing")
    build.set_defaults(func=command_build)

    validate = sub.add_parser("validate", help="Run static questing validations")
    validate.add_argument("--rebuild", action="store_true", help="Rebuild before validating")
    validate.add_argument("--no-ipf", action="store_true", help="Skip extracted IPF/IES indexing during rebuild")
    validate.add_argument("--limit", type=int, default=200, help="Maximum issues to print")
    validate.add_argument("--scope", choices=["main", "all"], default="main", help="Quest scope to validate")
    validate.add_argument("--fail-on-error", action="store_true", help="Return non-zero when errors are found")
    validate.set_defaults(func=command_validate)

    diagnose = sub.add_parser("diagnose", help="Diagnose one quest by className or name")
    diagnose.add_argument("quest")
    diagnose.add_argument("--rebuild", action="store_true", help="Rebuild before diagnosing")
    diagnose.add_argument("--no-ipf", action="store_true", help="Skip extracted IPF/IES indexing during rebuild")
    diagnose.set_defaults(func=command_diagnose)

    audit_chain = sub.add_parser("audit-chain", help="Audit recursive quest_auto/prerequisite successors from one quest")
    audit_chain.add_argument("quest")
    audit_chain.add_argument("--depth", type=int, default=20, help="Maximum recursive successor depth")
    audit_chain.add_argument("--include-side", action="store_true", help="Include non-main successor quests")
    audit_chain.add_argument("--summary", action="store_true", help="Print only counts and error details")
    audit_chain.add_argument("--limit", type=int, default=50, help="Maximum errors to print with --summary")
    audit_chain.add_argument("--rebuild", action="store_true", help="Rebuild before auditing")
    audit_chain.add_argument("--no-ipf", action="store_true", help="Skip extracted IPF/IES indexing during rebuild")
    audit_chain.set_defaults(func=command_audit_chain)

    crawl = sub.add_parser("crawl", help="Fetch and cache one allowed public URL")
    crawl.add_argument("url")
    crawl.set_defaults(func=command_crawl)

    mcp = sub.add_parser("mcp", help="Run the MCP stdio server")
    mcp.add_argument("--rebuild", action="store_true", help="Rebuild before serving")
    mcp.add_argument("--no-ipf", action="store_true", help="Skip extracted IPF/IES indexing during rebuild")
    mcp.set_defaults(func=command_mcp)

    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main())
