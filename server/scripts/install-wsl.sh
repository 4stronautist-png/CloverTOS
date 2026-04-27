#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVER_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
APP_DIR="$SERVER_DIR/app"
DB_DUMP="$SERVER_DIR/db/clover_local.sql.gz"

DB_NAME="${DB_NAME:-clover_local}"
DB_USER="${DB_USER:-melia}"
DB_PASS="${DB_PASS:-melia123}"
SERVER_NAME="${SERVER_NAME:-Clover}"
GROUP_ID="${GROUP_ID:-1001}"
PUBLIC_HOST="${PUBLIC_HOST:-127.0.0.1}"
INTER_HOST="${INTER_HOST:-127.0.0.1}"
PUBLIC_WEB_PORT="${PUBLIC_WEB_PORT:-8080}"
DEFAULT_ACCOUNT="${DEFAULT_ACCOUNT:-clover}"
DEFAULT_PASSWORD="${DEFAULT_PASSWORD:-clover123}"

RESET_DB=1
START_AFTER_INSTALL=1

for arg in "$@"; do
	case "$arg" in
		--keep-db)
			RESET_DB=0
			;;
		--no-start)
			START_AFTER_INSTALL=0
			;;
		*)
			echo "[ERROR] Opcao desconhecida: $arg"
			exit 1
			;;
	esac
done

log() {
	echo ""
	echo "==> $1"
}

ok() {
	echo "OK  $1"
}

require_file() {
	if [ ! -f "$1" ]; then
		echo "[ERROR] Arquivo obrigatorio nao encontrado: $1"
		exit 1
	fi
}

install_dotnet() {
	if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks | grep -q '^8\.'; then
		ok ".NET SDK 8 encontrado"
		return
	fi

	log "Instalando Microsoft .NET SDK 8"
	local ubuntu_version
	ubuntu_version="$(. /etc/os-release && echo "$VERSION_ID")"
	local deb="/tmp/packages-microsoft-prod.deb"

	wget -q "https://packages.microsoft.com/config/ubuntu/${ubuntu_version}/packages-microsoft-prod.deb" -O "$deb"
	sudo dpkg -i "$deb"
	rm -f "$deb"
	sudo apt-get update
	sudo apt-get install -y dotnet-sdk-8.0
	ok ".NET SDK 8 instalado"
}

start_mariadb() {
	log "Iniciando MariaDB"

	if command -v systemctl >/dev/null 2>&1 && systemctl list-unit-files mariadb.service >/dev/null 2>&1; then
		sudo systemctl enable mariadb >/dev/null 2>&1 || true
		sudo systemctl start mariadb >/dev/null 2>&1 || true
	fi

	if ! mysqladmin ping --silent >/dev/null 2>&1; then
		sudo service mariadb start
	fi

	mysqladmin ping --silent
	ok "MariaDB respondendo"
}

