# TOS Knowledge MCP

This module is the long-horizon quest repair knowledge layer for CloverTOS.
It builds a local, versioned knowledge database from Clover data, extracted
client/IPF data, public sources, and manually exported community/Papaya notes.

The goal is not to replace playtesting. The goal is to catch the questing
failure modes that make playtesting 1400+ quests impractical:

- missing prerequisite links
- missing maps, monsters, items, dialogs, NPC spawns, private encounters
- objectives without a resolvable target
- NPCDIALOG quests whose actor is not reachable
- quest_auto links that do not resolve
- track/cutscene references without local script evidence
- bridge quests that should be server-only or auto-completed
- map anchors that do not resolve to a static point or known actor

## Quick Start

From `CloverTOS/server/app`:

```powershell
tools/Validate-TosKnowledge.ps1 -Rebuild
```

Or directly:

```powershell
python tools/tos-knowledge-mcp/server.py build
python tools/tos-knowledge-mcp/server.py validate
python tools/tos-knowledge-mcp/server.py diagnose SOUT_Q_01
python tools/tos-knowledge-mcp/server.py audit-chain SOUT_Q_16 --depth 40
python tools/tos-knowledge-mcp/server.py mcp
```

`audit-chain` follows both `quest_auto` successors and future quests that list
the current quest as a prerequisite. Use it before repairing a blocker so the
same patch can cover the next handoffs instead of only the visible quest.

## Codex MCP Config

Example `~/.codex/config.toml` entry:

```toml
[mcp_servers.tosKnowledge]
command = "python"
args = ["tools/tos-knowledge-mcp/server.py", "mcp"]
cwd = "/home/z3ck/Melia-TOS-Server/CloverTOS/server/app"
```

## Data Sources

Source definitions live in `sources/source_registry.json`.

The module treats sources with different trust levels:

1. Papaya traces and local captured behavior
2. CloverTOS local code/data
3. Extracted client/IPF data
4. Official IMC/Tree of Savior docs
5. TosBase and tos.guru public databases
6. Fandom/Reddit/manual community evidence

Discord is intentionally modeled as a manual-import source. The MCP cannot
access private Discord history unless an authorized export or connector output
is placed under `data/manual/papaya-discord/`.

## Generated Files

`data/tos_knowledge.sqlite` is generated and can be rebuilt at any time.
`data/web_cache/` is for fetched public pages when Cloud/local network access
is available. Cache entries should preserve source URL and capture time.
