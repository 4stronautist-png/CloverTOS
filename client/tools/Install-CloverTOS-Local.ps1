param(
    [string]$Destination = "C:\CloverTOS-Local",
    [string]$HostName = "127.0.0.1",
    [int]$WebPort = 8080,
    [int]$BarracksPort = 2000,
    [int]$GroupId = 1001,
    [string]$ServerName = "Clover",
    [string]$SteamTosPath = "",
    [string]$ExpectedRevision = "402595",
    [switch]$SkipPrerequisites
)

$ErrorActionPreference = "Stop"

function Write-Step($Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Ok($Message) {
    Write-Host "OK  $Message" -ForegroundColor Green
}

function Test-SourceClientVersion {
    param([string]$SourcePath)

    $revisionPath = Join-Path $SourcePath "release\release.revision.txt"
    if (-not (Test-Path -LiteralPath $revisionPath)) {
        throw "Nao encontrei release.revision.txt no client Steam: $revisionPath"
    }

    $revision = (Get-Content -LiteralPath $revisionPath -Raw).Trim()
    if ($revision -ne $ExpectedRevision) {
        throw "Versao do Tree of Savior incompativel. Esperado release $ExpectedRevision, encontrado $revision. Atualize/ajuste a instalacao Steam para a mesma versao do ambiente CloverTOS."
    }

    $expectedFiles = @{
        "release\Client_tos_x64.exe" = 29056352
        "data\ies.ipf" = 3077599
        "data\ui.ipf" = 347228473
        "data\script_client.ipf" = 42738
        "data\xml.ipf" = 1688308
    }

    foreach ($relativePath in $expectedFiles.Keys) {
        $path = Join-Path $SourcePath $relativePath
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Arquivo obrigatorio ausente no client Steam: $relativePath"
        }

        $actualLength = (Get-Item -LiteralPath $path).Length
        $expectedLength = $expectedFiles[$relativePath]
        if ($actualLength -ne $expectedLength) {
            throw "Arquivo do client incompativel: $relativePath tem $actualLength bytes, esperado $expectedLength. Use a mesma instalacao/revisao de client do ambiente CloverTOS."
        }
    }

    Write-Ok "Client Steam compativel com release $ExpectedRevision"
}

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-SystemDll {
    param([string]$Name)

    $paths = @(
        (Join-Path $env:WINDIR "System32\$Name"),
        (Join-Path $env:WINDIR "SysWOW64\$Name")
    )

    foreach ($path in $paths) {
        if (Test-Path -LiteralPath $path) {
            return $true
        }
    }

    return $false
}

function Invoke-Installer {
    param(
        [string]$Name,
        [string]$Url,
        [string]$FileName,
        [string[]]$Arguments
    )

    $tempDir = Join-Path $env:TEMP "CloverTOS-Prereqs"
    New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

    $installerPath = Join-Path $tempDir $FileName

    Write-Step "Baixando $Name"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $Url -OutFile $installerPath -UseBasicParsing

    Write-Step "Instalando $Name"
    $process = Start-Process -FilePath $installerPath -ArgumentList $Arguments -Wait -PassThru

    if ($process.ExitCode -ne 0 -and $process.ExitCode -ne 3010) {
        throw "$Name falhou com codigo $($process.ExitCode)."
    }

    Write-Ok "$Name instalado/verificado"
}

function Install-Prerequisites {
    $missingDirectX = -not (Test-SystemDll "XINPUT1_3.dll")
    $missingVcRuntime = @(
        "MSVCP140.dll",
        "CONCRT140.dll",
        "VCOMP140.dll"
    ) | Where-Object { -not (Test-SystemDll $_) }

    if (-not $missingDirectX -and $missingVcRuntime.Count -eq 0) {
        Write-Ok "Pre-requisitos do Windows ja encontrados"
        return
    }

    if (-not (Test-Admin)) {
        throw "Faltam pre-requisitos do Windows. Abra o PowerShell como administrador e rode o script novamente."
    }

    if ($missingVcRuntime.Count -gt 0) {
        Invoke-Installer `
            -Name "Microsoft Visual C++ Redistributable x64" `
            -Url "https://aka.ms/vc14/vc_redist.x64.exe" `
            -FileName "vc_redist.x64.exe" `
            -Arguments @("/install", "/quiet", "/norestart")

        Invoke-Installer `
            -Name "Microsoft Visual C++ Redistributable x86" `
            -Url "https://aka.ms/vc14/vc_redist.x86.exe" `
            -FileName "vc_redist.x86.exe" `
            -Arguments @("/install", "/quiet", "/norestart")
    }

    if ($missingDirectX) {
        Invoke-Installer `
            -Name "Microsoft DirectX End-User Runtime" `
            -Url "https://download.microsoft.com/download/1/7/1/1718ccc4-6315-4d8e-9543-8e28a4e18c4c/dxwebsetup.exe" `
            -FileName "dxwebsetup.exe" `
            -Arguments @("/Q")
    }
}

