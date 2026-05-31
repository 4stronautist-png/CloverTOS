$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\..\.."))
$ipfTool = Join-Path $repoRoot "server\app\tools\create-ipf-animation-override.py"
$clientRoot = "C:\CloverTOS-Local"
$bgmDir = Join-Path $clientRoot "release\bgm"
$patchDir = Join-Path $clientRoot "patch"

$sourceDir = Get-ChildItem -Path "C:\Users\gusta\OneDrive" -Directory -Recurse -ErrorAction SilentlyContinue |
	Where-Object { $_.FullName -like "*Giltine Sin*_M*sicas" } |
	Select-Object -First 1 -ExpandProperty FullName

if ([string]::IsNullOrWhiteSpace($sourceDir)) {
	throw "Missing Giltine Sin music folder under C:\Users\gusta\OneDrive"
}

$day = Get-ChildItem -LiteralPath $sourceDir -File | Where-Object { $_.BaseName -eq "klaipeda_day" } | Select-Object -First 1
$night = Get-ChildItem -LiteralPath $sourceDir -File | Where-Object { $_.BaseName -eq "klaipeda_night" } | Select-Object -First 1
$tavern1 = Get-ChildItem -LiteralPath $sourceDir -File | Where-Object { $_.BaseName -eq "klaipeda_tavern1" } | Select-Object -First 1
$tavern2 = Get-ChildItem -LiteralPath $sourceDir -File | Where-Object { $_.BaseName -eq "klaipeda_tavern2" } | Select-Object -First 1
$login = Get-ChildItem -LiteralPath $bgmDir -File |
	Where-Object { $_.BaseName -eq "tos_SFA_Deives_Veliava_feat_Romanas(DLC_special_edition)" -or $_.BaseName -eq "SFA_Deives_Veliava_feat_Romanas(DLC_special_edition)" } |
	Select-Object -First 1

if ($null -eq $day) { throw "Missing klaipeda_day music file in $sourceDir" }
if ($null -eq $night) { throw "Missing klaipeda_night music file in $sourceDir" }
if ($null -eq $tavern1) { throw "Missing klaipeda_tavern1 music file in $sourceDir" }
if ($null -eq $tavern2) { throw "Missing klaipeda_tavern2 music file in $sourceDir" }
if ($null -eq $login) { throw "Missing SFA_Deives_Veliava_feat_Romanas(DLC_special_edition) in $bgmDir" }
if (-not (Test-Path -LiteralPath $ipfTool)) { throw "IPF writer not found: $ipfTool" }
if (-not (Test-Path -LiteralPath $bgmDir)) { throw "Client BGM folder not found: $bgmDir" }

Copy-Item -LiteralPath $day.FullName -Destination (Join-Path $bgmDir "klaipeda_day.mp3") -Force
Copy-Item -LiteralPath $night.FullName -Destination (Join-Path $bgmDir "klaipeda_night.mp3") -Force
Copy-Item -LiteralPath $day.FullName -Destination (Join-Path $bgmDir "SFA_April_Town.mp3") -Force
Copy-Item -LiteralPath $night.FullName -Destination (Join-Path $bgmDir "SFA_Night_Paradise.mp3") -Force
Copy-Item -LiteralPath $tavern1.FullName -Destination (Join-Path $bgmDir "klaipeda_tavern1.mp3") -Force
Copy-Item -LiteralPath $tavern2.FullName -Destination (Join-Path $bgmDir "klaipeda_tavern2.mp3") -Force
Copy-Item -LiteralPath $tavern1.FullName -Destination (Join-Path $bgmDir "Intium_Arquebuiser_Lyudmila(short).mp3") -Force
Copy-Item -LiteralPath $tavern1.FullName -Destination (Join-Path $bgmDir "tos_Initium_Arquebusier_Lyudmila(short).mp3") -Force
Copy-Item -LiteralPath $tavern2.FullName -Destination (Join-Path $bgmDir "SoundTeMP_Village_school.mp3") -Force
Copy-Item -LiteralPath $tavern2.FullName -Destination (Join-Path $bgmDir "tos_SoundTeMP_Village_School.mp3") -Force
Copy-Item -LiteralPath $tavern2.FullName -Destination (Join-Path $bgmDir "SFA_Klaipeda_Tavern_2.mp3") -Force
Copy-Item -LiteralPath $tavern2.FullName -Destination (Join-Path $bgmDir "c_request_1.mp3") -Force
Copy-Item -LiteralPath $tavern2.FullName -Destination (Join-Path $bgmDir "tos_c_request_1.mp3") -Force

