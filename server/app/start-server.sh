#!/bin/bash

set -e

echo "=========================================="
echo "   CLOVER LOCAL SERVER STARTUP"
echo "=========================================="

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

SERVER_MODE="${SERVER_MODE:-local}"
PUBLIC_HOST="${PUBLIC_HOST:-127.0.0.1}"
PUBLIC_WEB_PORT="${PUBLIC_WEB_PORT:-8080}"
LOCAL_HOST="${LOCAL_HOST:-127.0.0.1}"
LOCAL_WEB_PORT="${LOCAL_WEB_PORT:-8080}"
WINDOWS_CLIENT_ROOT="${WINDOWS_CLIENT_ROOT:-/mnt/c/CloverTOS-Local}"
WINDOWS_CLIENT_XML="${WINDOWS_CLIENT_XML:-$WINDOWS_CLIENT_ROOT/release/client.xml}"
WINDOWS_USER_XML="${WINDOWS_USER_XML:-$WINDOWS_CLIENT_ROOT/release/user.xml}"
WINDOWS_START_BAT="${WINDOWS_START_BAT:-$WINDOWS_CLIENT_ROOT/release/Start-CloverTOS-Local.bat}"
WINDOWS_CLIENT_PATCH_RELEASE="${WINDOWS_CLIENT_PATCH_RELEASE:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)/client/patches/loading-screen/release}"
DB_NAME="${DB_NAME:-clover_local}"
DB_USER="${DB_USER:-melia}"
DB_PASS="${DB_PASS:-melia123}"
GROUP_ID="${GROUP_ID:-1001}"
CLOVER_CLIENT_VERSION="${CLOVER_CLIENT_VERSION:-402595}"
MELIA_PORTS=(2000 7001 7002 8080 9001 9002)

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

ensure_server_config() {
    local target_host
    target_host="$LOCAL_HOST"

    if [ "$SERVER_MODE" != "local" ]; then
        target_host="$PUBLIC_HOST"
    fi

    mkdir -p user/db
    cat > user/db/servers.txt <<EOF
[
{ groupId: 1001, name: "Clover", nation: "GLOBAL", servers: [
	{ type: "ISC", id: 1, ip: "127.0.0.1", port: 6001 },
	{ type: "Barracks", id: 1, ip: "$target_host", port: 2000, interHost: "127.0.0.1", interPort: 6001 },
	{ type: "Web", id: 1, ip: "$target_host", port: 8080 },
	{ type: "Zone", id: 1, ip: "$target_host", port: 7001, maps: "all" },
	{ type: "Zone", id: 2, ip: "$target_host", port: 7002, maps: "all" },
	{ type: "Social", id: 1, ip: "$target_host", port: 9001 },
	{ type: "Social", id: 2, ip: "$target_host", port: 9002 },
]},
]
EOF
}

ensure_windows_client_config() {
    local expected_url
    local expected_serverlist
    local expected_register

    mkdir -p "$WINDOWS_CLIENT_ROOT/release"

    if [ "$SERVER_MODE" = "local" ]; then
        expected_url="http://${LOCAL_HOST}:${LOCAL_WEB_PORT}/toslive/patch/"
    else
        expected_url="http://${PUBLIC_HOST}:${PUBLIC_WEB_PORT}/toslive/patch/"
    fi

    expected_serverlist="${expected_url}serverlist.xml"
    expected_register="http://${LOCAL_HOST}:${LOCAL_WEB_PORT}/register/index.html"

    if [ "$SERVER_MODE" != "local" ]; then
        expected_register="http://${PUBLIC_HOST}:${PUBLIC_WEB_PORT}/register/index.html"
    fi

    if [ -f "$WINDOWS_CLIENT_XML" ]; then
        cp "$WINDOWS_CLIENT_XML" "${WINDOWS_CLIENT_XML}.backup-$(date +%Y%m%d-%H%M%S)" || true
    fi

    cat > "$WINDOWS_CLIENT_XML" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<client>
<General Width="1280" Height="720" WindowMode="1" UseSteamClient="NO" />
<Display Shadow="3" AntiAliasing="0" VSync="0" FullScreenBloom="0" SSAO="0" />
<GameOption ServerListURL="$expected_serverlist" StaticConfigURL="${expected_url}" NewAccountURL="${expected_register}" PaymentURL="${expected_url}" LoadingImgURL="${expected_url}" LoadingImgCount="10"/>
<Locale ServiceNation="GLOBAL" Dictionary="YES" DefaultLanguage="English" />
<Security CheatCheck="NO" GameGuard="NO" XignCode="NO" />
</client>
EOF

    if [ -d "$WINDOWS_CLIENT_PATCH_RELEASE" ]; then
        cp -rf "$WINDOWS_CLIENT_PATCH_RELEASE"/. "$WINDOWS_CLIENT_ROOT/release/"
    else
        log_warning "Patch de loading screen nao encontrado em $WINDOWS_CLIENT_PATCH_RELEASE; criando launcher basico."
        cat > "$WINDOWS_START_BAT" <<'EOF'
@echo off
cd /d "%~dp0"
start "CloverTOS" "%~dp0Client_tos_x64.exe" -SERVICE GLOBAL
EOF
    fi

    if [ -f "$WINDOWS_USER_XML" ]; then
        rm -f "$WINDOWS_USER_XML"
    fi

    log_success "Cliente local configurado em $WINDOWS_CLIENT_ROOT"
}