function Get-SteamInstallPath {
    $registryPaths = @(
        "HKCU:\Software\Valve\Steam",
        "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam",
        "HKLM:\SOFTWARE\Valve\Steam"
    )

    foreach ($path in $registryPaths) {
        try {
            $value = (Get-ItemProperty -LiteralPath $path -ErrorAction Stop).SteamPath
            if ($value -and (Test-Path -LiteralPath $value)) {
                return $value
            }
        }
        catch {
        }
    }

    $commonPaths = @(
        "${env:ProgramFiles(x86)}\Steam",
        "$env:ProgramFiles\Steam"
    )

    foreach ($path in $commonPaths) {
        if ($path -and (Test-Path -LiteralPath $path)) {
            return $path
        }
    }

    return $null
}

function Get-SteamLibraries($SteamPath) {
    $libraries = New-Object System.Collections.Generic.List[string]
    $defaultLibrary = Join-Path $SteamPath "steamapps"
    if (Test-Path -LiteralPath $defaultLibrary) {
        $libraries.Add($defaultLibrary)
    }

    $libraryFolders = Join-Path $SteamPath "steamapps\libraryfolders.vdf"
    if (Test-Path -LiteralPath $libraryFolders) {
        $content = Get-Content -LiteralPath $libraryFolders -Raw
        $matches = [regex]::Matches($content, '"path"\s+"([^"]+)"')

        foreach ($match in $matches) {
            $libraryRoot = $match.Groups[1].Value -replace "\\\\", "\"
            $steamApps = Join-Path $libraryRoot "steamapps"
            if ((Test-Path -LiteralPath $steamApps) -and -not $libraries.Contains($steamApps)) {
                $libraries.Add($steamApps)
            }
        }
    }

    return $libraries
}

function Find-TreeOfSaviorPath {
    param([string]$ExplicitPath)

    if ($ExplicitPath) {
        if (Test-Path -LiteralPath (Join-Path $ExplicitPath "release\Client_tos_x64.exe")) {
            return (Resolve-Path -LiteralPath $ExplicitPath).Path
        }

        throw "O caminho informado em -SteamTosPath nao parece ser uma instalacao valida do Tree of Savior: $ExplicitPath"
    }

    $steamPath = Get-SteamInstallPath
    if (-not $steamPath) {
        throw "Nao consegui localizar o Steam. Use -SteamTosPath com o caminho da pasta TreeOfSavior."
    }

    $libraries = Get-SteamLibraries $steamPath
    foreach ($steamApps in $libraries) {
        $candidate = Join-Path $steamApps "common\TreeOfSavior"
        if (Test-Path -LiteralPath (Join-Path $candidate "release\Client_tos_x64.exe")) {
            return $candidate
        }
    }

    throw "Nao encontrei TreeOfSavior nas bibliotecas Steam. Use -SteamTosPath com o caminho da pasta TreeOfSavior."
}

function Write-ClientConfig {
    param([string]$ReleasePath)

    $baseUrl = "http://${HostName}:${WebPort}/toslive/patch/"
    $newAccountUrl = "http://${HostName}:${WebPort}/register/index.html"

    $clientXml = @"
<?xml version="1.0" encoding="UTF-8"?>
<client>
<General Width="1280" Height="720" WindowMode="1" UseSteamClient="NO" />
<Display Shadow="3" AntiAliasing="0" VSync="0" FullScreenBloom="0" SSAO="0" />
<GameOption ServerListURL="${baseUrl}serverlist.xml" StaticConfigURL="$baseUrl" NewAccountURL="$newAccountUrl" PaymentURL="$baseUrl" LoadingImgURL="$baseUrl" LoadingImgCount="10"/>
<Locale ServiceNation="GLOBAL" Dictionary="YES" DefaultLanguage="English" />
<Security CheatCheck="NO" GameGuard="NO" XignCode="NO" />
</client>
"@

    $serverList = @"
<?xml version="1.0" encoding="UTF-8"?>
<serverlist>
    <server GROUP_ID="$GroupId" TRAFFIC="0" ENTER_LIMIT="100" NAME="$ServerName" Server0_IP="$HostName" Server0_Port="$BarracksPort"/>
</serverlist>
"@

    $launcher = @"
@echo off
cd /d "%~dp0"
start "$ServerName" "%~dp0Client_tos_x64.exe" -SERVICE
"@

    Set-Content -LiteralPath (Join-Path $ReleasePath "client.xml") -Value $clientXml -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $ReleasePath "serverlist_recent.xml") -Value $serverList -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $ReleasePath "Start-CloverTOS-Local.bat") -Value $launcher -Encoding ASCII
}

