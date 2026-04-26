$ErrorActionPreference = "Stop"

$launcher = "C:\CloverTOS-Local\release\Start-CloverTOS-Local.bat"

if (-not (Test-Path -LiteralPath $launcher)) {
    throw "Launcher nao encontrado em $launcher. Rode primeiro .\client\tools\Install-CloverTOS-Local.ps1"
}

Start-Process -FilePath $launcher
