param(
    [string]$Destination = "C:\CloverTOS-Local"
)

$ErrorActionPreference = "Stop"

$launcher = Join-Path $Destination "release\Start-CloverTOS-Local.bat"

if (-not (Test-Path -LiteralPath $launcher)) {
    throw "Launcher nao encontrado em $launcher. Rode primeiro .\client\tools\Install-CloverTOS-Local.ps1"
}

Start-Process -FilePath $launcher
