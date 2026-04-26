#!/bin/bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVER_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
COMPOSE_FILE="$SERVER_DIR/docker/docker-compose.yml"

if ! command -v docker >/dev/null 2>&1; then
    echo "[ERROR] Docker nao encontrado no WSL."
    exit 1
fi

if ! docker compose version >/dev/null 2>&1; then
    echo "[ERROR] Docker Compose v2 nao encontrado."
    exit 1
fi

mkdir -p "$SERVER_DIR/runtime/logs"

echo "=========================================="
echo "   CLOVER DOCKER BOOTSTRAP"
echo "=========================================="

docker compose -f "$COMPOSE_FILE" up -d --build
docker compose -f "$COMPOSE_FILE" ps

echo ""
echo "ServerListURL esperado: http://127.0.0.1:18080/toslive/patch/serverlist.xml"
