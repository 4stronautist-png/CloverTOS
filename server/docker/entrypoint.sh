#!/bin/bash

set -euo pipefail

APP_DIR="/opt/clover/server/app"
LOG_DIR="$APP_DIR/logs"
PIDS_FILE="$APP_DIR/pids.txt"

DB_HOST="${DB_HOST:-db}"
DB_PORT="${DB_PORT:-3306}"
DB_USER="${DB_USER:-melia}"
DB_PASS="${DB_PASS:-melia123}"
DB_NAME="${DB_NAME:-clover_local}"

SERVER_NAME="${SERVER_NAME:-Clover}"
SERVER_NATION="${SERVER_NATION:-GLOBAL}"
GROUP_ID="${GROUP_ID:-1001}"
PUBLIC_HOST="${PUBLIC_HOST:-127.0.0.1}"
INTER_HOST="${INTER_HOST:-127.0.0.1}"

RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

cleanup() {
    if [ -f "$PIDS_FILE" ]; then
        while read -r pid; do
            if kill -0 "$pid" 2>/dev/null; then
                kill "$pid" 2>/dev/null || true
            fi
        done < "$PIDS_FILE"
    fi
}

trap cleanup EXIT INT TERM

mkdir -p "$APP_DIR/user/conf" "$APP_DIR/user/db" "$LOG_DIR"
rm -f "$PIDS_FILE"

cat > "$APP_DIR/user/conf/database.conf" <<EOF
host     : $DB_HOST
port     : $DB_PORT
user     : $DB_USER
pass     : $DB_PASS
database : $DB_NAME
EOF

cat > "$APP_DIR/user/db/servers.txt" <<EOF
[
{ groupId: $GROUP_ID, name: "$SERVER_NAME", nation: "$SERVER_NATION", servers: [
	{ type: "ISC", id: 1, ip: "$INTER_HOST", port: 6001 },
	{ type: "Barracks", id: 1, ip: "$PUBLIC_HOST", port: 2000, interHost: "$INTER_HOST", interPort: 6001 },
	{ type: "Web", id: 1, ip: "$PUBLIC_HOST", port: 8080 },
	{ type: "Zone", id: 1, ip: "$PUBLIC_HOST", port: 7001, maps: "all" },
	{ type: "Zone", id: 2, ip: "$PUBLIC_HOST", port: 7002, maps: "all" },
	{ type: "Social", id: 1, ip: "$PUBLIC_HOST", port: 9001 },
	{ type: "Social", id: 2, ip: "$PUBLIC_HOST", port: 9002 },
]},
]
EOF

log_info "Aguardando MariaDB em ${DB_HOST}:${DB_PORT}..."
until mysqladmin ping -h"$DB_HOST" -P"$DB_PORT" -u"$DB_USER" -p"$DB_PASS" --silent; do
    sleep 2
done
log_success "MariaDB pronto."

cd "$APP_DIR"

start_server() {
    local server_name="$1"
    local project_path="$2"
    local log_file="$LOG_DIR/${server_name}.log"

    nohup dotnet run --project "$project_path" -c Release --no-build > "$log_file" 2>&1 &
    local pid=$!
    echo "$pid" >> "$PIDS_FILE"
    sleep 2

    if kill -0 "$pid" 2>/dev/null; then
        log_success "$server_name iniciado (PID: $pid)"
    else
        log_error "$server_name falhou ao iniciar."
        tail -n 80 "$log_file" || true
        exit 1
    fi
}

start_server "BarracksServer" "src/BarracksServer"
start_server "ZoneServer1" "src/ZoneServer" "--name ZoneServer1"
start_server "ZoneServer2" "src/ZoneServer" "--name ZoneServer2"
start_server "SocialServer1" "src/SocialServer" "--name SocialServer1"
start_server "SocialServer2" "src/SocialServer" "--name SocialServer2"
start_server "WebServer" "src/WebServer"

log_success "Clover server container pronto."

wait -n
log_error "Um dos processos do Clover encerrou. Derrubando container."
exit 1
