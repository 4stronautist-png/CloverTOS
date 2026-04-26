param(
    [string]$InstallRoot = "C:\CloverTOS",
    [string]$ClientDestination = "C:\CloverTOS-Local",
    [string]$RepoUrl = "https://github.com/4stronautist-png/CloverTOS.git",
    [string]$Branch = "main",
    [string]$HostName = "127.0.0.1",
    [int]$WebPort = 18080,
    [int]$BarracksPort = 2000,
    [int]$GroupId = 1001,
    [string]$ServerName = "Clover",
    [string]$SteamTosPath = "",
    [string]$AccountName = "clover",
    [string]$AccountPassword = "clover123",
    [switch]$SkipPrerequisites,
    [switch]$SkipClient,
    [switch]$SkipAccount,
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

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-Command($Name) {
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function Invoke-Checked {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath falhou com codigo $LASTEXITCODE"
    }
}

function Install-WingetPackage {
    param(
        [string]$Id,
        [string]$Name
    )

    if (-not (Test-Command "winget.exe")) {
        throw "winget nao encontrado. Instale o App Installer pela Microsoft Store ou instale $Name manualmente."
    }

    Write-Step "Instalando $Name via winget"
    Invoke-Checked "winget.exe" @("install", "--id", $Id, "--exact", "--accept-package-agreements", "--accept-source-agreements", "--silent")
    Write-Ok "$Name instalado/verificado"
}

function Ensure-Wsl {
    if (-not (Get-Command "wsl.exe" -ErrorAction SilentlyContinue)) {
        throw "wsl.exe nao encontrado. Atualize o Windows ou instale WSL manualmente antes de instalar o Docker Desktop."
    }

    wsl.exe --status *> $null
    if ($LASTEXITCODE -eq 0) {
        Write-Ok "WSL encontrado"
        return
    }

    if (-not (Test-Admin)) {
        throw "WSL nao esta habilitado. Abra o PowerShell como administrador e rode novamente."
    }

    Write-Step "Habilitando WSL"
    & wsl.exe --install --no-distribution
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao habilitar WSL. Habilite WSL manualmente e rode o script de novo."
    }

    throw "WSL foi habilitado. Reinicie o Windows e rode este script novamente."
}

function Ensure-Prerequisites {
    if ($SkipPrerequisites) {
        Write-Host "Pulando pre-requisitos por causa de -SkipPrerequisites." -ForegroundColor Yellow
        return
    }

    Ensure-Wsl

    if (-not (Test-Command "git.exe")) {
        Install-WingetPackage -Id "Git.Git" -Name "Git"
        $env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [Environment]::GetEnvironmentVariable("Path", "User")
    }
    else {
        Write-Ok "Git encontrado"
    }

    if (-not (Test-Command "docker.exe")) {
        if (-not (Test-Admin)) {
            throw "Docker Desktop nao encontrado. Abra o PowerShell como administrador e rode novamente, ou instale Docker Desktop manualmente."
        }

        Install-WingetPackage -Id "Docker.DockerDesktop" -Name "Docker Desktop"
        $env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [Environment]::GetEnvironmentVariable("Path", "User")
    }
    else {
        Write-Ok "Docker encontrado"
    }
}

function Start-DockerDesktop {
    Write-Step "Verificando Docker Desktop"

    docker info *> $null
    if ($LASTEXITCODE -eq 0) {
        Write-Ok "Docker ja esta respondendo"
        return
    }

    $dockerDesktop = Join-Path $env:ProgramFiles "Docker\Docker\Docker Desktop.exe"
    if (Test-Path -LiteralPath $dockerDesktop) {
        Start-Process -FilePath $dockerDesktop | Out-Null
    }

    $deadline = (Get-Date).AddMinutes(5)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 5
        docker info *> $null
        if ($LASTEXITCODE -eq 0) {
            Write-Ok "Docker Desktop pronto"
            return
        }
    }

    throw "Docker Desktop nao respondeu. Se ele acabou de instalar WSL2, reinicie o Windows e rode este script de novo."
}

