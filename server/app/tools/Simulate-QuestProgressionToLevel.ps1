param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [int]$TargetLevel = 500,
    [switch]$VerboseLog
)

$ErrorActionPreference = 'Stop'

function Get-Field {
    param([string]$Text, [string]$Name)
    $match = [regex]::Match($Text, "$Name\s*:\s*`"([^`"]*)`"")
    if ($match.Success) { return $match.Groups[1].Value }
    return ""
}

function Get-IntField {
    param([string]$Text, [string]$Name)
    $match = [regex]::Match($Text, "$Name\s*:\s*([0-9]+)")
    if ($match.Success) { return [int]$match.Groups[1].Value }
    return 0
}

function Get-ListField {
    param([string]$Text, [string]$Name)
    $match = [regex]::Match($Text, "$Name\s*:\s*\[(.*?)\]")
    if (-not $match.Success) { return @() }

    return @([regex]::Matches($match.Groups[1].Value, "`"([^`"]+)`"") |
        ForEach-Object { $_.Groups[1].Value })
}

function Get-IntListField {
    param([string]$Text, [string]$Name)
    $match = [regex]::Match($Text, "$Name\s*:\s*\[(.*?)\]")
    if (-not $match.Success) { return @() }

    return @([regex]::Matches($match.Groups[1].Value, '[0-9]+') |
        ForEach-Object { [int]$_.Value })
}

function Get-ConfNumber {
    param(
        [string[]]$Texts,
        [string]$Name,
        [double]$Default
    )

    $value = $Default
    foreach ($text in $Texts) {
        foreach ($match in [regex]::Matches($text, "(?m)^\s*$([regex]::Escape($Name))\s*:\s*([0-9.]+)\s*$")) {
            $value = [double]$match.Groups[1].Value
        }
    }
    return $value
}

function New-Set {
    return New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
}

function Add-Error {
    param([string]$Message)
    $script:Errors.Add($Message) | Out-Null
}

function Split-Targets {
    param([string]$Targets)

    if ([string]::IsNullOrWhiteSpace($Targets) -or $Targets -eq 'ALL') {
        return @()
    }

    return @($Targets.Split('/', [System.StringSplitOptions]::RemoveEmptyEntries) |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ })
}

function Remove-CSharpComments {
    param([string]$Text)

    $withoutBlockComments = [regex]::Replace($Text, '(?s)/\*.*?\*/', '')
    return [regex]::Replace($withoutBlockComments, '(?m)//.*$', '')
}

function Add-SpawnSetValue {
    param(
        [hashtable]$Table,
        [string]$MapName,
        [string]$MonsterName
    )

    if ([string]::IsNullOrWhiteSpace($MapName) -or [string]::IsNullOrWhiteSpace($MonsterName)) {
        return
    }

    $key = $MapName.ToLowerInvariant()
    if (-not $Table.ContainsKey($key)) {
        $Table[$key] = New-Set
    }
    $Table[$key].Add($MonsterName) | Out-Null
}

function Read-ActiveMapSpawns {
    param([string]$Root)

    $result = @{}
    $script:MapMobScripts = New-Set
    $mobsRoot = Join-Path $Root 'packages/laima/scripts/zone/content/laima/mobs'
    if (-not (Test-Path -LiteralPath $mobsRoot)) {
        return $result
    }

    foreach ($file in Get-ChildItem -LiteralPath $mobsRoot -Recurse -Filter '*.cs') {
        $script:MapMobScripts.Add([System.IO.Path]::GetFileNameWithoutExtension($file.Name)) | Out-Null
        $source = Remove-CSharpComments (Get-Content -LiteralPath $file.FullName -Raw)
        foreach ($match in [regex]::Matches($source, 'AddSpawner\(\s*"([^"]+?)\.[^"]*"\s*,\s*MonsterId\.([A-Za-z0-9_]+)')) {
            Add-SpawnSetValue $result $match.Groups[1].Value $match.Groups[2].Value
        }
    }

    return $result
}

function Read-PrivateEncounters {
    param([string]$Path)

    $result = @{}
    if (-not (Test-Path -LiteralPath $Path)) {
        return $result
    }

    $rows = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    foreach ($row in $rows) {
        $questName = [string]$row.questName
        $mapName = [string]$row.mapName
        $target = [string]$row.target
        if ([string]::IsNullOrWhiteSpace($questName) -or [string]::IsNullOrWhiteSpace($mapName) -or [string]::IsNullOrWhiteSpace($target)) {
            continue
        }

        foreach ($targetName in (Split-Targets $target)) {
            $key = "$($questName.ToLowerInvariant())/$($mapName.ToLowerInvariant())"
            if (-not $result.ContainsKey($key)) {
                $result[$key] = New-Set
            }
            $result[$key].Add($targetName) | Out-Null
        }
    }

    return $result
}

function Test-MapHasActiveSpawnerForTarget {
    param([string]$MapName, [string]$Targets)

    if ([string]::IsNullOrWhiteSpace($MapName)) { return $false }
    $mapKey = $MapName.ToLowerInvariant()
    if (-not $script:ActiveMapSpawns.ContainsKey($mapKey)) { return $false }

    foreach ($target in (Split-Targets $Targets)) {
        if ($script:ActiveMapSpawns[$mapKey].Contains($target)) {
            return $true
        }
    }
    return $false
}

function Test-PrivateEncounterCoversTarget {
    param($Quest, [string]$MapName, [string]$Targets)

    if ($null -eq $Quest -or [string]::IsNullOrWhiteSpace($MapName)) { return $false }
    $key = "$($Quest.ClassName.ToLowerInvariant())/$($MapName.ToLowerInvariant())"
    if (-not $script:PrivateEncounterTargets.ContainsKey($key)) { return $false }

    foreach ($target in (Split-Targets $Targets)) {
        if ($script:PrivateEncounterTargets[$key].Contains($target)) {
            return $true
        }
    }
    return $false
}

function Validate-MapPopulation {
    param($Quest)

    foreach ($mapName in @($Quest.StartMap, $Quest.ProgressMap, $Quest.EndMap)) {
        if ([string]::IsNullOrWhiteSpace($mapName)) { continue }

        $mapKey = $mapName.ToLowerInvariant()
        if ($script:MapMobScripts.Contains($mapName) -and
            (-not $script:ActiveMapSpawns.ContainsKey($mapKey) -or $script:ActiveMapSpawns[$mapKey].Count -eq 0)) {
            Add-Error "Quest $($Quest.ClassName) enters map '$mapName', but its mob script has no active AddSpawner calls. This would produce an empty gameplay map."
        }
    }
}

function Validate-ObjectiveSpawnCoverage {
    param($Quest, [string]$Targets, [string]$Phase)

    if ([string]::IsNullOrWhiteSpace($Targets) -or $Targets -eq 'ALL') { return }

    $maps = @($Quest.ProgressMap, $Quest.StartMap, $Quest.EndMap) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique

    foreach ($mapName in $maps) {
        if ((Test-MapHasActiveSpawnerForTarget $mapName $Targets) -or
            (Test-PrivateEncounterCoversTarget $Quest $mapName $Targets)) {
            return
        }
    }

    if ([string]::Equals($Phase, 'forced', [System.StringComparison]::OrdinalIgnoreCase)) {
        Add-Error "Forced quest $($Quest.ClassName) objective target '$Targets' has no active map spawner or private encounter on its quest maps."
    }
}

function Read-ExpTables {
    param([string]$Path)

    $text = Get-Content -LiteralPath $Path -Raw
    $baseBlock = [regex]::Match($text, '(?s)\{\s*exp\s*:\s*\[(.*?)\]\s*\},')
    $jobBlock = [regex]::Match($text, '(?s)\{\s*jobExp\s*:\s*\[(.*?)\]\s*\}')
    if (-not $baseBlock.Success -or -not $jobBlock.Success) {
        throw "Could not parse EXP table at $Path"
    }

    $base = @{}
    foreach ($match in [regex]::Matches($baseBlock.Groups[1].Value, '\{\s*level\s*:\s*([0-9]+),\s*exp\s*:\s*([0-9.E+]+)\s*\}')) {
        $base[[int]$match.Groups[1].Value] = [int64][math]::Round([double]$match.Groups[2].Value)
    }

    $job = @{}
    foreach ($match in [regex]::Matches($jobBlock.Groups[1].Value, '\{\s*rank\s*:\s*([0-9]+),\s*level\s*:\s*([0-9]+),\s*exp\s*:\s*([0-9.E+]+)\s*\}')) {
        $rank = [int]$match.Groups[1].Value
        $level = [int]$match.Groups[2].Value
        $key = "$rank/$level"
        $job[$key] = [int64][math]::Round([double]$match.Groups[3].Value)
    }

    return @{ Base = $base; Job = $job }
}

function Get-BaseMaxExp {
    param([int]$Level)
    if ($script:BaseExpTable.ContainsKey($Level)) { return [int64]$script:BaseExpTable[$Level] }
    return [int64]1
}

function Get-JobCumulativeExp {
    param([int]$Rank, [int]$Level)

    $sum = [int64]0
    for ($i = 1; $i -le $Level; $i++) {
        $key = "$Rank/$i"
        if ($script:JobExpTable.ContainsKey($key)) {
            $sum += [int64]$script:JobExpTable[$key]
        }
    }
    if ($sum -lt 1) { return [int64]1 }
    return [int64]$sum
}

function Get-JobMaxLevel {
    param([int]$Rank)
    if ($Rank -le 1) { return 15 }
    return 45
}

function Scale-Amount {
	param([int64]$Amount, [double]$Rate)

	if ($Amount -le 0) { return [int64]0 }
	$scaled = [math]::Round(([double]$Amount) * ($Rate / 100.0), [MidpointRounding]::AwayFromZero)
	if ($scaled -lt 1) { return [int64]1 }
	if ($scaled -ge [int64]::MaxValue) { return [int64]::MaxValue }
	return [int64]$scaled
}

function Add-BaseLevelAbilityPoints {
    param([int]$LevelCount)

    if ($LevelCount -le 0 -or $script:AbilityPointsPerLevel -le 0) { return }
    $amount = Scale-Amount ([int64]$LevelCount * [int64]$script:AbilityPointsPerLevel) $script:ExpRate
    $script:AbilityPoints += $amount
}

function Add-JobLevelAbilityPoints {
    param([int]$LevelCount)

    if ($LevelCount -le 0 -or $script:AbilityPointsPerJobLevel -le 0) { return }
    $amount = Scale-Amount ([int64]$LevelCount * [int64]$script:AbilityPointsPerJobLevel) $script:JobExpRate
    $script:AbilityPoints += $amount
}

function Add-Experience {
    param([int64]$BaseExp, [int64]$JobExp)

    $script:TotalBaseExpGained += $BaseExp
    $script:TotalJobExpGained += $JobExp
    $script:BaseExpRemainder += $BaseExp
    while ($script:Level -lt $script:MaxLevel -and $script:BaseExpRemainder -ge (Get-BaseMaxExp $script:Level)) {
        $script:BaseExpRemainder -= Get-BaseMaxExp $script:Level
        $script:Level++
        $script:BaseLevelsGained++
        Add-BaseLevelAbilityPoints 1
    }

    $script:JobTotalExp += $JobExp
    while ($true) {
        $jobMaxLevel = Get-JobMaxLevel $script:JobRank
        $nextJobThreshold = Get-JobCumulativeExp $script:JobRank $script:JobLevel
        if ($script:JobLevel -lt $jobMaxLevel -and $script:JobTotalExp -ge $nextJobThreshold) {
            $script:JobLevel++
            $script:JobLevelsGained++
            $script:SkillPoints++
            Add-JobLevelAbilityPoints 1
            continue
        }

        $jobTotalCap = Get-JobCumulativeExp $script:JobRank $jobMaxLevel
        if ($script:JobLevel -ge $jobMaxLevel -and $script:JobTotalExp -ge $jobTotalCap -and $script:JobRank -lt $script:JobMaxRank) {
            $script:JobRank++
            $script:JobLevel = 1
            $script:JobTotalExp = 0
            $script:SkillPoints++
            $script:JobAdvancements++
            continue
        }

        if ($script:JobTotalExp -gt $jobTotalCap) {
            $script:JobTotalExp = $jobTotalCap
        }
        break
    }
}

function Get-QuestExperienceRate {
    param($Quest)

    $isStarter = $false
    if ([string]::Equals($Quest.StartZone, 'StartLine1', [System.StringComparison]::OrdinalIgnoreCase)) {
        $isStarter = $true
    }
    if ($Quest.StartMap -eq 'f_siauliai_west' -or $Quest.ProgressMap -eq 'f_siauliai_west' -or $Quest.EndMap -eq 'f_siauliai_west') {
        $isStarter = $true
    }
    if ($Quest.ClassName.StartsWith('SIAUL_WEST', [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($Quest.ClassName, 'TUTO_SKILL_RUN', [System.StringComparison]::OrdinalIgnoreCase)) {
        $isStarter = $true
    }

    if ($isStarter) {
        if ([string]::Equals($Quest.Mode, 'MAIN', [System.StringComparison]::OrdinalIgnoreCase)) {
            $rate = 0.20
        } else {
            $rate = 0.10
        }
        if ($Quest.HasObjectives) { $rate += 0.25 }
        return $rate
    }

    $rate = 0.15
    if ([string]::Equals($Quest.Mode, 'MAIN', [System.StringComparison]::OrdinalIgnoreCase)) {
        $rate = 0.35
    } elseif ([string]::Equals($Quest.Mode, 'REPEAT', [System.StringComparison]::OrdinalIgnoreCase)) {
        $rate = 0.05
    }

    if ($Quest.HasObjectives) {
        if ([string]::Equals($Quest.Mode, 'MAIN', [System.StringComparison]::OrdinalIgnoreCase)) {
            $rate += 0.15
        } else {
            $rate += 0.05
        }
    }
    return $rate
}

function Get-ObjectiveObjects {
    param([string]$Raw)
    return @([regex]::Matches($Raw, '\{[^\{\}]*type\s*:\s*"[^"]+"[^\{\}]*\}') | ForEach-Object { $_.Value })
}

function Get-ObjectiveMonsterTargets {
    param([string]$Raw)

    $targets = New-Set
    foreach ($objective in (Get-ObjectiveObjects $Raw)) {
        $type = Get-Field $objective 'type'
        if ([string]::Equals($type, 'Kill', [System.StringComparison]::OrdinalIgnoreCase)) {
            foreach ($target in (Split-Targets (Get-Field $objective 'target'))) {
                $targets.Add($target) | Out-Null
            }
            continue
        }

        if ([string]::Equals($type, 'Collect', [System.StringComparison]::OrdinalIgnoreCase)) {
            foreach ($target in (Split-Targets (Get-Field $objective 'dropTarget'))) {
                $targets.Add($target) | Out-Null
            }
        }
    }

    return $targets
}

function Test-NativeTargetRequiresServerObjective {
    param([string]$Targets)

    foreach ($target in (Split-Targets $Targets)) {
        $key = $target.ToLowerInvariant()
        if (-not $script:Monsters.ContainsKey($key)) {
            continue
        }

        $monster = $script:Monsters[$key]
        if ([string]::Equals($monster.Rank, 'MISC', [System.StringComparison]::OrdinalIgnoreCase) -or
            [string]::Equals($monster.Faction, 'Neutral', [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        if ($monster.Exp -gt 0 -or $monster.JobExp -gt 0) {
            return $true
        }
    }

    return $false
}

function Get-FirstMonster {
    param([string]$Targets)

    foreach ($target in (Split-Targets $Targets)) {
        $key = $target.ToLowerInvariant()
        if ($script:Monsters.ContainsKey($key)) {
            return $script:Monsters[$key]
        }
    }
    return $null
}

function Validate-NativeSessionObjectiveCoverage {
    param($Quest, [string]$Phase)

    $key = $Quest.ClassName.ToLowerInvariant()
    if (-not $script:SessionQuestByName.ContainsKey($key)) {
        return
    }

    $sessionQuest = $script:SessionQuestByName[$key]
    $hasNativeCounter = $sessionQuest.InfoNames.Count -gt 0 -or $sessionQuest.InfoMaxCounts.Count -gt 0
    if (-not $hasNativeCounter) {
        return
    }

    $nativeTargets = @($sessionQuest.MonsterGroups | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and $_ -ne 'None' })
    if ($nativeTargets.Count -eq 0) {
        return
    }

    $requiresServerObjective = $false
    foreach ($nativeTarget in $nativeTargets) {
        if (Test-NativeTargetRequiresServerObjective $nativeTarget) {
            $requiresServerObjective = $true
            break
        }
    }

    if (-not $requiresServerObjective) {
        return
    }

    if (-not $Quest.HasObjectives) {
        Add-Error "Quest $($Quest.ClassName) exposes native session monster objective '$($nativeTargets -join '/')' but quests.txt has no real server objective. Player kills would not count."
        return
    }

    $objectiveTargets = Get-ObjectiveMonsterTargets $Quest.Raw
    foreach ($nativeTarget in $nativeTargets) {
        $covered = $false
        foreach ($target in (Split-Targets $nativeTarget)) {
            if ($objectiveTargets.Contains($target)) {
                $covered = $true
                break
            }
        }

        if (-not $covered) {
            Add-Error "Quest $($Quest.ClassName) native session target '$nativeTarget' is not covered by any Kill target or Collect dropTarget in quests.txt."
            continue
        }

        Validate-ObjectiveSpawnCoverage $Quest $nativeTarget $Phase
    }
}

function Validate-PapayaPlayableBossTrackCoverage {
    param($Quest)

    $playableBossTrackQuests = @(
        'GELE573_MQ_09',
        'GELE574_MQ_09'
    )

    if ($playableBossTrackQuests -notcontains $Quest.ClassName) {
        return
    }

    $key = $Quest.ClassName.ToLowerInvariant()
    if (-not $script:QuestAutoByName.ContainsKey($key)) {
        Add-Error "Quest $($Quest.ClassName) must run a Papaya boss track, but quest_auto.txt has no entry for it."
        return
    }

    $track = $script:QuestAutoByName[$key].Track
    if ($track -notmatch '/m_boss_') {
        Add-Error "Quest $($Quest.ClassName) must remain a playable boss encounter, but its quest_auto track '$track' is not a boss track."
    }

    if (-not $Quest.HasObjectives) {
        Add-Error "Quest $($Quest.ClassName) uses Papaya boss track '$track' but has no real server objective; it would auto-complete by timer."
        return
    }

    $objectiveTargets = Get-ObjectiveMonsterTargets $Quest.Raw
    if ($objectiveTargets.Count -eq 0) {
        Add-Error "Quest $($Quest.ClassName) uses Papaya boss track '$track' but no Kill target or Collect dropTarget is defined."
        return
    }

    foreach ($objectiveTarget in $objectiveTargets) {
        Validate-ObjectiveSpawnCoverage $Quest $objectiveTarget 'forced'
    }
}

function Validate-MapField {
    param($Quest, [string]$FieldName, [string]$MapName)

    if ([string]::IsNullOrWhiteSpace($MapName)) { return }
    if (-not $script:Maps.Contains($MapName)) {
        Add-Error "Quest $($Quest.ClassName) references missing $FieldName map '$MapName'."
    }
}

function Validate-And-Apply-Objectives {
    param($Quest, [string]$Phase)

    foreach ($objective in (Get-ObjectiveObjects $Quest.Raw)) {
        $type = Get-Field $objective 'type'
        $count = Get-IntField $objective 'count'
        if ($count -lt 1) { $count = 1 }

        if ([string]::Equals($type, 'Kill', [System.StringComparison]::OrdinalIgnoreCase)) {
            $target = Get-Field $objective 'target'
            $monster = Get-FirstMonster $target
            if ($null -eq $monster -and -not [string]::IsNullOrWhiteSpace($target) -and $target -ne 'ALL') {
                Add-Error "Quest $($Quest.ClassName) kill objective references missing monster target '$target'."
            } elseif ($null -ne $monster) {
                Validate-ObjectiveSpawnCoverage $Quest $target $Phase
                Add-Experience (Scale-Amount ([int64]$monster.Exp * $count) $script:ExpRate) (Scale-Amount ([int64]$monster.JobExp * $count) $script:JobExpRate)
                $script:ObjectiveKills += $count
            }
        } elseif ([string]::Equals($type, 'Collect', [System.StringComparison]::OrdinalIgnoreCase)) {
            $item = Get-Field $objective 'item'
            if (-not [string]::IsNullOrWhiteSpace($item) -and -not $script:Items.Contains($item)) {
                Add-Error "Quest $($Quest.ClassName) collect objective references missing item '$item'."
            }

            $dropTarget = Get-Field $objective 'dropTarget'
            $monster = Get-FirstMonster $dropTarget
            if ($null -eq $monster -and -not [string]::IsNullOrWhiteSpace($dropTarget) -and $dropTarget -ne 'ALL') {
                Add-Error "Quest $($Quest.ClassName) collect objective references missing drop target '$dropTarget'."
            } elseif ($null -ne $monster) {
                Validate-ObjectiveSpawnCoverage $Quest $dropTarget $Phase
                Add-Experience (Scale-Amount ([int64]$monster.Exp * $count) $script:ExpRate) (Scale-Amount ([int64]$monster.JobExp * $count) $script:JobExpRate)
                $script:ObjectiveKills += $count
            }
        } else {
            $item = Get-Field $objective 'item'
            if (-not [string]::IsNullOrWhiteSpace($item) -and -not $script:Items.Contains($item)) {
                Add-Error "Quest $($Quest.ClassName) objective references missing item '$item'."
            }
        }
    }
}

function Complete-Quest {
    param($Quest, [string]$Phase)

    Validate-MapField $Quest 'start' $Quest.StartMap
    Validate-MapField $Quest 'progress' $Quest.ProgressMap
    Validate-MapField $Quest 'end' $Quest.EndMap
    Validate-MapPopulation $Quest

    Validate-PapayaPlayableBossTrackCoverage $Quest
    Validate-NativeSessionObjectiveCoverage $Quest $Phase
    Validate-And-Apply-Objectives $Quest $Phase

    $rate = Get-QuestExperienceRate $Quest
    $questBaseExp = [int64][math]::Ceiling((Get-BaseMaxExp $script:Level) * $rate)
    $questJobExp = [int64][math]::Ceiling((Get-JobCumulativeExp $script:JobRank $script:JobLevel) * $rate)
    if ($questBaseExp -lt 1) { $questBaseExp = [int64]1 }
    if ($questJobExp -lt 1) { $questJobExp = [int64]1 }
    Add-Experience (Scale-Amount $questBaseExp $script:ExpRate) (Scale-Amount $questJobExp $script:JobExpRate)

    $script:Completed.Add($Quest.ClassName) | Out-Null
    $script:CompletedQuestCount++
    if ([string]::Equals($Quest.Mode, 'MAIN', [System.StringComparison]::OrdinalIgnoreCase)) {
        $script:CompletedMainQuestCount++
    } elseif ([string]::Equals($Quest.Mode, 'SUB', [System.StringComparison]::OrdinalIgnoreCase)) {
        $script:CompletedSubQuestCount++
    }

    if ($VerboseLog) {
        Write-Host ("{0,-8} L{1,3} C{2}/{3,-2} AP {4,9} {5}" -f $Phase, $script:Level, $script:JobRank, $script:JobLevel, $script:AbilityPoints, $Quest.ClassName)
    }
}

function Test-IsDisabledQuest {
    param($Quest)

    if ([string]::IsNullOrWhiteSpace($Quest.ClassName)) { return $true }
    if ($Quest.Name -match 'Delete|Deleted') { return $true }
    if ([string]::Equals($Quest.Mode, 'REPEAT', [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    if ($Quest.ClassName.StartsWith('JOB_', [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    if ($Quest.ClassName.StartsWith('ASSISTOR_TUTO_', [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    if ($Quest.ClassName.StartsWith('TOSHERO_TUTO_', [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    if ($Quest.ClassName.StartsWith('TUTO_', [System.StringComparison]::OrdinalIgnoreCase) -and
        -not [string]::Equals($Quest.ClassName, 'TUTO_SKILL_RUN', [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }
    return $false
}

function Test-QuestAvailable {
    param($Quest)

    if (Test-IsDisabledQuest $Quest) { return $false }
    if ($script:Completed.Contains($Quest.ClassName)) { return $false }
    if (-not [string]::Equals($Quest.Mode, 'MAIN', [System.StringComparison]::OrdinalIgnoreCase) -and $Quest.Level -gt 0 -and $Quest.Level -lt 9999 -and $script:Level -lt $Quest.Level) {
        return $false
    }

    foreach ($required in $Quest.Required) {
        if ([string]::IsNullOrWhiteSpace($required)) { continue }
        if (-not $script:Completed.Contains($required)) {
            return $false
        }
    }
    return $true
}

function Read-QuestRows {
    param([string]$Path)

    $result = @()
    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($line -notmatch '^\s*\{') { continue }
        $className = Get-Field $line 'className'
        if ([string]::IsNullOrWhiteSpace($className)) { continue }

        $result += [pscustomobject]@{
            Id = Get-IntField $line 'id'
            ClassName = $className
            Name = Get-Field $line 'name'
            Level = Get-IntField $line 'level'
            Mode = Get-Field $line 'questMode'
            StartMode = Get-Field $line 'questStartMode'
            EndMode = Get-Field $line 'questEndMode'
            StartZone = Get-Field $line 'questStartZone'
            StartMap = Get-Field $line 'startMap'
            ProgressMap = Get-Field $line 'progressMap'
            EndMap = Get-Field $line 'endMap'
            Required = @(Get-ListField $line 'requiredQuestName')
            HasObjectives = ($line -match 'objectives\s*:\s*\[')
            Raw = $line
        }
    }
    return @($result)
}

function Read-SessionQuestRows {
    param([string]$Path)

    $result = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($line -notmatch 'quest\s*:\s*\{') { continue }
        $questBlockMatch = [regex]::Match($line, 'quest\s*:\s*\{(.*?)\}\s*\}', [System.Text.RegularExpressions.RegexOptions]::Singleline)
        if (-not $questBlockMatch.Success) { continue }

        $questBlock = $questBlockMatch.Groups[1].Value
        $questName = Get-Field $questBlock 'name'
        if ([string]::IsNullOrWhiteSpace($questName)) { continue }

        $result[$questName.ToLowerInvariant()] = [pscustomobject]@{
            QuestName = $questName
            InfoNames = @(Get-ListField $questBlock 'infoName')
            InfoMaxCounts = @(Get-IntListField $questBlock 'infoMaxCount')
            MonsterGroups = @(Get-ListField $questBlock 'monsterNameGroup')
            Raw = $line
        }
    }
    return $result
}

function Read-QuestAutoRows {
    param([string]$Path)

    $result = @{}
    $text = Get-Content -LiteralPath $Path -Raw
    foreach ($match in [regex]::Matches($text, '(?s)\{.*?"questName"\s*:\s*"([^"]+)".*?"track"\s*:\s*"([^"]*)".*?\}')) {
        $questName = $match.Groups[1].Value
        if ([string]::IsNullOrWhiteSpace($questName)) { continue }

        $result[$questName.ToLowerInvariant()] = [pscustomobject]@{
            QuestName = $questName
            Track = $match.Groups[2].Value
        }
    }
    return $result
}

function Read-ForcedOrder {
    param([string]$QuestComponentSource)

    $orderedNames = New-Object System.Collections.Generic.List[string]
    $seen = New-Set

    $westMatch = [regex]::Match($QuestComponentSource, '(?s)WestSiauliaiMainQuestOrder\s*=\s*\{(.*?)\};')
    if ($westMatch.Success) {
        foreach ($idMatch in [regex]::Matches($westMatch.Groups[1].Value, '[0-9]+')) {
            $id = [int]$idMatch.Value
            if ($script:QuestById.ContainsKey($id)) {
                $name = $script:QuestById[$id].ClassName
                if ($seen.Add($name)) { $orderedNames.Add($name) }
            }
        }
    }

    $papayaMatch = [regex]::Match($QuestComponentSource, '(?s)PapayaCapturedMainQuestOrder\s*=\s*\{(.*?)\};')
    if ($papayaMatch.Success) {
        foreach ($nameMatch in [regex]::Matches($papayaMatch.Groups[1].Value, '"([^"]+)"')) {
            $name = $nameMatch.Groups[1].Value
            if ($seen.Add($name)) { $orderedNames.Add($name) }
        }
    }

    return @($orderedNames)
}

$questPath = Join-Path $Root 'system/db/quests.txt'
$questAutoPath = Join-Path $Root 'system/db/quest_auto.txt'
$expPath = Join-Path $Root 'system/db/exp.txt'
$mapPath = Join-Path $Root 'system/db/maps.txt'
$monsterPath = Join-Path $Root 'system/db/monsters.txt'
$itemPath = Join-Path $Root 'system/db/items.txt'
$sessionObjectPath = Join-Path $Root 'system/db/sessionobjects.txt'
$privateEncounterPath = Join-Path $Root 'system/db/private_encounters.txt'
$questComponentPath = Join-Path $Root 'src/ZoneServer/World/Actors/Characters/Components/QuestComponent.cs'
$npcFunctionsPath = Join-Path $Root 'src/ZoneServer/Scripting/Shared/NPCFunctions.cs'
$characterStatsPath = Join-Path $Root 'src/ZoneServer/World/Actors/Characters/Character.Stats.cs'
$trackComponentPath = Join-Path $Root 'src/ZoneServer/World/Actors/Characters/Components/TrackComponent.cs'
$packetHandlerPath = Join-Path $Root 'src/ZoneServer/Network/PacketHandler.cs'
$packageExpConfPath = Join-Path $Root 'packages/laima/conf/world/exp.conf'
$userExpConfPath = Join-Path $Root 'user/conf/world/exp.conf'

foreach ($requiredPath in @($questPath, $questAutoPath, $expPath, $mapPath, $monsterPath, $itemPath, $sessionObjectPath, $questComponentPath, $npcFunctionsPath, $characterStatsPath, $trackComponentPath, $packetHandlerPath, $packageExpConfPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Missing required simulator input: $requiredPath"
    }
}

$expConfTexts = @((Get-Content -LiteralPath $packageExpConfPath -Raw))
if (Test-Path -LiteralPath $userExpConfPath) {
    $expConfTexts += (Get-Content -LiteralPath $userExpConfPath -Raw)
}

$script:ExpRate = Get-ConfNumber $expConfTexts 'exp_rate' 100
$script:JobExpRate = Get-ConfNumber $expConfTexts 'job_exp_rate' 100
$script:AbilityPointsPerLevel = [int](Get-ConfNumber $expConfTexts 'ability_points_per_level' 100)
$script:AbilityPointsPerJobLevel = [int](Get-ConfNumber $expConfTexts 'ability_points_per_job_level' 100)
$script:MaxLevel = 550
$script:JobMaxRank = 4

$tables = Read-ExpTables $expPath
$script:BaseExpTable = $tables.Base
$script:JobExpTable = $tables.Job

$script:Maps = New-Set
foreach ($match in [regex]::Matches((Get-Content -LiteralPath $mapPath -Raw), 'className:\s*"([^"]+)"')) {
    $script:Maps.Add($match.Groups[1].Value) | Out-Null
}

$script:Items = New-Set
foreach ($match in [regex]::Matches((Get-Content -LiteralPath $itemPath -Raw), 'className:\s*"([^"]+)"')) {
    $script:Items.Add($match.Groups[1].Value) | Out-Null
}

$script:Monsters = @{}
foreach ($line in Get-Content -LiteralPath $monsterPath) {
    if ($line -notmatch '^\s*\{') { continue }
    $className = Get-Field $line 'className'
    if ([string]::IsNullOrWhiteSpace($className)) { continue }
        $script:Monsters[$className.ToLowerInvariant()] = [pscustomobject]@{
            ClassName = $className
            Level = Get-IntField $line 'level'
            Exp = Get-IntField $line 'exp'
            JobExp = Get-IntField $line 'jobExp'
            Hp = Get-IntField $line 'hp'
            Faction = Get-Field $line 'faction'
            Rank = Get-Field $line 'rank'
        }
}

$script:ActiveMapSpawns = Read-ActiveMapSpawns $Root
$script:PrivateEncounterTargets = Read-PrivateEncounters $privateEncounterPath
$script:QuestAutoByName = Read-QuestAutoRows $questAutoPath
$script:SessionQuestByName = Read-SessionQuestRows $sessionObjectPath

$quests = Read-QuestRows $questPath
$script:QuestByName = @{}
$script:QuestById = @{}
foreach ($quest in $quests) {
    $script:QuestByName[$quest.ClassName.ToLowerInvariant()] = $quest
    $script:QuestById[$quest.Id] = $quest
}

$script:Errors = New-Object System.Collections.Generic.List[string]
$script:Completed = New-Set
$script:Level = 1
$script:BaseExpRemainder = [int64]0
$script:JobRank = 1
$script:JobLevel = 1
$script:JobTotalExp = [int64]0
$script:SkillPoints = 1
$script:AbilityPoints = [int64]0
$script:BaseLevelsGained = 0
$script:JobLevelsGained = 0
$script:JobAdvancements = 0
$script:CompletedQuestCount = 0
$script:CompletedMainQuestCount = 0
$script:CompletedSubQuestCount = 0
$script:ObjectiveKills = 0
$script:TotalBaseExpGained = [int64]0
$script:TotalJobExpGained = [int64]0

foreach ($requiredSpawn in @(
    [pscustomobject]@{ Map = 'f_siauliai_west'; Monster = 'Leaf_Diving'; Reason = 'early Search Scout/Drasius collect flow' },
    [pscustomobject]@{ Map = 'f_siauliai_west'; Monster = 'Onion'; Reason = 'early West Siauliai field population' },
    [pscustomobject]@{ Map = 'f_siauliai_west'; Monster = 'Hanaming'; Reason = 'Battle Commander intro flow' },
    [pscustomobject]@{ Map = 'f_siauliai_west'; Monster = 'InfroRocktor'; Reason = 'Road to Klaipeda flow' },
    [pscustomobject]@{ Map = 'f_siauliai_2'; Monster = 'Popolion_Blue'; Reason = "Aras' Commission clue/objective flow" },
    [pscustomobject]@{ Map = 'f_siauliai_2'; Monster = 'Chupacabra_Blue'; Reason = 'East Siauliai reclaim/objective flow' },
    [pscustomobject]@{ Map = 'f_siauliai_2'; Monster = 'Pokubu'; Reason = 'East Siauliai supply/objective flow' },
    [pscustomobject]@{ Map = 'f_siauliai_2'; Monster = 'Weaver'; Reason = 'East Siauliai follow-up objective flow' }
)) {
    if (-not (Test-MapHasActiveSpawnerForTarget $requiredSpawn.Map $requiredSpawn.Monster)) {
        Add-Error "Required ambient spawn '$($requiredSpawn.Monster)' is not active on '$($requiredSpawn.Map)' for $($requiredSpawn.Reason)."
    }
}

$questComponentSource = Get-Content -LiteralPath $questComponentPath -Raw
$npcFunctionsSource = Get-Content -LiteralPath $npcFunctionsPath -Raw
$characterStatsSource = Get-Content -LiteralPath $characterStatsPath -Raw
$trackComponentSource = Get-Content -LiteralPath $trackComponentPath -Raw
$packetHandlerSource = Get-Content -LiteralPath $packetHandlerPath -Raw

foreach ($requiredFrameName in @('mainstatus', 'buff', 'buff_separatedlist', 'questinfoset_2', 'quickslotnexpbar')) {
    if ($characterStatsSource -notmatch [regex]::Escape($requiredFrameName)) {
        Add-Error "HUD recovery does not force client frame '$requiredFrameName' visible after cutscene/track recovery."
    }
}

if ($trackComponentSource -notmatch 'AbortGenericTrackAfterMapTransition' -or
    $trackComponentSource -notmatch 'SourceMapClassName') {
    Add-Error "Generic quest_auto tracks are not aborted when they survive a map transition."
}

if ($packetHandlerSource -notmatch 'AbortGenericTrackAfterMapTransition') {
    Add-Error "Map load completion does not clear stale generic quest_auto tracks before restoring HUD."
}

if ($npcFunctionsSource -notmatch 'WARP_F_SIAULIAI_OUT[\s\S]*RepairPapayaMainQuestFlow[\s\S]*RestoreCoreHudState') {
    Add-Error "Miners' Village goddess statue does not repair Papaya flow and restore HUD when worshipped."
}

if ($npcFunctionsSource -notmatch 'SIAULIAIOUT_Q01[\s\S]*COMMON_QUEST_HANDLER') {
    Add-Error "Miners' Village first Papaya actor SIAULIAIOUT_Q01 has no quest-dialog bridge."
}

if ($questComponentSource -notmatch 'RepairPapayaGelePlateauImminentInvasion' -or
    $questComponentSource -notmatch 'TryCompletePapayaGelePlateauImminentInvasion' -or
    $questComponentSource -notmatch 'GELE572_MQ_01_TRACK' -or
    $questComponentSource -notmatch 'client-native track is not reliable') {
    Add-Error "Gele Plateau GELE572_MQ_01 still relies on the generic client-native quest_auto track instead of completing on server-side map sync."
}

foreach ($requiredSkippedQuest in @(
    'SOUT_Q_16',
    'MINE_1_ALCHEMIST',
    'MINE_1_CRYSTAL_2',
    'MINE_1_CRYSTAL_8',
    'MINE_1_CRYSTAL_9',
    'MINE_1_CRYSTAL_10',
    'MINE_1_CRYSTAL_13',
    'MINE_1_CRYSTAL_18',
    'MINE_1_CRYSTAL_19',
    'MINE_2_ALCHEMIST',
    'MINE_2_CRYSTAL_2',
    'MINE_2_CRYSTAL_3',
    'MINE_2_CRYSTAL_4',
    'MINE_2_CRYSTAL_5',
    'MINE_2_CRYSTAL_7',
    'MINE_2_CRYSTAL_10',
    'MINE_2_CRYSTAL_11',
    'MINE_2_CRYSTAL_14',
    'MINE_2_CRYSTAL_20',
    'MINE_2_CRYSTAL_21',
    'MINE_3_RESQUE1',
    'MINE_3_RESQUE3',
    'ACT4_MINE3_ENTER',
    'MINE_3_BOSS',
    'CMINE6_TO_KATYN7_1',
    'CMINE6_TO_KATYN7_2',
    'CMINE6_TO_KATYN7_3',
    'SOUT_Q_41'
)) {
    if ($questComponentSource -notmatch ('"' + [regex]::Escape($requiredSkippedQuest) + '"')) {
        Add-Error "Papaya Crystal Mine skip list is missing '$requiredSkippedQuest'."
    }
}

foreach ($requiredMinersCleanupQuest in @(
    'SOUT_Q_05',
    'SOUT_Q_07',
    'SOUT_Q_08',
    'SOUT_Q_09',
    'SOUT_Q_10',
    'SOUT_SUDD_PREBOSS',
    'SOUT_Q_13',
    'SOUT_Q_15',
    'SOUT_Q_20',
    'SOUT_Q_21',
    'SOUT_Q_22',
    'SOUT_Q_23',
    'SOUT_Q_24',
    'SOUT_Q_31',
    'SOUT_Q_32'
)) {
    if ($questComponentSource -notmatch ('"' + [regex]::Escape($requiredMinersCleanupQuest) + '"')) {
        Add-Error "Papaya Miners' Village cleanup list is missing '$requiredMinersCleanupQuest'."
    }
}

$forcedOrder = @(Read-ForcedOrder $questComponentSource)

foreach ($questName in $forcedOrder) {
    $key = $questName.ToLowerInvariant()
    if (-not $script:QuestByName.ContainsKey($key)) {
        Add-Error "Forced quest order references missing quest '$questName'."
        continue
    }

    $quest = $script:QuestByName[$key]
    foreach ($required in $quest.Required) {
        if ([string]::IsNullOrWhiteSpace($required)) { continue }
        if (-not $script:Completed.Contains($required)) {
            Add-Error "Forced quest '$($quest.ClassName)' started before required quest '$required' was completed."
        }
    }
    Complete-Quest $quest 'forced'
}

while ($script:Level -lt $TargetLevel) {
    $available = @($quests |
        Where-Object { Test-QuestAvailable $_ } |
        Sort-Object @{ Expression = { if ($_.Level -gt 0 -and $_.Level -lt 9999) { $_.Level } else { 0 } } }, Id)

    if ($available.Count -eq 0) {
        break
    }

    Complete-Quest $available[0] 'auto'
}

if ($script:Level -lt $TargetLevel) {
    $blocked = @($quests |
        Where-Object { -not (Test-IsDisabledQuest $_) -and -not $script:Completed.Contains($_.ClassName) } |
        Select-Object -First 20)

    Add-Error "Simulation stalled at level $script:Level before target level $TargetLevel. Remaining candidate quests: $($blocked.Count)."
    foreach ($quest in $blocked) {
        $missing = @($quest.Required | Where-Object { -not $script:Completed.Contains($_) })
        Add-Error "Blocked quest $($quest.ClassName) level $($quest.Level), missing prerequisites: $($missing -join ', ')"
    }
}

if ($script:JobLevel -le 1 -and $script:JobRank -eq 1) {
    Add-Error "Class level did not progress from level 1 during simulation."
}

if ($script:SkillPoints -le 1) {
    Add-Error "Class/skill points did not grow during simulation."
}

if ($script:AbilityPoints -le 0) {
    Add-Error "Attribute points did not grow during simulation."
}

if ($script:Errors.Count -gt 0) {
    Write-Host "Quest progression simulation FAILED."
    foreach ($errorLine in $script:Errors) {
        Write-Host "ERROR: $errorLine"
    }
    exit 1
}

Write-Host "Quest progression simulation passed."
Write-Host ("TargetLevel={0}; FinalLevel={1}; CompletedQuests={2}; Main={3}; Sub={4}" -f $TargetLevel, $script:Level, $script:CompletedQuestCount, $script:CompletedMainQuestCount, $script:CompletedSubQuestCount)
Write-Host ("ClassRank={0}; ClassLevel={1}; SkillPoints={2}; JobAdvancements={3}; JobLevelsGained={4}" -f $script:JobRank, $script:JobLevel, $script:SkillPoints, $script:JobAdvancements, $script:JobLevelsGained)
Write-Host ("AbilityPoints={0}; BaseLevelsGained={1}; ObjectiveKills={2}" -f $script:AbilityPoints, $script:BaseLevelsGained, $script:ObjectiveKills)
Write-Host ("BaseExpGained={0}; JobExpGained={1}; ExpRate={2}; JobExpRate={3}" -f $script:TotalBaseExpGained, $script:TotalJobExpGained, $script:ExpRate, $script:JobExpRate)
