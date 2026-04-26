param(
    [ValidateSet("up", "down", "logs", "ps")]
    [string]$Action = "up",
    [string]$PublicHost = "127.0.0.1",
    [string]$ServerName = "Clover",
    [int]$GroupId = 1001,
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
$composeFile = Join-Path $serverDir "docker\docker-compose.yml"

function Invoke-DockerCompose {
    param([string[]]$Arguments)

    & docker @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Docker falhou com codigo $LASTEXITCODE"
    }
}

Write-Step "Executando acao '$Action' para o CloverTOS"

$env:PUBLIC_HOST = $PublicHost
$env:SERVER_NAME = $ServerName
$env:GROUP_ID = "$GroupId"

switch ($Action) {
    "up" {
        New-Item -ItemType Directory -Force -Path (Join-Path $serverDir "runtime\logs") | Out-Null
        Invoke-DockerCompose @("compose", "-f", $composeFile, "up", "-d", "--build")
        Invoke-DockerCompose @("compose", "-f", $composeFile, "ps")
        Write-Ok "Ambiente CloverTOS iniciado"
    }
    "down" {
        if ($RemoveVolumes) {
            Invoke-DockerCompose @("compose", "-f", $composeFile, "down", "-v")
            Write-Ok "Ambiente CloverTOS parado e volumes removidos"
        }
        else {
            Invoke-DockerCompose @("compose", "-f", $composeFile, "down")
            Write-Ok "Ambiente CloverTOS parado"
        }
    }
    "logs" {
        & docker compose -f $composeFile logs -f
        exit $LASTEXITCODE
    }
    "ps" {
        Invoke-DockerCompose @("compose", "-f", $composeFile, "ps")
    }
}
