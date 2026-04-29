param(
    [string]$OutputDir = "C:\CloverTOS-Captures",
    [string]$Name = ""
)

$ErrorActionPreference = "Stop"

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Admin)) {
    $script = $PSCommandPath
    $argsList = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$script`"", "-OutputDir", "`"$OutputDir`"")
    if ($Name) { $argsList += @("-Name", $Name) }

    Start-Process powershell.exe -Verb RunAs -ArgumentList $argsList
    Write-Host "Solicitei permissao de administrador para parar a captura." -ForegroundColor Yellow
    exit 0
}

if (-not $Name) {
    $latest = Get-ChildItem -LiteralPath $OutputDir -Filter "*.etl" -ErrorAction Stop |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $latest) {
        throw "Nenhum .etl encontrado em $OutputDir."
    }

    $Name = [IO.Path]::GetFileNameWithoutExtension($latest.Name)
}

$etlPath = Join-Path $OutputDir "$Name.etl"
$pcapPath = Join-Path $OutputDir "$Name.pcapng"
$metaPath = Join-Path $OutputDir "$Name.metadata.txt"

Write-Host "Parando captura..." -ForegroundColor Cyan
& pktmon stop | Out-Null

if (-not (Test-Path -LiteralPath $etlPath)) {
    throw "Arquivo ETL nao encontrado: $etlPath"
}

Write-Host "Convertendo para pcapng..." -ForegroundColor Cyan
& pktmon etl2pcap $etlPath --out $pcapPath | Out-Null
& pktmon filter remove | Out-Null

"stopped_at=$(Get-Date -Format o)" | Add-Content -LiteralPath $metaPath -Encoding ASCII
"pcapng=$pcapPath" | Add-Content -LiteralPath $metaPath -Encoding ASCII

Write-Host ""
Write-Host "Captura pronta:" -ForegroundColor Green
Write-Host "  $pcapPath"
Write-Host "Metadata:"
Write-Host "  $metaPath"
