param(
    [string]$ServerIp = "",
    [string]$OutputDir = "C:\CloverTOS-Captures",
    [string]$Name = "",
    [int]$WaitSeconds = 300
)

$ErrorActionPreference = "Stop"

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-ClientRemoteAddresses {
    $processes = Get-Process Client_tos_x64, Client_tos -ErrorAction SilentlyContinue
    if (-not $processes) {
        return @()
    }

    $processIds = $processes.Id
    Get-NetTCPConnection -State Established -ErrorAction SilentlyContinue |
        Where-Object { $processIds -contains $_.OwningProcess } |
        Where-Object { $_.RemoteAddress -and $_.RemoteAddress -notin @("127.0.0.1", "::1", "0.0.0.0") } |
        Select-Object -ExpandProperty RemoteAddress -Unique
}

if (-not (Test-Admin)) {
    $script = $PSCommandPath
    $argsList = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$script`"")
    if ($ServerIp) { $argsList += @("-ServerIp", $ServerIp) }
    $argsList += @("-OutputDir", "`"$OutputDir`"", "-WaitSeconds", $WaitSeconds)
    if ($Name) { $argsList += @("-Name", $Name) }

    Start-Process powershell.exe -Verb RunAs -ArgumentList $argsList
    Write-Host "Solicitei permissao de administrador para iniciar a captura." -ForegroundColor Yellow
    exit 0
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

if (-not $Name) {
    $Name = "titas-scout-" + (Get-Date -Format "yyyyMMdd-HHmmss")
}

$etlPath = Join-Path $OutputDir "$Name.etl"
$metaPath = Join-Path $OutputDir "$Name.metadata.txt"

if (-not $ServerIp) {
    Write-Host "Aguardando Client_tos_x64.exe conectar ao servidor de teste..." -ForegroundColor Cyan
    $deadline = (Get-Date).AddSeconds($WaitSeconds)
    while ((Get-Date) -lt $deadline) {
        $addresses = @(Get-ClientRemoteAddresses)
        if ($addresses.Count -gt 0) {
            $ServerIp = $addresses[0]
            break
        }
        Start-Sleep -Seconds 2
    }
}

if (-not $ServerIp) {
    throw "Nao encontrei conexao ativa do Client_tos_x64.exe. Abra o client e conecte no test server, ou informe -ServerIp."
}

Write-Host "Iniciando captura filtrada para $ServerIp" -ForegroundColor Green

& pktmon stop 2>$null | Out-Null
& pktmon filter remove | Out-Null
& pktmon filter add CloverTOS-TestServer -i $ServerIp -t TCP | Out-Null
& pktmon start --capture --comp nics --pkt-size 0 --file-name $etlPath | Out-Null

@(
    "name=$Name"
    "server_ip=$ServerIp"
    "etl=$etlPath"
    "started_at=$(Get-Date -Format o)"
    "note=Depois de concluir lvl1 -> Titas -> Scout, rode Stop-TestServerCapture.ps1."
) | Set-Content -LiteralPath $metaPath -Encoding ASCII

Write-Host ""
Write-Host "Captura rodando." -ForegroundColor Green
Write-Host "Arquivo ETL: $etlPath"
Write-Host "Quando terminar o fluxo, rode:"
Write-Host "  powershell -ExecutionPolicy Bypass -File .\tools\capture\Stop-TestServerCapture.ps1 -Name $Name" -ForegroundColor Yellow
