#!/bin/bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVER_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
COMPOSE_FILE="$SERVER_DIR/docker/docker-compose.yml"

if [[ "${1:-}" == "--volumes" ]]; then
    docker compose -f "$COMPOSE_FILE" down -v
else
    docker compose -f "$COMPOSE_FILE" down
fi