wait_for_port() {
    local port=$1
    local timeout=${2:-45}
    local elapsed=0

    while [ "$elapsed" -lt "$timeout" ]; do
        if ss -ltn "( sport = :$port )" | grep -q ":$port"; then
            return 0
        fi
        sleep 2
        elapsed=$((elapsed + 2))
    done

    return 1
}

wait_for_log_pattern() {
    local log_file=$1
    local pattern=$2
    local timeout=${3:-60}
    local elapsed=0

    while [ "$elapsed" -lt "$timeout" ]; do
        if [ -f "$log_file" ] && tail -n 200 "$log_file" | grep -q "$pattern"; then
            return 0
        fi
        sleep 2
        elapsed=$((elapsed + 2))
    done

    return 1
}

wait_for_pid_exit() {
    local pid=$1
    local timeout=${2:-120}
    local elapsed=0

    while [ "$elapsed" -lt "$timeout" ]; do
        if ! kill -0 "$pid" 2>/dev/null; then
            return 0
        fi
        sleep 2
        elapsed=$((elapsed + 2))
    done

    return 1
}

mysql_ping() {
    mysqladmin -u "$DB_USER" -p"$DB_PASS" ping >/dev/null 2>&1
}

start_database_service() {
    local services=("mariadb" "mysql")
    local service_name

    for service_name in "${services[@]}"; do
        if command -v service >/dev/null 2>&1 && service "$service_name" status >/dev/null 2>&1; then
            log_info "Servico $service_name ja esta ativo."
            return 0
        fi
    done

    for service_name in "${services[@]}"; do
        if command -v service >/dev/null 2>&1 && service "$service_name" status >/dev/null 2>&1; then
            continue
        fi

        if command -v sudo >/dev/null 2>&1; then
            if sudo -n service "$service_name" start >/dev/null 2>&1; then
                log_success "Servico $service_name iniciado."
                return 0
            fi
        elif service "$service_name" start >/dev/null 2>&1; then
            log_success "Servico $service_name iniciado."
            return 0
        fi
    done

    log_warning "Nao consegui iniciar MariaDB/MySQL automaticamente. Inicie com: sudo service mariadb start"
    return 1
}

wait_for_database() {
    local timeout=${1:-60}
    local elapsed=0

    if mysql_ping; then
        log_success "MariaDB respondeu com $DB_USER."
        return 0
    fi

    log_warning "MariaDB ainda nao respondeu; tentando iniciar o servico..."
    start_database_service || true

    while [ "$elapsed" -lt "$timeout" ]; do
        if mysql_ping; then
            log_success "MariaDB respondeu com $DB_USER."
            return 0
        fi
        sleep 2
        elapsed=$((elapsed + 2))
    done

    log_error "MariaDB nao respondeu com $DB_USER apos ${timeout}s."
    return 1
}

