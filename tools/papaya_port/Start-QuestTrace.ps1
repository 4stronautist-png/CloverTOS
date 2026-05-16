param(
	[string]$Name = "papaya-quest-flow",
	[string]$Note = "",
	[string]$OutputRoot = "C:\CloverTOS-Captures\quest-traces"
)

$ErrorActionPreference = "Stop"

function Assert-Admin {
	$current = [Security.Principal.WindowsIdentity]::GetCurrent()
	$principal = New-Object Security.Principal.WindowsPrincipal($current)
	if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
		throw "Run this script from an elevated PowerShell session. PktMon requires Administrator."
	}
}

function Safe-Name([string]$value) {
	if ([string]::IsNullOrWhiteSpace($value)) { return "quest-trace" }
	return ($value -replace '[^A-Za-z0-9_.-]', '-')
}

Assert-Admin

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$safeName = Safe-Name $Name
$traceDir = Join-Path $OutputRoot "$stamp-$safeName"
New-Item -ItemType Directory -Force -Path $traceDir | Out-Null

$etl = Join-Path $traceDir "$safeName.etl"
$metadata = Join-Path $traceDir "metadata.txt"
$processes = Join-Path $traceDir "processes-start.txt"

Get-Process |
	Where-Object { $_.ProcessName -like "*tos*" -or $_.ProcessName -like "*papaya*" -or $_.ProcessName -like "*steam*" } |
	Select-Object Id, ProcessName, Path, StartTime |
	Sort-Object ProcessName, Id |
	Format-List | Out-File -Encoding utf8 $processes

@(
	"name=$safeName"
	"note=$Note"
	"trace_dir=$traceDir"
	"started_at=$((Get-Date).ToString('o'))"
	"computer=$env:COMPUTERNAME"
	"user=$env:USERNAME"
	"etl=$etl"
) | Out-File -Encoding utf8 $metadata

cmd.exe /c "pktmon stop >nul 2>nul"
pktmon filter remove | Out-Null
pktmon start --capture --comp nics --pkt-size 0 --file-name "$etl" --file-size 2048

Write-Host "Quest trace started."
Write-Host "Trace directory: $traceDir"
Write-Host "Now play the Papaya flow. When done, run:"
Write-Host "  powershell -ExecutionPolicy Bypass -File `"$PSScriptRoot\Stop-QuestTrace.ps1`" -TraceDir `"$traceDir`""
