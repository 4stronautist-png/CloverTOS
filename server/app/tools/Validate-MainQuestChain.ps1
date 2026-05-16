param(
    [string]$AppRoot = (Resolve-Path "$PSScriptRoot\..").Path,
    [switch]$ShowWarnings
)

$ErrorActionPreference = "Stop"

function Get-Field {
    param(
        [string]$Text,
        [string]$Name
    )

    $match = [regex]::Match($Text, "$Name\s*:\s*`"([^`"]*)`"")
    if ($match.Success) { return $match.Groups[1].Value }
    return ""
}

function Get-IntField {
    param(
        [string]$Text,
        [string]$Name
    )

    $match = [regex]::Match($Text, "$Name\s*:\s*([0-9]+)")
    if ($match.Success) { return [int]$match.Groups[1].Value }
    return 0
}

function Get-ListField {
    param(
        [string]$Text,
        [string]$Name
    )

    $match = [regex]::Match($Text, "$Name\s*:\s*\[(.*?)\]")
    if (-not $match.Success) { return @() }

    return [regex]::Matches($match.Groups[1].Value, "`"([^`"]+)`"") |
        ForEach-Object { $_.Groups[1].Value }
}

function Split-QuestTargets {
    param([string]$Targets)

    if ([string]::IsNullOrWhiteSpace($Targets) -or $Targets -eq "ALL") {
        return @()
    }

    return $Targets.Split("/", [System.StringSplitOptions]::RemoveEmptyEntries) |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -and $_ -ne "ALL" }
}

$questPath = Join-Path $AppRoot "system\db\quests.txt"
$questAutoPath = Join-Path $AppRoot "system\db\quest_auto.txt"
$monsterPath = Join-Path $AppRoot "system\db\monsters.txt"
$itemPath = Join-Path $AppRoot "system\db\items.txt"
$mapPath = Join-Path $AppRoot "system\db\maps.txt"
$privateEncounterPath = Join-Path $AppRoot "system\db\private_encounters.txt"
$questComponentPath = Join-Path $AppRoot "src\ZoneServer\World\Actors\Characters\Components\QuestComponent.cs"
$npcFunctionsPath = Join-Path $AppRoot "src\ZoneServer\Scripting\Shared\NPCFunctions.cs"
$westSiauliaiWarpPath = Join-Path $AppRoot "packages\laima\scripts\zone\content\laima\warps\fields\f_siauliai_west.cs"
$scoutTrackPath = Join-Path $AppRoot "packages\laima\scripts\zone\content\laima\tracks\fields\siaul_west_drasius1_track.cs"
$shortcutsPath = Join-Path $AppRoot "src\ZoneServer\Scripting\Shortcuts.cs"
$layeredKillPath = Join-Path $AppRoot "src\ZoneServer\World\Quests\Objectives\LayeredKillObjective.cs"

$quests = @(foreach ($line in Get-Content -LiteralPath $questPath) {
    if ($line -notmatch "^\s*\{") { continue }

    [pscustomobject]@{
        Id = Get-IntField $line "id"
        ClassName = Get-Field $line "className"
        Name = Get-Field $line "name"
        Mode = Get-Field $line "questMode"
        StartMode = Get-Field $line "questStartMode"
        EndMode = Get-Field $line "questEndMode"
        StartMap = Get-Field $line "startMap"
        ProgressMap = Get-Field $line "progressMap"
        EndMap = Get-Field $line "endMap"
        StartLocation = Get-Field $line "startLocation"
        ProgressLocation = Get-Field $line "progressLocation"
        EndLocation = Get-Field $line "endLocation"
        StartZone = Get-Field $line "questStartZone"
        StartNpc = Get-Field $line "startNPC"
        ProgressNpc = Get-Field $line "progressNPC"
        EndNpc = Get-Field $line "endNPC"
        Required = @(Get-ListField $line "requiredQuestName")
        Raw = $line
    }
}) | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_.ClassName)
}

$questByName = @{}
foreach ($quest in $quests) {
    if ($quest.ClassName) {
        $questByName[$quest.ClassName.ToLowerInvariant()] = $quest
    }
}

$maps = @{}
foreach ($match in [regex]::Matches((Get-Content -LiteralPath $mapPath -Raw), 'className:\s*"([^"]+)"')) {
    $maps[$match.Groups[1].Value.ToLowerInvariant()] = $true
}

$monsters = @{}
foreach ($match in [regex]::Matches((Get-Content -LiteralPath $monsterPath -Raw), 'className:\s*"([^"]+)"')) {
    $monsters[$match.Groups[1].Value.ToLowerInvariant()] = $true
}

$items = @{}
foreach ($match in [regex]::Matches((Get-Content -LiteralPath $itemPath -Raw), 'className:\s*"([^"]+)"')) {
    $items[$match.Groups[1].Value.ToLowerInvariant()] = $true
}

$mainQuests = @($quests | Where-Object { $_.Mode -eq "MAIN" } | Sort-Object Id)

function Test-CloverDisabledMainQuestName {
    param([string]$ClassName)

    if ([string]::IsNullOrWhiteSpace($ClassName)) {
        return $false
    }

    if ([string]::Equals($ClassName, "TUTO_SKILL_RUN", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }

    return $ClassName.StartsWith("TUTO_", [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($ClassName, "LEGEND_CARD_LIFT", [System.StringComparison]::OrdinalIgnoreCase) -or
        $ClassName.StartsWith("JOB_", [System.StringComparison]::OrdinalIgnoreCase) -or
        $ClassName.StartsWith("ASSISTOR_TUTO_", [System.StringComparison]::OrdinalIgnoreCase) -or
        $ClassName.StartsWith("TOSHERO_TUTO_", [System.StringComparison]::OrdinalIgnoreCase)
}

$mainPrerequisiteBridgeNames = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
$visitedBridgeNames = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
$bridgeQueue = New-Object System.Collections.Generic.Queue[string]

foreach ($quest in $mainQuests) {
    if (Test-CloverDisabledMainQuestName $quest.ClassName) {
        continue
    }

    foreach ($requiredQuestName in $quest.Required) {
        $bridgeQueue.Enqueue($requiredQuestName)
    }
}

while ($bridgeQueue.Count -gt 0) {
    $requiredQuestName = $bridgeQueue.Dequeue()
    if ([string]::IsNullOrWhiteSpace($requiredQuestName)) {
        continue
    }

    $requiredQuestKey = $requiredQuestName.ToLowerInvariant()
    if (-not $questByName.ContainsKey($requiredQuestKey)) {
        continue
    }

    $requiredQuest = $questByName[$requiredQuestKey]
    if (-not $visitedBridgeNames.Add($requiredQuest.ClassName)) {
        continue
    }

    if (Test-CloverDisabledMainQuestName $requiredQuest.ClassName) {
        continue
    }

    if ($requiredQuest.Name -and $requiredQuest.Name -match "Delete") {
        continue
    }

    if ($requiredQuest.Mode -ne "MAIN") {
        $mainPrerequisiteBridgeNames.Add($requiredQuest.ClassName) | Out-Null
    }

    foreach ($nextRequiredQuestName in $requiredQuest.Required) {
        $bridgeQueue.Enqueue($nextRequiredQuestName)
    }
}

$errors = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

$questAutoEntries = @()
if (Test-Path -LiteralPath $questAutoPath) {
    $parsedQuestAuto = Get-Content -LiteralPath $questAutoPath -Raw | ConvertFrom-Json
    $questAutoEntries = @($parsedQuestAuto | ForEach-Object { $_ })
}

$questAutoByName = @{}
foreach ($entry in $questAutoEntries) {
    if ($entry.questName) {
        $questAutoByName[$entry.questName.ToLowerInvariant()] = $entry
    }
}

$questComponent = ""
if (Test-Path -LiteralPath $questComponentPath) {
    $questComponent = Get-Content -LiteralPath $questComponentPath -Raw
}

$npcFunctionsSource = ""
if (Test-Path -LiteralPath $npcFunctionsPath) {
    $npcFunctionsSource = Get-Content -LiteralPath $npcFunctionsPath -Raw
}

$westSiauliaiWarpSource = ""
if (Test-Path -LiteralPath $westSiauliaiWarpPath) {
    $westSiauliaiWarpSource = Get-Content -LiteralPath $westSiauliaiWarpPath -Raw
}

$scoutTrack = ""
if (Test-Path -LiteralPath $scoutTrackPath) {
    $scoutTrack = Get-Content -LiteralPath $scoutTrackPath -Raw
}

$shortcutsSource = ""
if (Test-Path -LiteralPath $shortcutsPath) {
    $shortcutsSource = Get-Content -LiteralPath $shortcutsPath -Raw
}

$layeredKillSource = ""
if (Test-Path -LiteralPath $layeredKillPath) {
    $layeredKillSource = Get-Content -LiteralPath $layeredKillPath -Raw
}

$privateEncounterSource = ""
if (Test-Path -LiteralPath $privateEncounterPath) {
    $privateEncounterSource = Get-Content -LiteralPath $privateEncounterPath -Raw
}

function Test-StaticLocationLikelyHasPoint {
    param([string]$Location)

    if ([string]::IsNullOrWhiteSpace($Location)) {
        return $false
    }

    return $Location -match '\s-?[0-9]+(\.[0-9]+)?\s+-?[0-9]+(\.[0-9]+)?\s+-?[0-9]+(\.[0-9]+)?\s+[0-9]+'
}

foreach ($quest in $mainQuests) {
	foreach ($required in $quest.Required) {
		if (-not $questByName.ContainsKey($required.ToLowerInvariant())) {
			$errors.Add("Missing prerequisite quest '$required' referenced by $($quest.ClassName) [$($quest.Id)].")
		}
    }

    foreach ($mapName in @($quest.StartMap, $quest.ProgressMap, $quest.EndMap)) {
        if ($mapName -and $mapName -ne "None" -and -not $maps.ContainsKey($mapName.ToLowerInvariant())) {
            $errors.Add("Missing map '$mapName' referenced by $($quest.ClassName) [$($quest.Id)].")
        }
    }

    if ($quest.StartMode -eq "NPCDIALOG" -and [string]::IsNullOrWhiteSpace($quest.StartNpc)) {
        $errors.Add("NPCDIALOG main quest $($quest.ClassName) [$($quest.Id)] has no startNPC.")
    }

    if ($quest.EndMode -eq "NPCDIALOG" -and [string]::IsNullOrWhiteSpace($quest.EndNpc)) {
        $errors.Add("NPCDIALOG main quest $($quest.ClassName) [$($quest.Id)] has no endNPC.")
    }

    foreach ($objective in [regex]::Matches($quest.Raw, '\{ ident:.*?\}')) {
        $objectiveText = $objective.Value
        $type = Get-Field $objectiveText "type"
        $target = Get-Field $objectiveText "target"
        $item = Get-Field $objectiveText "item"
        $dropTarget = Get-Field $objectiveText "dropTarget"

        if ($type -eq "Kill") {
            foreach ($monsterName in Split-QuestTargets $target) {
                if (-not $monsters.ContainsKey($monsterName.ToLowerInvariant())) {
                    $errors.Add("Missing monster '$monsterName' for $($quest.ClassName) [$($quest.Id)].")
                }
            }
        }

        if ($type -eq "Collect") {
            if ($item -and -not $items.ContainsKey($item.ToLowerInvariant()) -and -not ($item -match '^[0-9]+$')) {
                $errors.Add("Missing collect item '$item' for $($quest.ClassName) [$($quest.Id)].")
            }
            foreach ($monsterName in Split-QuestTargets $dropTarget) {
                if (-not $monsters.ContainsKey($monsterName.ToLowerInvariant())) {
                    $errors.Add("Missing drop monster '$monsterName' for $($quest.ClassName) [$($quest.Id)].")
                }
            }
        }
    }

	if ($quest.StartMode -eq "SYSTEM" -and
        $quest.Required.Count -eq 0 -and
        -not (Test-CloverDisabledMainQuestName $quest.ClassName) -and
        [string]::IsNullOrWhiteSpace($quest.StartZone)) {
		$warnings.Add("SYSTEM main quest $($quest.ClassName) [$($quest.Id)] has no prerequisite; it may need a quest_auto or trigger starter.")
	}
}

foreach ($entry in $questAutoEntries) {
	if (-not $entry.questName) {
		continue
	}

	if (-not $questByName.ContainsKey($entry.questName.ToLowerInvariant())) {
		$errors.Add("quest_auto references missing quest '$($entry.questName)'.")
	}

	foreach ($nextQuestName in @($entry.successNextQuestNames)) {
		if (-not $questByName.ContainsKey($nextQuestName.ToLowerInvariant())) {
			$errors.Add("quest_auto '$($entry.questName)' references missing successNextQuest '$nextQuestName'.")
		}
	}
}

$mainQuestAutoTracks = @($mainQuests | Where-Object {
	$questAutoByName.ContainsKey($_.ClassName.ToLowerInvariant()) -and
	-not [string]::IsNullOrWhiteSpace($questAutoByName[$_.ClassName.ToLowerInvariant()].track)
})

$nonMainQuestCount = @($quests | Where-Object { $_.Mode -ne "MAIN" -and -not $mainPrerequisiteBridgeNames.Contains($_.ClassName) }).Count
$repeatQuestCount = @($quests | Where-Object { $_.Mode -eq "REPEAT" }).Count

$locationNpcTriggerCandidates = @($mainQuests | Where-Object {
	$_.StartMode -eq "NPCDIALOG" -and (
		(Test-StaticLocationLikelyHasPoint $_.StartLocation) -or
		(Test-StaticLocationLikelyHasPoint $_.ProgressLocation) -or
		(Test-StaticLocationLikelyHasPoint $_.EndLocation)
	)
})

$objectiveMainQuests = @($mainQuests | Where-Object { $_.Raw -match 'objectives:\s*\[' })

$runtimeChecks = @{
	"runtime MAIN tracker loop exists" = $questComponent -match 'CheckStaticMainQuestRuntimeStateInternal'
	"quest_auto tracks are started generically" = $questComponent -match 'TryStartStaticQuestAutoTracks' -and $questComponent -match 'TryStartStaticQuestAutoTrack'
	"NPC location bridge can start MAIN NPCDIALOG quests" = $questComponent -match 'StaticQuestCanStartFromNpcDialog[\s\S]*StaticQuestNpcDialogMatchesLocation'
	"NPC location bridge can advance MAIN NPCDIALOG quests" = $questComponent -match 'StaticQuestCanAdvanceFromNpcDialog[\s\S]*StaticQuestNpcDialogMatchesLocation'
	"NPC location bridge can complete MAIN NPCDIALOG quests" = $questComponent -match 'StaticQuestShouldCompleteFromNpcDialog[\s\S]*StaticQuestNpcDialogMatchesLocation'
	"NPC dialog advancement is deferred out of active dialogs" = $questComponent -match 'ShouldDeferStaticNpcDialogAdvance' -and $questComponent -match 'StaticNpcDialogCanCrashWhenAdvancedInDialog[\s\S]*return true;'
	"static objective monsters spawn generically" = $questComponent -match 'EnsureStaticQuestObjectiveMonsters' -and $questComponent -match 'DistributeStaticObjectiveMonsterSpawnBudget'
	"static objective monsters are aggressive and AI-driven" = $questComponent -match 'ConfigureStaticQuestObjectiveMonster' -and $questComponent -match 'new\s+MovementComponent' -and $questComponent -match 'new\s+AiComponent' -and $questComponent -match 'SetTarget\(this\.Character\)' -and $questComponent -match 'InsertHate\(this\.Character, 5000\)' -and $questComponent -match 'TendencyType\.Aggressive'
	"existing static objective monsters are re-armed for the player" = $questComponent -match 'GetStaticQuestObjectiveMonsters' -and $questComponent -match 'foreach \(var existingMonster in existingMonsters\)[\s\S]*ConfigureStaticQuestObjectiveMonster\(existingMonster\)'
	"private static objective monsters are explicitly sent to their owner" = $questComponent -match 'SendStaticQuestObjectiveMonsterIfNeeded' -and $questComponent -match 'Clover\.StaticQuestObjective\.EnterSent' -and $questComponent -match 'request\.IsPrivateEncounter[\s\S]*Send\.ZC_ENTER_MONSTER'
	"generic character-spawned enemies target the player" = $shortcutsSource -match 'SetTarget\(character\)[\s\S]*InsertHate\(character, 5000\)' -and $shortcutsSource -match 'LureNearbyEnemies[\s\S]*SetTarget\(character\)'
	"layered kill objective enemies target the player" = $layeredKillSource -match 'SetTarget\(character\)[\s\S]*InsertHate\(character, 5000\)[\s\S]*TendencyType\.Aggressive'
	"slash-separated objective targets share spawn budget" = $questComponent -match 'DistributeStaticObjectiveMonsterSpawnBudget' -and $questComponent -match 'counts\[i % monsters\.Count\]\+\+'
	"quest_auto status handoff is wired for non-objective tracks" = $questComponent -match 'ParseStaticQuestAutoStatus' -and $questComponent -match 'StaticQuestAutoTrackShouldApplyQuestStatus' -and $questComponent -match 'questIdForTrackStatus'
	"quest_auto generic tracks keep objective monsters as real combat actors" = $questComponent -match 'StaticQuestAutoTrackShouldAvoidGenericMonsterActors' -and $questComponent -match 'HasPrivateEncounterObjective\(quest\)'
	"MAIN NPCDIALOG follow-ups auto-start on the current map" = $questComponent -match 'StaticMainFollowUpShouldStartImmediately' -and $questComponent -match 'StartStaticQuest\(nextQuestData'
	"disabled non-MAIN native quest noise is suppressed" = $questComponent -match 'SuppressDisabledStaticQuestNoise[\s\S]*HideUnavailableStaticQuestFromNativeClient\(questData\)'
	"non-MAIN prerequisite bridges remain available for MAIN chains" = $questComponent -match 'StaticQuestIsMainPrerequisiteBridge' -and $questComponent -match 'GetMainPrerequisiteBridgeQuestNames'
	"client quest table carries static class names for duplicate removal" = $questComponent -match 'questTable\.Insert\("ClassName"'
	"client quest restore skips hidden side and daily quests" = $questComponent -match 'UpdateClient\(\)[\s\S]*QuestShouldBeVisibleInClientList'
	"client quest add/update removes hidden quests instead of showing them temporarily" = $questComponent -match 'UpdateClient_AddQuest[\s\S]*HideQuestFromClientList' -and $questComponent -match 'UpdateClient_UpdateQuest[\s\S]*HideQuestFromClientList'
	"disabled static quests cannot emit native Mission Objectives updates" = $questComponent -match 'StaticQuestShouldNotifyNativeQuestState[\s\S]*StaticQuestDisabledForCloverFlow\(questData\)[\s\S]*return false;'
	"Papaya bridge quests stay server-only and auto-complete on success" = $questComponent -match 'StaticQuestIsClientHiddenPapayaBridge' -and $questComponent -match 'CompleteSucceededClientHiddenPapayaBridgeQuests' -and $questComponent -match 'TryAutoCompleteStaticQuestOnSuccess[\s\S]*StaticQuestIsClientHiddenPapayaBridge'
	"unavailable static quest NPCs are hidden until the active chain reaches them" = $questComponent -match 'ShouldSuppressStaticQuestNpcState\(npc\.DialogName,\s*mapClassName\)'
	"static objective fallback waits for active tracks to finish" = $questComponent -match 'Tracks\.ActiveTrack\s*!=\s*null[\s\S]*return;'
	"unresolved private encounter anchors fall back to generic objective spawns" = $questComponent -match 'PrivateEncounterFallback' -and $questComponent -match 'using generic objective fallback' -and $questComponent -match 'CreatePrivateEncounterMonsterSpawnRequests[\s\S]*ToList'
	"captured named anchors resolve private encounter locations" = $questComponent -match 'TryResolvePapayaCapturedStaticNpcPosition\(mapClassName, anchorName'
	"captured named anchors resolve native tracker map points" = $questComponent -match 'GetStaticQuestLocationPoints[\s\S]*TryResolvePapayaCapturedStaticNpcPosition\(mapClassName, anchorName'
	"remote objective map points route through current-map warps first" = $questComponent -match 'MapPointGroupsReferenceCurrentMap' -and $questComponent -match 'GetStaticQuestRouteFallbackMapPointGroups\(quest, currentMap\)[\s\S]*return routePoints'
	"SIAUL_EAST_REQUEST6 Vubbe Fighter spawns in Nudage logging site" = $privateEncounterSource -match '"questName"\s*:\s*"SIAUL_EAST_REQUEST6"[\s\S]*"f_siauliai_2 2008 185 -389 250"' -and $questComponent -match 'SIAUL_EAST_REQUEST6[\s\S]*x\s*=\s*2008[\s\S]*y\s*=\s*185[\s\S]*z\s*=\s*-389'
	"Scout combat Kepas spawn on real layer after track cleanup" = $scoutTrack -match 'QueueRealScoutEncounter' -and $scoutTrack -match 'character\.Tracks\.ActiveTrack == null' -and $scoutTrack -match 'mob\.Layer = 0' -and $scoutTrack -match 'new\s+MovementComponent' -and $scoutTrack -match 'SetTarget\(character\)' -and $scoutTrack -match 'InsertHate\(character, 5000\)' -and $scoutTrack -match 'TendencyType\.Aggressive'
	"Zemyna statue advances only the active Laimonas objective and clears worship effects" = $questComponent -match 'AdvanceStaticNpcDialogProgressOnly' -and $npcFunctionsSource -match 'F_SIAULIAI_WEST_EV_55_001[\s\S]*ZEMINA_STATUE\(dialog\)' -and $npcFunctionsSource -match 'ZEMINA_STATUE[\s\S]*AdvanceStaticNpcDialogProgressOnly\(npc\.DialogName\)' -and $npcFunctionsSource -match 'ZEMINA_STATUE[\s\S]*DetachEffect\(character,\s*"F_pc_statue_wing"\)' -and $npcFunctionsSource -match 'ZEMINA_STATUE[\s\S]*OpenGoddessStatueWarpMap\(dialog\)'
	"Klaipeda handoff waits for completed West Siauliai road chain" = $questByName.ContainsKey("klapeda_go_to_east") -and @($questByName["klapeda_go_to_east"].Required) -contains "SIAUL_WEST_WOOD_SPIRIT" -and $questComponent -match 'this\.HasCompleted\(1019\)[\s\S]*TryFind\("KLAPEDA_GO_TO_EAST"'
	"West Siauliai Klaipeda warp waits for Road to Klaipeda completion" = $westSiauliaiWarpSource -match 'CanUseKlaipedaRoad' -and $westSiauliaiWarpSource -match 'HasCompleted\(1019\)' -and $westSiauliaiWarpSource -notmatch 'HasCompleted\(1015\)'
	"Premature Klaipeda handoff is removed instead of auto-completing West road quests" = $questComponent -match 'SuppressPrematureKlaipedaHandoffBeforeWestRoad' -and $questComponent -notmatch 'completed stale road handoff quest'
	"Klaipeda tracker filters premature handoff before cleanup" = $questComponent -match 'QuestShouldBeVisibleInClientList' -and $questComponent -match 'IsPrematureKlaipedaHandoffQuest' -and $questComponent -match 'return false;'
	"Klaipeda tracker filters skipped West road repair quests" = $questComponent -match 'QuestShouldBeVisibleInClientList' -and $questComponent -match 'IsSkippedWestRoadQuestAfterKlaipedaArrival' -and $questComponent -match 'return false;'
	"Klaipeda arrival repairs skipped West road handoff before East chain" = $questComponent -match 'CompleteSkippedWestSiauliaiRoadAfterKlaipedaArrival' -and $questComponent -match 'CompleteStaticQuestForPapayaFlow\(1018,\s*"SIAUL_WEST_LAIMONAS4"\)' -and $questComponent -match 'CompleteStaticQuestForPapayaFlow\(1019,\s*"SIAUL_WEST_WOOD_SPIRIT"\)'
	"Klaipeda Uska advances handoff to EAST_PREPARE instead of late West road kill quest" = $npcFunctionsSource -match 'CompleteKlaipedaUskaHandoffAndStartEastPrepare' -and $npcFunctionsSource -match 'CompleteStaticQuestForKlaipedaRecovery\(character,\s*1027,\s*"KLAPEDA_GO_TO_EAST"\)' -and $npcFunctionsSource -match 'EnsureStaticQuestInProgress\("EAST_PREPARE"\)'
	"Uska no longer auto-completes unfinished West road quests" = $npcFunctionsSource -match 'SyncCompletedWestSiauliaiRoadToKlapedaQuests[\s\S]*HasCompleted\(1019\)' -and $npcFunctionsSource -notmatch 'completed stale West Siauliai road quest'
}

foreach ($check in $runtimeChecks.GetEnumerator()) {
	if (-not $check.Value) {
		$errors.Add("Runtime coverage missing: $($check.Key).")
	}
}

$reachabilityQuests = @($quests | Where-Object { $_.Mode -eq "MAIN" -or $mainPrerequisiteBridgeNames.Contains($_.ClassName) })
$roots = @($reachabilityQuests | Where-Object { $_.Required.Count -eq 0 })
$reachable = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
$queue = New-Object System.Collections.Generic.Queue[object]
foreach ($root in $roots) {
    $reachable.Add($root.ClassName) | Out-Null
    $queue.Enqueue($root)
}

while ($queue.Count -gt 0) {
    $current = $queue.Dequeue()
    foreach ($next in $reachabilityQuests) {
        if ($reachable.Contains($next.ClassName)) { continue }
        if ($next.Required | Where-Object { $_ -eq $current.ClassName }) {
            $reachable.Add($next.ClassName) | Out-Null
            $queue.Enqueue($next)
        }
    }
}

$reachableMainCount = @($mainQuests | Where-Object { $reachable.Contains($_.ClassName) }).Count
$unreachable = @($mainQuests | Where-Object { -not $reachable.Contains($_.ClassName) -and -not (Test-CloverDisabledMainQuestName $_.ClassName) })
foreach ($quest in $unreachable) {
    $warnings.Add("Main quest $($quest.ClassName) [$($quest.Id)] is not reachable from a no-prerequisite MAIN/prerequisite-bridge root using requiredQuestName links only.")
}

$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $AppRoot "src") -Recurse -Filter "*.cs") +
    @(Get-ChildItem -LiteralPath (Join-Path $AppRoot "packages") -Recurse -Filter "*.cs")

foreach ($sourceFile in $sourceFiles) {
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $sourceFile.FullName) {
        $lineNumber++
        if ($line -match 'new\s+QuestId\s*\(\s*"Laima\.Quest"\s*,' -and $line -notmatch 'namespacedQuestId\s*=\s*new\s+QuestId') {
            $relativePath = [System.IO.Path]::GetRelativePath($AppRoot, $sourceFile.FullName)
            $errors.Add("Unsafe Laima.Quest QuestId construction at ${relativePath}:$lineNumber. Use TryGetNamespacedQuestId so quest ids above 65535 do not crash NPC dialogs.")
        }
    }
}

if ($npcFunctionsSource -notmatch 'QueueWestSiauliaiScoutKepaTrack[\s\S]*Task\.Delay\(650\)[\s\S]*SIAUL_WEST_DRASIUS1_TRACK') {
    $errors.Add("Papaya Scout/Kepa track must be launched through the deferred QueueWestSiauliaiScoutKepaTrack handoff.")
}

$highIdMainCount = @($mainQuests | Where-Object { $_.Id -gt 65535 }).Count
if ($highIdMainCount -gt 0) {
    $warnings.Add("$highIdMainCount MAIN quest(s) use ids above 65535; script lookup must remain guarded and raw/static progression must handle them.")
}

Write-Host "MAIN quests checked: $($mainQuests.Count)"
Write-Host "Reachable MAIN quests by prerequisite graph: $reachableMainCount"
Write-Host "MAIN quest_auto track entries checked: $($mainQuestAutoTracks.Count)"
Write-Host "MAIN NPC/location trigger candidates covered by runtime bridge: $($locationNpcTriggerCandidates.Count)"
Write-Host "MAIN objective quest fallback-spawn coverage candidates: $($objectiveMainQuests.Count)"
Write-Host "Non-MAIN prerequisite bridge quests kept enabled: $($mainPrerequisiteBridgeNames.Count)"
Write-Host "Non-MAIN static quests suppressed from Clover main flow: $nonMainQuestCount"
Write-Host "REPEAT/daily-like static quests suppressed from Clover main flow: $repeatQuestCount"
Write-Host "Errors: $($errors.Count)"
Write-Host "Warnings: $($warnings.Count)"

if ($errors.Count -gt 0) {
    $errors | Sort-Object -Unique | ForEach-Object { Write-Host "ERROR: $_" }
}

if ($ShowWarnings -and $warnings.Count -gt 0) {
    $warnings | Sort-Object -Unique | ForEach-Object { Write-Host "WARN: $_" }
}

if ($errors.Count -gt 0) {
    exit 1
}
