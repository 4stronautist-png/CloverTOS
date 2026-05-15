param(
    [string]$ClientRoot = "C:\CloverTOS-Local",
    [string]$OutputPatch = "1000004_001001.ipf"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).ProviderPath
$toolPath = Join-Path $repoRoot "server\app\tools\create-ipf-animation-override.py"
$sourceLua = Join-Path $PSScriptRoot "status\status.lua"
$outputPath = Join-Path (Join-Path $ClientRoot "patch") $OutputPatch

if (-not (Test-Path -LiteralPath $sourceLua)) {
    throw "Missing patched status.lua: $sourceLua"
}

$python = @"
import importlib.util
from pathlib import Path

tool_path = Path(r"$toolPath")
source_lua = Path(r"$sourceLua")
output_path = Path(r"$outputPath")

spec = importlib.util.spec_from_file_location("ipf_writer", tool_path)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

module.create_ipf(
    output_path,
    [("addon.ipf", "status/status.lua", source_lua.read_bytes())],
    old_version=1,
    new_version=1000004,
)
print(f"Wrote {output_path}")
"@

$python | python -