function Disable-Reshade {
    param([string]$ReleasePath)

    $reshadeDlls = @("dxgi.dll", "d3d9.dll", "d3d10.dll", "d3d11.dll", "opengl32.dll")
    foreach ($name in $reshadeDlls) {
        $path = Join-Path $ReleasePath $name
        if (Test-Path -LiteralPath $path) {
            Move-Item -LiteralPath $path -Destination "$path.disabled" -Force
        }
    }
}

function Reset-ClientState {
    param([string]$ReleasePath)

    $transientFiles = @(
        "user.xml",
        "user_c.xml",
        "chatmacro.xml",
        "system.cfg",
        "serverlist_recent.xml"
    )

    foreach ($name in $transientFiles) {
        $path = Join-Path $ReleasePath $name
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force
        }
    }
}

function Test-ClientServerList {
    param([string]$ReleasePath)

    $clientXmlPath = Join-Path $ReleasePath "client.xml"
    [xml]$clientXml = Get-Content -LiteralPath $clientXmlPath -Raw
    $serverListUrl = $clientXml.client.GameOption.ServerListURL

    if (-not $serverListUrl) {
        throw "client.xml nao contem ServerListURL."
    }

    Write-Step "Validando ServerListURL"
    $response = Invoke-WebRequest -UseBasicParsing -TimeoutSec 8 -Uri $serverListUrl
    if ($response.Content -notmatch 'Server0_IP="127\.0\.0\.1"') {
        throw "ServerListURL respondeu, mas nao aponta para 127.0.0.1: $serverListUrl"
    }

    Write-Ok "ServerListURL acessivel: $serverListUrl"
}

Write-Step "Localizando Tree of Savior instalado pela Steam"
$source = Find-TreeOfSaviorPath -ExplicitPath $SteamTosPath
Write-Ok "Origem: $source"
Test-SourceClientVersion -SourcePath $source

if (-not $SkipPrerequisites) {
    Write-Step "Verificando pre-requisitos do Windows"
    Install-Prerequisites
}
else {
    Write-Host "Pulando pre-requisitos por causa de -SkipPrerequisites." -ForegroundColor Yellow
}

Write-Step "Preparando pasta de destino"
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
Write-Ok "Destino: $Destination"

Write-Step "Copiando arquivos do cliente"
$robocopyArgs = @(
    $source,
    $Destination,
    "/MIR",
    "/R:2",
    "/W:2",
    "/MT:16",
    "/XD",
    "screenshot",
    "tempfiles",
    "log_Client",
    "DisconnectLog",
    "/XF",
    "ReShade.log"
)

& robocopy @robocopyArgs
$copyExitCode = $LASTEXITCODE
if ($copyExitCode -gt 7) {
    throw "Robocopy falhou com codigo $copyExitCode."
}
Write-Ok "Copia concluida"

$releasePath = Join-Path $Destination "release"
if (-not (Test-Path -LiteralPath (Join-Path $releasePath "Client_tos_x64.exe"))) {
    throw "Client_tos_x64.exe nao foi encontrado em $releasePath."
}

Write-Step "Aplicando configuracao do servidor $ServerName"
Write-ClientConfig -ReleasePath $releasePath
Disable-Reshade -ReleasePath $releasePath
Reset-ClientState -ReleasePath $releasePath
Write-ClientConfig -ReleasePath $releasePath
Test-ClientServerList -ReleasePath $releasePath
Write-Ok "Configuracao aplicada"

Write-Step "Finalizado"
Write-Host "Abra o jogo por:"
Write-Host "  $releasePath\Start-CloverTOS-Local.bat" -ForegroundColor Yellow
