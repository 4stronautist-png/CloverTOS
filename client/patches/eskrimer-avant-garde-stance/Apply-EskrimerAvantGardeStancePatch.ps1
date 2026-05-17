param(
	[string]$ClientRoot = "C:\CloverTOS-Local",
	[string]$OutputFile = "9000002_001001.ipf"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path "$PSScriptRoot\..\..\..").ProviderPath
$clientRootPath = (Resolve-Path $ClientRoot).ProviderPath
$outputPath = Join-Path $clientRootPath "patch\$OutputFile"
$ipfTool = Join-Path $repoRoot "server\app\tools\create-ipf-animation-override.py"

$script = @'
import importlib.util
import sys
from pathlib import Path

client_root = Path(sys.argv[1])
output_path = Path(sys.argv[2])
ipf_tool = Path(sys.argv[3])

spec = importlib.util.spec_from_file_location("ipf_tool", ipf_tool)
ipf = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ipf)
reader = ipf.load_ipf_reader()

source_patch = client_root / "patch" / "402785_001001.ipf"
if not source_patch.is_file():
	raise SystemExit(f"Missing Eskrimer animation patch: {source_patch}")

epee_base_patch = client_root / "patch" / "138241_001001.ipf"
epee_xml_patch = client_root / "patch" / "283186_001001.ipf"
for required_patch in (epee_base_patch, epee_xml_patch):
	if not required_patch.is_file():
		raise SystemExit(f"Missing Epee Garde animation patch: {required_patch}")

def clean_switch_xml(content):
	text = content.decode("utf-8", errors="replace")
	lines = []
	for line in text.splitlines():
		if "F_pose_cheers_light" in line:
			continue
		if "skl_eff_sinobi_kunai_cast_1" in line:
			continue
		lines.append(line)
	return ("\r\n".join(lines) + "\r\n").encode("utf-8")

entries = []
for gender in ("f", "m"):
	prefix = f"pc/warrior_{gender}/warrior_{gender}_rap"
	for extension in ("xml", "xsm", "xsmtime"):
		source = f"{prefix}_skl_epeegarde_switch_escrimeur.{extension}"
		content = reader.extract_ipf_file(source_patch, source)
		if extension == "xml":
			content = clean_switch_xml(content)
		entries.append(("animation.ipf", source, content))

stance_aliases = (
	"avantgarde",
	"advantgarde",
	"avantgarde_escrimeur",
	"advantgarde_buff",
	"epeegarde_escrimeur",
)

for gender in ("f", "m"):
	prefix = f"pc/warrior_{gender}/warrior_{gender}_rap"
	for anim in ("astd", "atk"):
		for extension in ("xml", "xsm", "xsmtime"):
			source = f"{prefix}_skl_epeegarde_{anim}.{extension}"
			source_file = epee_xml_patch if extension == "xml" else epee_base_patch
			try:
				content = reader.extract_ipf_file(source_file, source)
			except ValueError:
				continue

			for alias in stance_aliases:
				target = f"{prefix}_skl_{alias}_{anim}.{extension}"
				entries.append(("animation.ipf", target, content))

new_version = int(output_path.name.split("_", 1)[0])
ipf.create_ipf(output_path, entries, new_version=new_version)
print(f"Wrote {output_path} with cleaned Avant Garde switch effects and Epee Garde stance aliases")
'@

$tempScript = Join-Path $env:TEMP "clover_eskrimer_avant_garde_effect_cleanup.py"
Set-Content -LiteralPath $tempScript -Value $script -Encoding UTF8
python $tempScript $clientRootPath $outputPath $ipfTool