verify_http_endpoint() {
    local url=$1
    if curl -fsS --max-time 8 "$url" >/tmp/laima-serverlist-check.xml; then
        if grep -q "Server0_IP=" /tmp/laima-serverlist-check.xml; then
            log_success "Endpoint OK: $url"
            return 0
        fi
    fi
    log_error "Endpoint nao respondeu corretamente: $url"
    return 1
}

verify_windows_client_connectivity() {
    local client_host
    client_host="$LOCAL_HOST"

    if [ "$SERVER_MODE" != "local" ]; then
        client_host="$PUBLIC_HOST"
    fi

    if ! command -v powershell.exe >/dev/null 2>&1; then
        log_warning "powershell.exe nao encontrado no WSL; pulando validacao do localhost do Windows."
        return 0
    fi

    log_info "Validando conectividade do cliente Windows em ${client_host}..."

    if powershell.exe -NoProfile -Command "\$ErrorActionPreference='Stop'; \$hostName='${client_host}'; \$r=Invoke-WebRequest -UseBasicParsing -TimeoutSec 8 -Uri \"http://\$(\$hostName):${PUBLIC_WEB_PORT}/toslive/patch/serverlist.xml\"; if (-not \$r.Content.Contains('Server0_IP=\"' + \$hostName + '\"')) { throw \"serverlist nao aponta para \$hostName\" }" >/dev/null 2>&1; then
        log_success "Cliente Windows consegue acessar serverlist em ${client_host}:${PUBLIC_WEB_PORT}"
    else
        log_error "Cliente Windows nao consegue acessar http://${client_host}:${PUBLIC_WEB_PORT}/toslive/patch/serverlist.xml"
        log_error "Verifique firewall/antivirus, portproxy do Windows ou encaminhamento localhost do WSL."
        return 1
    fi

    if powershell.exe -NoProfile -Command "if (-not (Test-NetConnection -ComputerName '${client_host}' -Port 2000 -InformationLevel Quiet)) { exit 1 }" >/dev/null 2>&1; then
        log_success "Cliente Windows consegue acessar Barracks em ${client_host}:2000"
    else
        log_error "Cliente Windows nao consegue acessar Barracks em ${client_host}:2000"
        return 1
    fi
}

verify_account_api() {
    local account_name="clover_api_check_$(date +%s)"
    local account_pass="clover123"
    local body
    body="{\"username\":\"${account_name}\",\"password1\":\"${account_pass}\",\"password2\":\"${account_pass}\"}"

    log_info "Validando API de criacao de conta..."

    if curl -fsS \
        -H "Content-Type: application/json" \
        -d "$body" \
        "http://127.0.0.1:8080/api/account/create" >/tmp/clovertos-account-api-check.json 2>/tmp/clovertos-account-api-check.err; then
        if mysql -u "$DB_USER" -p"$DB_PASS" "$DB_NAME" -N -B -e "SELECT COUNT(*) FROM accounts WHERE name='${account_name}';" 2>/dev/null | grep -q '^1$'; then
            log_success "API de conta gravou no banco corretamente."
            return 0
        fi

        log_error "API de conta respondeu, mas a conta nao apareceu no banco."
        return 1
    fi

    log_error "API de criacao de conta nao respondeu corretamente."
    if [ -s /tmp/clovertos-account-api-check.err ]; then
        tail -n 20 /tmp/clovertos-account-api-check.err
    fi
	return 1
}

repair_barracks_database() {
    local repair_sql="tools/clover-repair-barracks.sql"

    if [ ! -f "$repair_sql" ]; then
        log_warning "SQL de reparo do barracks nao encontrado: $repair_sql"
        return 0
    fi

    log_info "Reparando slots/selectedSlot do barracks..."
    mysql -u "$DB_USER" -p"$DB_PASS" "$DB_NAME" < "$repair_sql"
    log_success "Barracks DB OK"
}

start_server() {
    local server_name=$1
    local project_path=$2
    local args=$3
    local log_file="logs/${server_name}.log"

    mkdir -p logs

    nohup dotnet run --project "$project_path" -c Release --no-build $args > "$log_file" 2>&1 &
    local pid=$!
    sleep 3

    if kill -0 "$pid" 2>/dev/null; then
        log_success "$server_name iniciado (PID: $pid)"
        echo "$pid" >> pids.txt
        return 0
    fi

    log_error "$server_name falhou ao iniciar. Verifique $log_file"
    return 1
}