function Get-RepoPath {
    $scriptRoot = $PSScriptRoot
    if ((Test-Path -LiteralPath (Join-Path $scriptRoot "server")) -and (Test-Path -LiteralPath (Join-Path $scriptRoot "client"))) {
        return $scriptRoot
    }

    return $InstallRoot
}

function Sync-Repository {
    $repoPath = Get-RepoPath
    $scriptRoot = $PSScriptRoot

    if ($repoPath -eq $scriptRoot -and (Test-Path -LiteralPath (Join-Path $repoPath "server")) -and (Test-Path -LiteralPath (Join-Path $repoPath "client"))) {
        Write-Ok "Usando checkout local: $repoPath"
    }
    elseif (Test-Path -LiteralPath (Join-Path $repoPath ".git")) {
        Write-Step "Atualizando repositorio CloverTOS"
        Invoke-Checked "git.exe" @("-C", $repoPath, "fetch", "origin", $Branch)
        Invoke-Checked "git.exe" @("-C", $repoPath, "checkout", $Branch)
        Invoke-Checked "git.exe" @("-C", $repoPath, "pull", "--ff-only", "origin", $Branch)
    }
    elseif (Test-Path -LiteralPath $repoPath) {
        if ((Get-ChildItem -LiteralPath $repoPath -Force | Select-Object -First 1)) {
            throw "A pasta $repoPath ja existe e nao esta vazia. Use -InstallRoot com outra pasta ou remova a pasta manualmente."
        }

        Write-Step "Clonando CloverTOS"
        Invoke-Checked "git.exe" @("clone", "--branch", $Branch, $RepoUrl, $repoPath)
    }
    else {
        Write-Step "Clonando CloverTOS"
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $repoPath) | Out-Null
        Invoke-Checked "git.exe" @("clone", "--branch", $Branch, $RepoUrl, $repoPath)
    }

    return $repoPath
}

function Wait-ServerReady {
    param([string]$Url)

    Write-Step "Aguardando serverlist do CloverTOS"
    $deadline = (Get-Date).AddMinutes(4)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 8
            if ($response.Content -like "*Server0_IP=*") {
                Write-Ok "Serverlist respondendo: $Url"
                return
            }
        }
        catch {
        }

        Start-Sleep -Seconds 5
    }

    throw "Servidor nao respondeu em $Url. Veja os logs com server\scripts\Manage-CloverTOS.ps1 -Action logs."
}

Ensure-Prerequisites
Start-DockerDesktop

$repoPath = Sync-Repository

Write-Step "Subindo servidor CloverTOS"
$manageScript = Join-Path $repoPath "server\scripts\Manage-CloverTOS.ps1"
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $manageScript -Action up -PublicHost $HostName -ServerName $ServerName -GroupId $GroupId
if ($LASTEXITCODE -ne 0) {
    throw "Falha ao subir o servidor CloverTOS."
}

$serverListUrl = "http://${HostName}:${WebPort}/toslive/patch/serverlist.xml"
Wait-ServerReady -Url $serverListUrl

if (-not $SkipClient) {
    Write-Step "Instalando cliente CloverTOS"
    $clientInstaller = Join-Path $repoPath "client\tools\Install-CloverTOS-Local.ps1"
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
}

if (-not $SkipAccount) {
    $accountScript = Join-Path $repoPath "server\scripts\Create-CloverTOS-Account.ps1"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $accountScript -Username $AccountName -Password $AccountPassword -HostName $HostName -WebPort $WebPort
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao criar a conta CloverTOS."
    }
}

if ($StartClient -and -not $SkipClient) {
    $startClientScript = Join-Path $repoPath "client\tools\Start-CloverTOS-Client.ps1"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $startClientScript -Destination $ClientDestination
}

Write-Step "Finalizado"
Write-Host "Servidor: $serverListUrl" -ForegroundColor Yellow
Write-Host "Cliente: $ClientDestination\release\Start-CloverTOS-Local.bat" -ForegroundColor Yellow

if (-not $SkipAccount) {
    Write-Host "Conta pronta: $AccountName / $AccountPassword" -ForegroundColor Yellow
}
else {
    Write-Host "Para criar conta pelo login do jogo, use usuario new__NOME na primeira entrada; depois entre como NOME." -ForegroundColor Yellow
}