configure_database() {
	log "Configurando banco ${DB_NAME}"

	sudo mysql <<SQL
CREATE DATABASE IF NOT EXISTS \`${DB_NAME}\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS '${DB_USER}'@'localhost' IDENTIFIED BY '${DB_PASS}';
CREATE USER IF NOT EXISTS '${DB_USER}'@'127.0.0.1' IDENTIFIED BY '${DB_PASS}';
GRANT ALL PRIVILEGES ON \`${DB_NAME}\`.* TO '${DB_USER}'@'localhost';
GRANT ALL PRIVILEGES ON \`${DB_NAME}\`.* TO '${DB_USER}'@'127.0.0.1';
FLUSH PRIVILEGES;
SQL

	if [ "$RESET_DB" -eq 1 ]; then
		require_file "$DB_DUMP"
		log "Restaurando dump ${DB_DUMP}"
		mysql -u "$DB_USER" -p"$DB_PASS" -e "DROP DATABASE IF EXISTS \`${DB_NAME}\`; CREATE DATABASE \`${DB_NAME}\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
		gzip -dc "$DB_DUMP" | mysql -u "$DB_USER" -p"$DB_PASS" "$DB_NAME"
		mysql -u "$DB_USER" -p"$DB_PASS" "$DB_NAME" < "$APP_DIR/tools/clover-repair-barracks.sql"
		ok "Dump restaurado"
	else
		mysql -u "$DB_USER" -p"$DB_PASS" "$DB_NAME" < "$APP_DIR/tools/clover-repair-barracks.sql"
		ok "Banco existente preservado por causa de --keep-db"
	fi
}

write_server_config() {
	log "Escrevendo configuracao local do Melia"

	mkdir -p "$APP_DIR/user/conf" "$APP_DIR/user/db" "$APP_DIR/logs"

	cat > "$APP_DIR/user/conf/database.conf" <<EOF
host     : 127.0.0.1
port     : 3306
user     : $DB_USER
pass     : $DB_PASS
database : $DB_NAME
EOF

	cat > "$APP_DIR/user/db/servers.txt" <<EOF
[
{ groupId: $GROUP_ID, name: "$SERVER_NAME", nation: "GLOBAL", servers: [
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

	ok "Configuracao do servidor pronta"
}

ensure_generated_user_files() {
	log "Garantindo estrutura user/ gerada"

	mkdir -p "$APP_DIR/user/scripts/zone" "$APP_DIR/user/web/register"

	if [ ! -f "$APP_DIR/user/scripts/zone/ScriptsZoneUser.csproj" ]; then
		cat > "$APP_DIR/user/scripts/zone/ScriptsZoneUser.csproj" <<'EOF'
<Project>
	<PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
		<OutputPath>..\..\..\user\tmp\build\scripts\zone_user\bin\</OutputPath>
		<BaseIntermediateOutputPath>..\..\..\user\tmp\build\scripts\zone_user\obj\</BaseIntermediateOutputPath>
	</PropertyGroup>
	<PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
		<OutputPath>..\..\..\user\tmp\build\scripts\zone_user\bin\</OutputPath>
		<BaseIntermediateOutputPath>..\..\..\user\tmp\build\scripts\zone_user\obj\</BaseIntermediateOutputPath>
	</PropertyGroup>
	<Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />
	<PropertyGroup>
		<TargetFramework>net8.0</TargetFramework>
		<OutputType>Library</OutputType>
		<GenerateAssemblyInfo>false</GenerateAssemblyInfo>
	</PropertyGroup>
	<Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />
	<ItemGroup>
		<ProjectReference Include="..\..\..\src\ZoneServer\ZoneServer.csproj" />
		<ProjectReference Include="..\..\..\src\Shared\Shared.csproj" />
	</ItemGroup>
</Project>
EOF
	fi

	if [ ! -f "$APP_DIR/user/scripts/zone/scripts_custom.txt" ]; then
		cat > "$APP_DIR/user/scripts/zone/scripts_custom.txt" <<'EOF'
// CloverTOS
// Add custom user zone scripts here, one relative path per line.
//---------------------------------------------------------------------------
EOF
	fi

	if [ ! -f "$APP_DIR/user/scripts/zone/scripts_content.txt" ]; then
		cat > "$APP_DIR/user/scripts/zone/scripts_content.txt" <<'EOF'
// CloverTOS
// Add custom user content scripts here, one relative path per line.
//---------------------------------------------------------------------------
EOF
	fi

	if [ ! -f "$APP_DIR/user/scripts/zone/.editorconfig" ]; then
		cat > "$APP_DIR/user/scripts/zone/.editorconfig" <<'EOF'
root = false

[*.cs]
indent_style = tab
indent_size = 4
EOF
	fi

	if [ ! -f "$APP_DIR/user/web/register/index.html" ]; then
		cat > "$APP_DIR/user/web/register/index.html" <<'EOF'
<!doctype html>
<html lang="en">
<head>
	<meta charset="utf-8">
	<meta name="viewport" content="width=device-width, initial-scale=1">
	<title>CloverTOS Account</title>
	<style>
		body { margin: 0; min-height: 100vh; display: grid; place-items: center; font-family: Segoe UI, sans-serif; background: #111820; color: #eef4fb; }
		main { width: min(420px, calc(100vw - 32px)); border: 1px solid #314255; background: #1b2530; padding: 28px; }
		label { display: block; margin-top: 12px; color: #a7b6c8; }
		input, button { width: 100%; box-sizing: border-box; padding: 12px; margin-top: 6px; }
		button { margin-top: 18px; border: 0; background: #4fd1a5; font-weight: 700; cursor: pointer; }
		#status { min-height: 22px; margin-top: 14px; }
	</style>
</head>
<body>
	<main>
		<h1>Create CloverTOS Account</h1>
		<form id="form">
			<label>Username<input id="username" minlength="4" required></label>
			<label>Password<input id="password1" type="password" minlength="6" required></label>
			<label>Confirm password<input id="password2" type="password" minlength="6" required></label>
			<button type="submit">Create account</button>
			<div id="status"></div>
		</form>
	</main>
	<script>
		const form = document.getElementById('form');
		const status = document.getElementById('status');
		form.addEventListener('submit', async event => {
			event.preventDefault();
			const body = {
				username: username.value.trim(),
				password1: password1.value,
				password2: password2.value
			};
			const response = await fetch('/api/account/create', {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify(body)
			});
			const data = await response.json().catch(() => ({}));
			status.textContent = response.ok ? 'Account created. You can log in now.' : (data.error || 'Failed to create account.');
		});
	</script>
</body>
</html>
EOF
	fi

	ok "Estrutura user/ pronta"
}

install_packages() {
	log "Instalando dependencias do Ubuntu WSL"

	sudo apt-get update
	sudo apt-get install -y \
		apt-transport-https \
		ca-certificates \
		curl \
		git \
		gnupg \
		gzip \
		iproute2 \
		lsof \
		mariadb-client \
		mariadb-server \
		procps \
		psmisc \
		wget

	ok "Dependencias APT instaladas"
}

build_server() {
	log "Compilando CloverTOS/Melia"
	cd "$APP_DIR"
	dotnet restore Melia.sln
	dotnet build Melia.sln -c Release --no-restore
	ok "Build concluido"
}

verify_windows_localhost() {
	if ! command -v powershell.exe >/dev/null 2>&1; then
		return
	fi

	log "Validando localhost do Windows"

	if powershell.exe -NoProfile -Command "\$ErrorActionPreference='Stop'; \$r=Invoke-WebRequest -UseBasicParsing -TimeoutSec 8 -Uri 'http://127.0.0.1:${PUBLIC_WEB_PORT}/toslive/patch/serverlist.xml'; if (\$r.Content -notmatch 'Server0_IP=\"127\.0\.0\.1\"') { throw 'serverlist nao aponta para 127.0.0.1' }" >/dev/null 2>&1; then
		ok "Windows acessa serverlist em 127.0.0.1:${PUBLIC_WEB_PORT}"
	else
		echo "[WARN] Windows ainda nao acessou http://127.0.0.1:${PUBLIC_WEB_PORT}/toslive/patch/serverlist.xml"
		echo "[WARN] O modo local nao usa portproxy. Verifique se o WSL esta encaminhando localhost para o Windows."
	fi

	if powershell.exe -NoProfile -Command "if (-not (Test-NetConnection -ComputerName 127.0.0.1 -Port 2000 -InformationLevel Quiet)) { exit 1 }" >/dev/null 2>&1; then
		ok "Windows acessa Barracks em 127.0.0.1:2000"
	else
		echo "[WARN] Windows ainda nao acessou o Barracks em 127.0.0.1:2000"
	fi
}

create_default_account() {
	if [ -z "$DEFAULT_ACCOUNT" ]; then
		return
	fi

	log "Criando/verificando conta padrao ${DEFAULT_ACCOUNT}"

	local body
	body="{\"username\":\"${DEFAULT_ACCOUNT}\",\"password1\":\"${DEFAULT_PASSWORD}\",\"password2\":\"${DEFAULT_PASSWORD}\"}"

	if curl -fsS \
		-H "Content-Type: application/json" \
		-d "$body" \
		"http://127.0.0.1:8080/api/account/create" >/tmp/clovertos-create-account.json 2>/tmp/clovertos-create-account.err; then
		ok "Conta pronta: ${DEFAULT_ACCOUNT}"
		return
	fi

	if grep -qi "already exists" /tmp/clovertos-create-account.json /tmp/clovertos-create-account.err 2>/dev/null; then
		ok "Conta ja existia: ${DEFAULT_ACCOUNT}"
		return
	fi

	echo "[WARN] Nao foi possivel criar a conta padrao agora. Veja /tmp/clovertos-create-account.err"
}

echo "=========================================="
echo "   CLOVERTOS WSL LOCAL INSTALL"
echo "=========================================="

if [ ! -f "$APP_DIR/Melia.sln" ]; then
	echo "[ERROR] Estrutura CloverTOS invalida. Rode a partir do repositorio clonado."
	exit 1
fi

install_packages
install_dotnet
start_mariadb
configure_database
write_server_config
ensure_generated_user_files
build_server

if [ "$START_AFTER_INSTALL" -eq 1 ]; then
	log "Subindo stack CloverTOS"
	cd "$APP_DIR"
	DB_NAME="$DB_NAME" DB_USER="$DB_USER" DB_PASS="$DB_PASS" GROUP_ID="$GROUP_ID" PUBLIC_HOST="$PUBLIC_HOST" PUBLIC_WEB_PORT="$PUBLIC_WEB_PORT" ./start-server.sh
	create_default_account
	verify_windows_localhost
else
	ok "Instalacao concluida sem iniciar o servidor"
fi

echo ""
echo "Servidor pronto."
echo "ServerListURL: http://127.0.0.1:${PUBLIC_WEB_PORT}/toslive/patch/serverlist.xml"
echo "Conta padrao: ${DEFAULT_ACCOUNT} / ${DEFAULT_PASSWORD}"
