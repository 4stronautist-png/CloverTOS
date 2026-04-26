param(
    [ValidateSet("install", "up", "down", "logs", "ps")]
    [string]$Action = "up",
    [string]$Distro = "Ubuntu-20.04",
    [string]$WslUser = "",
    [string]$PublicHost = "127.0.0.1",
    [string]$ServerName = "Clover",
    [int]$GroupId = 1001,
    [switch]$KeepDb,
    [switch]$NoStart
)

$ErrorActionPreference = "Stop"

function Write-Step($Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Ok($Message) {
    Write-Host "OK  $Message" -ForegroundColor Green
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$serverDir = Split-Path -Parent $scriptDir

function Convert-ToWslPath {
    param([string]$Path)

    $resolved = (Resolve-Path -LiteralPath $Path).Path
    $converted = & wsl.exe -d $Distro -- wslpath -a "$resolved"
    if ($LASTEXITCODE -ne 0) {
        throw "Nao consegui converter caminho para WSL: $Path"
    }

    return $converted.Trim()
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

Write-Step "Executando acao '$Action' para o CloverTOS"

$env:PUBLIC_HOST = $PublicHost
$env:SERVER_NAME = $ServerName
$env:GROUP_ID = "$GroupId"

$wslServerDir = Convert-ToWslPath $serverDir
$wslScriptDir = "$wslServerDir/scripts"

switch ($Action) {
    "install" {
        $installArgs = @()
        if ($KeepDb) {
            $installArgs += "--keep-db"
        }
        if ($NoStart) {
            $installArgs += "--no-start"
        }

        $suffix = ($installArgs -join " ")
        Invoke-Wsl "cd '$wslScriptDir' && PUBLIC_HOST='$PublicHost' SERVER_NAME='$ServerName' GROUP_ID='$GroupId' ./install-wsl.sh $suffix"
        Write-Ok "Ambiente CloverTOS instalado no WSL"
    }
    "up" {
        Invoke-Wsl "cd '$wslScriptDir' && PUBLIC_HOST='$PublicHost' SERVER_NAME='$ServerName' GROUP_ID='$GroupId' ./up.sh"
        Write-Ok "Ambiente CloverTOS iniciado"
    }
    "down" {
        Invoke-Wsl "cd '$wslScriptDir' && ./down.sh"
        Write-Ok "Ambiente CloverTOS parado"
    }
    "logs" {
        Invoke-Wsl "cd '$wslScriptDir' && ./logs.sh"
    }
    "ps" {
        Invoke-Wsl "ps -eo pid,ppid,stat,cmd | grep -E 'BarracksServer|ZoneServer|SocialServer|WebServer' | grep -v grep || true"
    }
}
