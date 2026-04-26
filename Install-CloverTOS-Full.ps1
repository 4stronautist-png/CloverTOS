param(
    [string]$Distro = "Ubuntu-20.04",
    [string]$WslUser = "",
    [string]$ClientDestination = "C:\CloverTOS-Local",
    [string]$HostName = "127.0.0.1",
    [int]$WebPort = 8080,
    [int]$BarracksPort = 2000,
    [int]$GroupId = 1001,
    [string]$ServerName = "Clover",
    [string]$SteamTosPath = "",
    [string]$AccountName = "clover",
    [string]$AccountPassword = "clover123",
    [switch]$SkipServer,
    [switch]$SkipClient,
    [switch]$KeepDb,
    [switch]$StartClient
)

$ErrorActionPreference = "Stop"

function Write-Step($Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Ok($Message) {
    Write-Host "OK  $Message" -ForegroundColor Green
}

function Invoke-Wsl {
    param([string]$Command)

    $args = @("-d", $Distro)
    if ($WslUser) {
        $args += @("-u", $WslUser)
    }
    $args += @("--", "bash", "-lc", $Command)

    & wsl.exe @args
    if ($LASTEXITCODE -ne 0) {
        throw "Comando WSL falhou com codigo $LASTEXITCODE"
    }
}

function Convert-ToWslPath {
    param([string]$Path)

    $resolved = (Resolve-Path -LiteralPath $Path).Path
    $converted = & wsl.exe -d $Distro -- wslpath -a "$resolved"
    if ($LASTEXITCODE -ne 0) {
        throw "Nao consegui converter caminho para WSL: $Path"
    }

    return $converted.Trim()
}

if (-not (Get-Command "wsl.exe" -ErrorAction SilentlyContinue)) {
    throw "wsl.exe nao encontrado. Instale/habilite o WSL antes de rodar este instalador."
}

$repoRoot = $PSScriptRoot
$serverScripts = Join-Path $repoRoot "server\scripts"
$clientInstaller = Join-Path $repoRoot "client\tools\Install-CloverTOS-Local.ps1"
$startClientScript = Join-Path $repoRoot "client\tools\Start-CloverTOS-Client.ps1"

if (-not $SkipServer) {
    Write-Step "Instalando servidor CloverTOS dentro do Ubuntu WSL"
    $wslScripts = Convert-ToWslPath $serverScripts
    $installArgs = @()
    if ($KeepDb) {
        $installArgs += "--keep-db"
    }

    $argText = $installArgs -join " "
    Invoke-Wsl "cd '$wslScripts' && PUBLIC_HOST='$HostName' PUBLIC_WEB_PORT='$WebPort' SERVER_NAME='$ServerName' GROUP_ID='$GroupId' DEFAULT_ACCOUNT='$AccountName' DEFAULT_PASSWORD='$AccountPassword' ./install-wsl.sh $argText"
    Write-Ok "Servidor instalado no WSL"
}

if (-not $SkipClient) {
    Write-Step "Instalando cliente local a partir do Tree of Savior da Steam"
    $clientArgs = @(
        "-NoProfile", "-ExecutionPolicy", "Bypass",
        "-File", $clientInstaller,
        "-Destination", $ClientDestination,
        "-HostName", $HostName,
        "-WebPort", "$WebPort",
        "-BarracksPort", "$BarracksPort",
        "-GroupId", "$GroupId",
        "-ServerName", $ServerName
    )

    if ($SteamTosPath) {
        $clientArgs += @("-SteamTosPath", $SteamTosPath)
    }

    & powershell.exe @clientArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao instalar o cliente CloverTOS."
    }

    Write-Ok "Cliente instalado"
}

if ($StartClient -and -not $SkipClient) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $startClientScript -Destination $ClientDestination
}

Write-Step "Finalizado"
Write-Host "Servidor: http://${HostName}:${WebPort}/toslive/patch/serverlist.xml" -ForegroundColor Yellow
Write-Host "Cliente: $ClientDestination\release\Start-CloverTOS-Local.bat" -ForegroundColor Yellow
Write-Host "Conta: $AccountName / $AccountPassword" -ForegroundColor Yellow
