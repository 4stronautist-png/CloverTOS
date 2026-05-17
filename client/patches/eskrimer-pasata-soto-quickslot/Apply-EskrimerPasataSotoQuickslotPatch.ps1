param(
	[string]$ClientRoot = "C:\CloverTOS-Local",
	[string]$OutputFile = "9000001_001001.ipf"
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
target_path = "quickslotnexpbar/quickslotnexpbar.lua"

spec = importlib.util.spec_from_file_location("ipf_tool", ipf_tool)
ipf = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ipf)
reader = ipf.load_ipf_reader()

candidates = []
for root_name in ("data", "patch"):
	root = client_root / root_name
	if not root.exists():
		continue
	for path in root.glob("*.ipf"):
		if path.resolve() == output_path.resolve():
			continue
		try:
			version = ipf.ipf_contains(path, target_path)
		except Exception:
			continue
		if version is not None:
			candidates.append((version, path.name, path))

if not candidates:
	raise SystemExit(f"Could not find {target_path} in client IPFs")

candidates.sort()
base_ipf = None
content = None
for _, _, candidate in reversed(candidates):
	candidate_content = reader.extract_ipf_file(candidate, target_path).decode("utf-8").replace("\r\n", "\n").replace("\r", "\n")
	if "CLOVER_ESKRIMER_PASATA_SOTO_SKILL_ID" not in candidate_content:
		base_ipf = candidate
		content = candidate_content
		break

if base_ipf is None:
	_, _, base_ipf = candidates[-1]
	content = reader.extract_ipf_file(base_ipf, target_path).decode("utf-8").replace("\r\n", "\n").replace("\r", "\n")

helper = r'''

CLOVER_ESKRIMER_PASATA_SOTO_SKILL_ID = 12359;

function CLOVER_ESKRIMER_HAS_TOUCHER_READY()
	local handle = session.GetMyHandle();
	if handle == nil then
		return false;
	end

	if info.GetBuff(handle, 7534) ~= nil then
		return true;
	end

	if info.GetBuff(handle, 3325) ~= nil then
		return true;
	end

	if info.GetBuffByName(handle, "Touche_max_Buff") ~= nil then
		return true;
	end

	return false;
end

function CLOVER_ESKRIMER_GET_ICON_SKILL_TYPE(icon)
	if icon == nil then
		return 0;
	end

	local iconInfo = icon:GetInfo();
	if iconInfo == nil or iconInfo:GetCategory() ~= 'Skill' then
		return 0;
	end

	local skillType = tonumber(iconInfo.type);
	if skillType == nil and iconInfo.GetClassID ~= nil then
		skillType = tonumber(iconInfo:GetClassID());
	end

	if skillType == nil then
		return 0;
	end

	return skillType;
end

function CLOVER_ESKRIMER_UPDATE_PASATA_ICON(icon)
	if CLOVER_ESKRIMER_GET_ICON_SKILL_TYPE(icon) ~= CLOVER_ESKRIMER_PASATA_SOTO_SKILL_ID then
		return false;
	end

	if CLOVER_ESKRIMER_HAS_TOUCHER_READY() == true then
		icon:SetEnableUpdateScp('None');
		icon:SetEnable(1);
		icon:SetColorTone("FFFFFFFF");
	else
		icon:SetEnableUpdateScp('ICON_UPDATE_SKILL_ENABLE');
		icon:SetColorTone("FF555555");
	end

	return true;
end

function CLOVER_ESKRIMER_UPDATE_PASATA_SLOT(slot)
	if slot == nil then
		return;
	end

	local icon = slot:GetIcon();
	if icon == nil then
		return;
	end

	CLOVER_ESKRIMER_UPDATE_PASATA_ICON(icon);
end

function CLOVER_ESKRIMER_TRY_USE_PASATA_ICON(icon)
	if CLOVER_ESKRIMER_GET_ICON_SKILL_TYPE(icon) ~= CLOVER_ESKRIMER_PASATA_SOTO_SKILL_ID then
		return false;
	end

	if CLOVER_ESKRIMER_HAS_TOUCHER_READY() ~= true then
		return false;
	end

	icon:SetEnableUpdateScp('None');
	icon:SetEnable(1);
	icon:SetColorTone("FFFFFFFF");
	control.CustomCommand("QUICKSLOT_CHANGE_SKILL_UPDATE", CLOVER_ESKRIMER_PASATA_SOTO_SKILL_ID, 0, 0);
	return true;
end
'''

if "CLOVER_ESKRIMER_PASATA_SOTO_SKILL_ID" not in content:
	content = content.replace('QUICKSLOT_OVERHEAT_GAUGE = "overheat_gauge";\n', 'QUICKSLOT_OVERHEAT_GAUGE = "overheat_gauge";\n' + helper + "\n", 1)

timer_old = "\t\tUPDATE_SLOT_OVERHEAT(slot);\n\tend\nend"
timer_new = "\t\tUPDATE_SLOT_OVERHEAT(slot);\n\t\tCLOVER_ESKRIMER_UPDATE_PASATA_SLOT(slot);\n\tend\nend"
if timer_old not in content:
	raise SystemExit("Could not find quickslot timer update block")
content = content.replace(timer_old, timer_new, 1)

slot_use_old = """\tlocal iconInfo = icon:GetInfo();\n\tlocal joystickquickslotRestFrame = ui.GetFrame(\"joystickrestquickslot\");"""
slot_use_new = """\tlocal iconInfo = icon:GetInfo();\n\tif iconInfo:GetCategory() == 'Skill' and CLOVER_ESKRIMER_TRY_USE_PASATA_ICON(icon) == true then\n\t\treturn;\n\tend\n\n\tlocal joystickquickslotRestFrame = ui.GetFrame(\"joystickrestquickslot\");"""
if slot_use_old not in content:
	raise SystemExit("Could not find quickslot slot-use iconInfo block")
content = content.replace(slot_use_old, slot_use_new, 1)

ipf.create_ipf(output_path, [("addon.ipf", target_path, content.encode("utf-8"))], new_version=9000001)
print(f"Wrote {output_path} using base {base_ipf.name}")
'@

$tempScript = Join-Path $env:TEMP "clover_eskrimer_pasata_quickslot_patch.py"
Set-Content -LiteralPath $tempScript -Value $script -Encoding UTF8
python $tempScript $clientRootPath $outputPath $ipfTool