if [ ! -f "Melia.sln" ]; then
    log_error "Execute dentro da pasta server/app do CloverTOS"
    exit 1
fi

mkdir -p user/conf user/db user/versions tools logs
printf '#define VERSION %s\n' "$CLOVER_CLIENT_VERSION" > user/versions/version.txt

ensure_server_config
ensure_windows_client_config

if ! command -v dotnet >/dev/null 2>&1; then
    log_error ".NET SDK nao encontrado."
    exit 1
fi

if ! command -v mysql >/dev/null 2>&1; then
    log_error "Cliente MySQL nao encontrado."
    exit 1
fi

if ! wait_for_database 90; then
    exit 1
fi

if ! mysql -u "$DB_USER" -p"$DB_PASS" -e "USE ${DB_NAME}; SELECT 1;" >/dev/null 2>&1; then
    log_error "Banco ${DB_NAME} nao esta acessivel."
    exit 1
fi

repair_barracks_database

log_info "Compilando Release para garantir binarios atualizados..."
dotnet build Melia.sln -c Release

if [ -f "./stop-server.sh" ]; then
    ./stop-server.sh >/dev/null 2>&1 || true
fi

rm -f pids.txt

for port in 2000 7001 7002 8080 9001 9002; do
    if lsof -Pi :"$port" -sTCP:LISTEN -t >/dev/null 2>&1; then
        log_warning "Porta $port ja estava em uso."
    fi
done

start_server "BarracksServer" "src/BarracksServer/BarracksServer.csproj" "${GROUP_ID} 1"

if ! wait_for_port 2000 180; then
    log_warning "Barracks ainda nao abriu a porta 2000; continuando e deixando os demais servicos aguardarem o coordinator."
fi

start_server "ZoneServer1" "src/ZoneServer/ZoneServer.csproj" "${GROUP_ID} 1"
start_server "ZoneServer2" "src/ZoneServer/ZoneServer.csproj" "${GROUP_ID} 2"
start_server "SocialServer1" "src/SocialServer/SocialServer.csproj" "${GROUP_ID} 1"
start_server "SocialServer2" "src/SocialServer/SocialServer.csproj" "${GROUP_ID} 2"
start_server "WebServer" "src/WebServer/WebServer.csproj" "${GROUP_ID} 1"

sleep 5
final_check_failed=0

for port in 2000 7001 7002 8080 9001 9002; do
    if wait_for_port "$port" 180; then
        log_success "Porta OK: $port"
    else
        log_error "Porta nao escutando: $port"
        final_check_failed=1
    fi
done

if verify_http_endpoint "http://127.0.0.1:8080/toslive/patch/serverlist.xml"; then
    :
else
    final_check_failed=1
fi

if verify_windows_client_connectivity; then
    :
else
    final_check_failed=1
fi

if verify_account_api; then
    :
else
    final_check_failed=1
fi

for log_name in ZoneServer1 SocialServer1 WebServer; do
    if wait_for_log_pattern "logs/${log_name}.log" "Successfully connected to coordinator" 180; then
        log_success "$log_name conectado ao coordinator"
    else
        log_error "$log_name nao confirmou coordinator"
        final_check_failed=1
    fi
done

for log_name in ZoneServer1 ZoneServer2; do
    if grep -q "loaded 0 scripts from" "logs/${log_name}.log"; then
        log_error "$log_name carregou 0 scripts"
        final_check_failed=1
    elif grep -q "loaded .* scripts from" "logs/${log_name}.log"; then
        log_success "$log_name carregou scripts"
    else
        log_error "$log_name nao informou carregamento de scripts"
        final_check_failed=1
    fi
done

if [ "$final_check_failed" -ne 0 ]; then
    log_error "Clover subiu com falhas. Veja logs/*.log"
    exit 1
fi

log_success "Clover local pronto."
log_info "Cliente Windows: $WINDOWS_CLIENT_ROOT"
log_info "ServerListURL: http://${LOCAL_HOST}:${LOCAL_WEB_PORT}/toslive/patch/serverlist.xml"
