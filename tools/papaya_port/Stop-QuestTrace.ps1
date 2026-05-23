param(
	[Parameter(Mandatory = $true)]
	[string]$TraceDir
)

$ErrorActionPreference = "Stop"

function Assert-Admin {
	$current = [Security.Principal.WindowsIdentity]::GetCurrent()
	$principal = New-Object Security.Principal.WindowsPrincipal($current)
	if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
		throw "Run this script from an elevated PowerShell session. PktMon requires Administrator."
	}
}

function Copy-IfExists([string]$Path, [string]$Destination) {
	if (Test-Path -LiteralPath $Path) {
		New-Item -ItemType Directory -Force -Path $Destination | Out-Null
		Copy-Item -LiteralPath $Path -Destination $Destination -Recurse -Force -ErrorAction SilentlyContinue
	}
}

Assert-Admin

if (-not (Test-Path -LiteralPath $TraceDir)) {
	throw "TraceDir does not exist: $TraceDir"
}

$metadata = Join-Path $TraceDir "metadata.txt"
$processes = Join-Path $TraceDir "processes-stop.txt"
$pcap = Join-Path $TraceDir "capture.pcapng"
$pktText = Join-Path $TraceDir "capture.txt"

pktmon stop

$etl = Get-ChildItem -LiteralPath $TraceDir -Filter "*.etl" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($etl -ne $null) {
	pktmon etl2pcap "$($etl.FullName)" --out "$pcap" | Out-Null
	pktmon etl2txt "$($etl.FullName)" --out "$pktText" | Out-Null
}

Get-Process |
	Where-Object { $_.ProcessName -like "*tos*" -or $_.ProcessName -like "*papaya*" -or $_.ProcessName -like "*steam*" } |
	Select-Object Id, ProcessName, Path, StartTime |
	Sort-Object ProcessName, Id |
	Format-List | Out-File -Encoding utf8 $processes

$clientRoots = @()
Get-Process |
	Where-Object { $_.ProcessName -like "*tos*" -or $_.ProcessName -like "*papaya*" } |
	ForEach-Object {
		if ($_.Path) {
			$clientRoots += Split-Path -Parent $_.Path
			$clientRoots += Split-Path -Parent (Split-Path -Parent $_.Path)
		}
	}

$clientRoots += "C:\CloverTOS-Local\release"
$clientRoots = $clientRoots | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Sort-Object -Unique

$logsDir = Join-Path $TraceDir "client-logs"
foreach ($root in $clientRoots) {
	Copy-IfExists (Join-Path $root "log_Client") $logsDir
	Copy-IfExists (Join-Path $root "DisconnectLog") $logsDir
	Copy-IfExists (Join-Path $root "dump") $logsDir
}

@(
	"stopped_at=$((Get-Date).ToString('o'))"
	"pcap=$pcap"
	"pkt_text=$pktText"
	"client_roots=$($clientRoots -join ';')"
) | Out-File -Encoding utf8 -Append $metadata

Write-Host "Quest trace stopped."
Write-Host "Trace directory: $TraceDir"
Write-Host "PCAP: $pcap"