$klaipedaNativeDayFallbacks = @(
	"SFA_Whisper_of_Moment.mp3",
	"SFA_April_Town.mp3",
	"SFA_Triste.mp3",
	"SFA_Openup_Po10.mp3",
	"SFA_Open_Up_Po10_inst.mp3",
	"SFA_Day_in_Glasgow.mp3",
	"tos_SFA_Whisper_of_Moment.mp3",
	"tos_SFA_April_Town.mp3",
	"tos_SFA_Triste.mp3",
	"tos_SFA_Open_Up_Po10_inst.mp3",
	"tos_SFA_Day_in_Glasgow.mp3",
	"tos_SFA_Velvety.mp3"
)

foreach ($fileName in $klaipedaNativeDayFallbacks) {
	Copy-Item -LiteralPath $day.FullName -Destination (Join-Path $bgmDir $fileName) -Force
}

$loginTargets = @(
	"SFA_Deives_Veliava_feat_Romanas(DLC_special_edition).mp3",
	"tos_SFA_Deives_Veliava_feat_Romanas(DLC_special_edition).mp3",
	"Tree_of_Savior.mp3",
	"tos_Tree_of_Savior.mp3",
	"Tree_of_Savior_Piano.mp3",
	"tos_Tree_of_Savior_Piano.mp3",
	"Orgel_Tree_of_Savior.mp3",
	"tos_Kevin_TOS_Carol_2017.mp3",
	"tos_SFA_Openup_Po10.mp3"
)

foreach ($fileName in $loginTargets) {
	$target = Join-Path $bgmDir $fileName
	if ([System.IO.Path]::GetFullPath($login.FullName) -ne [System.IO.Path]::GetFullPath($target)) {
		Copy-Item -LiteralPath $login.FullName -Destination $target -Force
	}
}

$webPatchDir = Join-Path $repoRoot "server\app\user\web\toslive\patch"
$localPatch = Join-Path $patchDir "9999999_001001.ipf"
if (Test-Path -LiteralPath $localPatch) {
	Remove-Item -LiteralPath $localPatch -Force
}
if (Test-Path -LiteralPath (Join-Path $webPatchDir "9999999_001001.ipf")) {
	Remove-Item -LiteralPath (Join-Path $webPatchDir "9999999_001001.ipf") -Force
}

$bgmPlayerPatch = Join-Path $patchDir "9999998_001001.ipf"
if (Test-Path -LiteralPath $bgmPlayerPatch) {
	Remove-Item -LiteralPath $bgmPlayerPatch -Force
}
if (Test-Path -LiteralPath (Join-Path $webPatchDir "9999998_001001.ipf")) {
	Remove-Item -LiteralPath (Join-Path $webPatchDir "9999998_001001.ipf") -Force
}

Get-Item -LiteralPath (Join-Path $bgmDir "klaipeda_day.mp3"), (Join-Path $bgmDir "klaipeda_night.mp3"), (Join-Path $bgmDir "SFA_April_Town.mp3"), (Join-Path $bgmDir "SFA_Night_Paradise.mp3"), (Join-Path $bgmDir "Intium_Arquebuiser_Lyudmila(short).mp3"), (Join-Path $bgmDir "tos_Initium_Arquebusier_Lyudmila(short).mp3"), (Join-Path $bgmDir "SoundTeMP_Village_school.mp3"), (Join-Path $bgmDir "tos_SoundTeMP_Village_School.mp3"), (Join-Path $bgmDir "SFA_Klaipeda_Tavern_2.mp3"), (Join-Path $bgmDir "c_request_1.mp3"), (Join-Path $bgmDir "tos_c_request_1.mp3"), (Join-Path $bgmDir "tos_Tree_of_Savior.mp3"), (Join-Path $bgmDir "tos_SFA_Openup_Po10.mp3") |
	Select-Object FullName, Length, LastWriteTime
