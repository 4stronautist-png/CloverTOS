param(
    [string]$Distro = "Ubuntu-20.04",
    [string]$ListenAddress = "0.0.0.0",
    [int[]]$Ports = @(2000, 7001, 7002, 8080, 9001, 9002),
    [int]$ExternalWebPort = 18080
)

$ErrorActionPreference = "Stop"

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Admin)) {
    throw "Abra o PowerShell como administrador e rode este script novamente."
}

Write-Host "Detectando IP do WSL ($Distro)..." -ForegroundColor Cyan
$wslIp = (& wsl.exe -d $Distro -- bash -lc "ip -4 addr show eth0 | grep -oP 'inet \K[0-9.]+' | head -n 1").Trim()

if (-not $wslIp) {
    throw "Nao consegui detectar o IP do WSL."
}

Write-Host "WSL IP: $wslIp" -ForegroundColor Green

Write-Host "Configurando portproxy TCP..." -ForegroundColor Cyan
foreach ($port in $Ports) {
    & netsh interface portproxy delete v4tov4 listenaddress=$ListenAddress listenport=$port | Out-Null
    & netsh interface portproxy add v4tov4 listenaddress=$ListenAddress listenport=$port connectaddress=$wslIp connectport=$port | Out-Null
    Write-Host "  ${ListenAddress}:${port} -> ${wslIp}:${port}"
}

& netsh interface portproxy delete v4tov4 listenaddress=$ListenAddress listenport=$ExternalWebPort | Out-Null
& netsh interface portproxy add v4tov4 listenaddress=$ListenAddress listenport=$ExternalWebPort connectaddress=$wslIp connectport=8080 | Out-Null
Write-Host "  ${ListenAddress}:${ExternalWebPort} -> ${wslIp}:8080"

Write-Host "Configurando Windows Firewall..." -ForegroundColor Cyan
$ruleName = "CloverTOS Melia TCP"
& netsh advfirewall firewall delete rule name="$ruleName" | Out-Null
foreach ($port in $Ports) {
    & netsh advfirewall firewall add rule name="$ruleName" dir=in action=allow protocol=TCP localport=$port | Out-Null
}
& netsh advfirewall firewall add rule name="$ruleName" dir=in action=allow protocol=TCP localport=$ExternalWebPort | Out-Null

Write-Host ""
Write-Host "Portproxy ativo:" -ForegroundColor Green
& netsh interface portproxy show v4tov4
