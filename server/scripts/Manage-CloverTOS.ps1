param(
    [ValidateSet("up", "down", "logs", "ps")]
    [string]$Action = "up",
    [switch]$RemoveVolumes
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
$repoRoot = Split-Path -Parent $serverDir
$wslRepoRoot = $repoRoot -replace '^\\\\wsl\.localhost\\Ubuntu-20\.04', ''
$wslRepoRoot = $wslRepoRoot -replace '\\', '/'
$composeFile = "$wslRepoRoot/server/docker/docker-compose.yml"

function Invoke-WslBash {
    param([string]$Command)

    & wsl.exe -d Ubuntu-20.04 -u z3ck -- bash -lc $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Comando WSL falhou com codigo $LASTEXITCODE"
    }
}

Write-Step "Executando acao '$Action' para o CloverTOS"

switch ($Action) {
    "up" {
        Invoke-WslBash "mkdir -p '$wslRepoRoot/server/runtime/logs' && docker compose -f '$composeFile' up -d --build && docker compose -f '$composeFile' ps"
        Write-Ok "Ambiente CloverTOS iniciado"
    }
    "down" {
        if ($RemoveVolumes) {
            Invoke-WslBash "docker compose -f '$composeFile' down -v"
            Write-Ok "Ambiente CloverTOS parado e volumes removidos"
        }
        else {
            Invoke-WslBash "docker compose -f '$composeFile' down"
            Write-Ok "Ambiente CloverTOS parado"
        }
    }
    "logs" {
        & wsl.exe -d Ubuntu-20.04 -u z3ck -- bash -lc "docker compose -f '$composeFile' logs -f"
        exit $LASTEXITCODE
    }
    "ps" {
        Invoke-WslBash "docker compose -f '$composeFile' ps"
    }
}
