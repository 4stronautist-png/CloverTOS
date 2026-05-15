param(
    [string] $OutputPatch = "1000005_001001.ipf"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..\..\..")).ProviderPath
$toolPath = Join-Path $repoRoot "server\app\tools\create-ipf-animation-override.py"
$sourceLua = Join-Path $scriptDir "skillability\skillability.lua"
$clientPatchDir = "C:\CloverTOS-Local\patch"
$outputPath = Join-Path $clientPatchDir $OutputPatch

if (!(Test-Path -LiteralPath $toolPath)) {
    throw "IPF writer not found: $toolPath"
}

if (!(Test-Path -LiteralPath $sourceLua)) {
    throw "skillability.lua not found: $sourceLua"
}

if (!(Test-Path -LiteralPath $clientPatchDir)) {
    throw "Client patch folder not found: $clientPatchDir"
}

$env:TOOL_PATH = $toolPath
$env:SOURCE_LUA = $sourceLua
$env:OUTPUT_PATH = $outputPath

@'
import importlib.util
import os
from pathlib import Path

tool_path = Path(os.environ["TOOL_PATH"])
source_lua = Path(os.environ["SOURCE_LUA"])
output_path = Path(os.environ["OUTPUT_PATH"])

spec = importlib.util.spec_from_file_location("ipf_writer", tool_path)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

module.create_ipf(
    output_path,
    [("addon.ipf", "skillability/skillability.lua", source_lua.read_bytes())],
)

print(f"Pied Piper skillability layout patch created: {output_path}")
'@ | python -
