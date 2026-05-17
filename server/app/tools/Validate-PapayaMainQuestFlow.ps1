param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

$sequence = @(
    'KLAPEDA_GO_TO_EAST',
    'EAST_PREPARE',
    'EAST_PREPARE_1',
    'SIAUL_EAST_RECLAIM1',
    'SIAUL_EAST_REQUEST1',
    'SIAUL_EAST_REQUEST2',
    'SIAUL_EAST_REQUEST3',
    'SIAUL_EAST_REQUEST6',
    'SIAUL_EAST_REQUEST7',
    'SOUT_Q_01',
    'SOUT_Q_14',
    'SOUT_Q_16',
    'MINE_1_ALCHEMIST',
    'MINE_2_ALCHEMIST',
    'MINE_3_RESQUE1',
    'CMINE6_TO_KATYN7_1',
    'CMINE6_TO_KATYN7_2',
    'CMINE6_TO_KATYN7_3',
    'SOUT_Q_41',
    'GELE572_MQ_01',
    'GELE573_MQ_07',
    'GELE573_MQ_09',
    'GELE573_MQ_08',
    'GELE574_MQ_09',
    'CHAPLE575_MQ_04',
    'CHAPLE575_MQ_09'
)

$requiredFiles = @{
    Quests = 'system/db/quests.txt'
    QuestAuto = 'system/db/quest_auto.txt'
    PrivateEncounters = 'system/db/private_encounters.txt'
    SessionObjects = 'system/db/sessionobjects.txt'
    ClientScript = 'src/ZoneServer/Scripting/ClientScript.cs'
    Send = 'src/ZoneServer/Network/Send.cs'
    PacketHandler = 'src/ZoneServer/Network/PacketHandler.cs'
    CharacterJobSkills = 'src/ZoneServer/World/Actors/Characters/Character.JobSkills.cs'
    CharacterStats = 'src/ZoneServer/World/Actors/Characters/Character.Stats.cs'
    ZoneDbCharacter = 'src/ZoneServer/Database/ZoneDb.Character.cs'
    ZoneDbInternal = 'src/ZoneServer/Database/ZoneDbInternal.cs'
    BuffComponent = 'src/ZoneServer/World/Actors/CombatEntities/Components/BuffComponent.cs'
    Buffs = 'system/db/buffs.txt'
    PackageBuffs = 'packages/laima/db/buffs.txt'
    Version390044Buffs = 'system/versions/390044/db/buffs.txt'
    QuestComponent = 'src/ZoneServer/World/Actors/Characters/Components/QuestComponent.cs'
    CustomPropertyShopClient = 'packages/laima/scripts/zone/core/client/custom_propertyshop/main.cs'
    KlaipedaWarps = 'packages/laima/scripts/zone/content/laima/warps/cities/c_Klaipe.cs'
    EastWarps = 'packages/laima/scripts/zone/content/laima/warps/fields/f_siauliai_2.cs'
    KlaipedaNpcs = 'packages/laima/scripts/zone/content/laima/npcs/cities/c_klaipe.cs'
    EastNpcs = 'packages/laima/scripts/zone/content/laima/npcs/fields/f_siauliai_2.cs'
    Gele572Npcs = 'packages/laima/scripts/zone/content/laima/npcs/fields/f_gele_57_2.cs'
    NefritasNpcs = 'packages/laima/scripts/zone/content/laima/npcs/fields/f_gele_57_3.cs'
    Gele574Npcs = 'packages/laima/scripts/zone/content/laima/npcs/fields/f_gele_57_4.cs'
    TenetB1Npcs = 'packages/laima/scripts/zone/content/laima/npcs/dungeons/d_chapel_57_5.cs'
    TenetB1Warps = 'packages/laima/scripts/zone/content/laima/warps/dungeons/d_chapel_57_5.cs'
    WestMobs = 'packages/laima/scripts/zone/content/laima/mobs/fields/f_siauliai_west.cs'
    EastMobs = 'packages/laima/scripts/zone/content/laima/mobs/fields/f_siauliai_2.cs'
    TenetB1Mobs = 'packages/laima/scripts/zone/content/laima/mobs/dungeons/d_chapel_57_5.cs'
    ExpConf = 'packages/laima/conf/world/exp.conf'
    EarlyTracks = 'packages/laima/scripts/zone/content/laima/tracks/fields/siaul_early_main_tracks.cs'
    Mob = 'src/ZoneServer/World/Actors/Monsters/Mob.cs'
    NpcFunctions = 'src/ZoneServer/Scripting/Shared/NPCFunctions.cs'
}

$content = @{}
foreach ($entry in $requiredFiles.GetEnumerator()) {
    $path = Join-Path $Root $entry.Value
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required file: $path"
    }
    $content[$entry.Key] = Get-Content -LiteralPath $path -Raw
}

$errors = New-Object System.Collections.Generic.List[string]

foreach ($questName in $sequence) {
    if ($content.Quests -notmatch ('className:\s*"' + [regex]::Escape($questName) + '"')) {
        $errors.Add("Quest missing in quests.txt: $questName")
    }

    if ($content.QuestComponent -notmatch ('"' + [regex]::Escape($questName) + '"')) {
        $errors.Add("Quest missing from PapayaCapturedMainQuestOrder/repair code: $questName")
    }
}

for ($i = 0; $i -lt $sequence.Count - 1; $i++) {
    $from = $sequence[$i]
    $to = $sequence[$i + 1]
    $autoPattern = '(?s)"questName"\s*:\s*"' + [regex]::Escape($from) + '".*?"successNextQuestNames"\s*:\s*\[[^\]]*"' + [regex]::Escape($to) + '"'
    $repairPattern = 'RepairPapayaCapturedMainQuestStep\("' + [regex]::Escape($from) + '",\s*"' + [regex]::Escape($to) + '"'
    $genericRepairPattern = 'for\s*\(\s*var\s+i\s*=\s*0;\s*i\s*<\s*PapayaCapturedMainQuestOrder\.Length\s*-\s*1'

    if (($content.QuestAuto -notmatch $autoPattern) -and
        ($content.QuestComponent -notmatch $repairPattern) -and
        ($content.QuestComponent -notmatch $genericRepairPattern)) {
        $errors.Add("No quest_auto or repair handoff found: $from -> $to")
    }
}

$criticalChecks = @{
    'Klaipeda -> East Siauliai warp WS_KLAPEDA_SIAULST2' = $content.KlaipedaWarps -match 'WS_KLAPEDA_SIAULST2' -and $content.KlaipedaWarps -match 'To\("f_siauliai_2"'
    'Klaipeda does not bypass East Siauliai directly to Miners Village' = $content.KlaipedaWarps -notmatch 'To\("f_siauliai_out"'
    'East Siauliai -> Klaipeda return warp WS_SIAULST2_KLAPEDA' = $content.EastWarps -match 'WS_SIAULST2_KLAPEDA'
    'Uska static NPC with KLAPEDA_USKA dialog' = $content.KlaipedaNpcs -match 'KLAPEDA_USKA'
    'Mirina static NPC mapped to EMILIA dialog' = $content.KlaipedaNpcs -match 'DialogName\s*=\s*"EMILIA"'
    'Ronesa static NPC mapped to ALFONSO dialog' = $content.KlaipedaNpcs -match 'DialogName\s*=\s*"ALFONSO"'
    'Knight Aras static NPC with SIAUL_EAST_MANAGER dialog' = $content.EastNpcs -match 'SIAUL_EAST_MANAGER'
    'COMMON quest handler available' = $content.NpcFunctions -match 'COMMON_QUEST_HANDLER'
    'Uska handler uses common quest handler' = $content.NpcFunctions -match 'KLAPEDA_USKA[\s\S]*COMMON_QUEST_HANDLER'
    'East manager handler uses common quest handler' = $content.NpcFunctions -match 'SIAUL_EAST_MANAGER[\s\S]*COMMON_QUEST_HANDLER'
    'Klaipeda road handoff cleanup exists' = $content.QuestComponent -match 'CompletePapayaKlaipedaHandoffRoadQuests'
    'Quest-class anchors are not spawned as fallback NPCs' = $content.QuestComponent -match 'IsStaticQuestAnchorNpcRole' -and $content.QuestComponent -match 'string\.Equals\(dialogName,\s*questData\.ClassName'
    'Unresolved private encounter anchors do not spawn on player position' = $content.QuestComponent -match 'TryResolveStaticPositionFromLocation\(mapPointGroup,\s*mapClassName' -and $content.QuestComponent -match 'skipped unresolved private encounter anchor' -and $content.QuestComponent -notmatch 'using player-position fallback'
    'SIAUL_EAST_REQUEST6 Vubbe Fighter spawn is not the Search Scout anchor' = $content.PrivateEncounters -match '"questName"\s*:\s*"SIAUL_EAST_REQUEST6"[\s\S]*"f_siauliai_2 2008 185 -389 250"' -and $content.QuestComponent -match 'SIAUL_EAST_REQUEST6[\s\S]*x\s*=\s*2008[\s\S]*z\s*=\s*-389'
    'Captured quest anchors resolve native tracker points' = $content.QuestComponent -match 'GetStaticQuestLocationPoints[\s\S]*TryResolvePapayaCapturedStaticNpcPosition\(mapClassName, anchorName'
    'Klaipeda merchant handoff keeps East Siauliai reclaim possible until Aras' = $content.QuestComponent -match 'ShouldKeepPapayaCapturedFollowUpPossible' -and $content.QuestComponent -match 'EnsureStaticQuestPossible\(nextQuestData,\s*true\)' -and $content.QuestComponent -match 'RepairPrematureEastSiauliaiReclaimAfterKlaipedaPreparation'
    'Possible NPCDIALOG main quests start only from their start NPC' = $content.QuestComponent -match 'quest\.IsPossible\s*&&\s*this\.StaticQuestCanStartFromNpcDialog'
    'Papaya captured chain repair is data-driven' = $content.QuestComponent -match 'PapayaCapturedMainQuestOrder\.Length\s*-\s*1' -and $content.QuestComponent -match 'PapayaCapturedFollowUpShouldStartImmediately'
    'Papaya quest_auto main graph blocks future quests' = $content.QuestComponent -match 'StaticQuestIsBlockedByPapayaAutoMainChain' -and $content.QuestComponent -match 'GetPapayaAutoMainPredecessors'
    'Out-of-sequence quest_auto main quests are suppressed' = $content.QuestComponent -match 'SuppressOutOfSequencePapayaAutoMainQuestState'
    'Client Lua scripts are installed once per client session across zone warps' = $content.ClientScript -match 'TryActivateClientScriptReady' -and $content.ClientScript -match '_readyScriptsBySession' -and $content.ClientScript -match 'SessionKey' -and $content.ClientScript -match 'ReadyAgain' -and $content.ClientScript -match 'OnPlayerReadyInternal[\s\S]*TryActivateClientScriptReady' -and $content.CustomPropertyShopClient -match 'ReadyAgain' -and $content.CustomPropertyShopClient -match 'MarkLuaReadyAndStreamShops'
    'HUD recovery does not rebuild sysmenu with native RemoveChildByType during quest/map transitions' = $content.CharacterStats -match 'SOUL_RESTORE_CORE_HUD' -and $content.CharacterStats -notmatch 'Melia\.Ui\.SysMenu\.Refresh'
    'DX11 client does not receive native job EXP delta packets that crash ON_JOB_EXP_UPDATE' = $content.Send -match 'public static void ZC_JOB_EXP_UP(?:(?!public static void ZC_ADDON_MSG)[\s\S])*Versions\.Protocol\s*>\s*500(?:(?!public static void ZC_ADDON_MSG)[\s\S])*return;' -and $content.Send -notmatch 'public static void ZC_JOB_EXP_UP(?:(?!public static void ZC_ADDON_MSG)[\s\S])*packet\.PutLong'
    'Native class advancement packet adds the requested same-tree job without forcing barracks disconnect' = $content.PacketHandler -match '\[PacketHandler\(Op\.CZ_REQ_CHANGEJOB\)\]' -and $content.PacketHandler -match 'TryResolveRequestedChangeJobId' -and $content.PacketHandler -match 'character\.Jobs\.AddSilent\(newJob\)' -and $content.PacketHandler -notmatch 'CZ_REQ_CHANGEJOB[\s\S]{0,3200}ZC_MOVE_BARRACK'
    'Native class advancement clears unsafe Scout skill-state buffs before job swap' = $content.PacketHandler -match 'ClearClassChangeUnsafeSkillStateBuffs' -and $content.CharacterJobSkills -match 'IsClassChangeUnsafeSkillStateBuff' -and $content.CharacterJobSkills -match 'DoubleAttack_Buff' -and $content.CharacterJobSkills -match 'FreeStep_Buff'
    'Unsafe Scout skill-state buffs are not restored or saved on login' = $content.ZoneDbCharacter -match 'LoadBuffs[\s\S]*IsClassChangeUnsafeSkillStateBuff' -and $content.ZoneDbInternal -match 'savableBuffs[\s\S]*!Character\.IsClassChangeUnsafeSkillStateBuff\(buff\.Id\)' -and $content.BuffComponent -match 'Remove\(BuffId buffId,\s*bool silently = false\)'
    'Unsafe Scout skill-state buffs are disabled in all active buff databases' = $content.Buffs -match 'DoubleAttack_Buff[^\r\n]*save:\s*false' -and $content.Buffs -match 'FreeStep_Buff[^\r\n]*save:\s*false' -and $content.PackageBuffs -match 'DoubleAttack_Buff[^\r\n]*save:\s*false' -and $content.PackageBuffs -match 'FreeStep_Buff[^\r\n]*save:\s*false' -and $content.Version390044Buffs -match 'DoubleAttack_Buff[^\r\n]*save:\s*false' -and $content.Version390044Buffs -match 'FreeStep_Buff[^\r\n]*save:\s*false'
    'quest_auto SProgress tracks do not regress already-Success objective quests' = $content.QuestComponent -match 'StaticQuestAutoTrackStartStatusMatches[\s\S]*return quest\.Status == QuestStatus\.InProgress;' -and $content.QuestComponent -match 'RepairStaticQuestObjectiveSuccessStatus' -and $content.QuestComponent -match 'StaticQuestAutoTrackWouldRegressQuestStatus'
    'Gele Plateau Imminent Invasion Paladin actor is spawned' = $content.Gele572Npcs -match 'Paladin Master' -and $content.Gele572Npcs -match 'GELE572_MQ_01'
    'Gele Plateau Imminent Invasion completes server-side instead of relying on a client-native generic track' = $content.QuestComponent -match 'RepairPapayaGelePlateauImminentInvasion' -and $content.QuestComponent -match 'TryCompletePapayaGelePlateauImminentInvasion' -and $content.QuestComponent -match 'GELE572_MQ_01_TRACK' -and $content.QuestComponent -match 'client-native track is not reliable'
    'Search Scout Large Kepa uses Papaya Onion_Big objective and map point' = $content.Quests -match 'SIAUL_WEST_MEET_NAGLIS[\s\S]*target:\s*"Onion_Big"[\s\S]*f_siauliai_west -1490 260 -140 100' -and $content.PrivateEncounters -match '"questName"\s*:\s*"SIAUL_WEST_MEET_NAGLIS"[\s\S]*"target"\s*:\s*"Onion_Big"[\s\S]*"f_siauliai_west -1490 260 -140 100"' -and $content.SessionObjects -match 'SIAUL_WEST_MEET_NAGLIS[\s\S]*"f_siauliai_west -1490 260 -140 100"[\s\S]*"Onion_Big"'
    'Search Scout Large Kepa Papaya track is restored without disposable combat actors' = $content.QuestAuto -match '"questName"\s*:\s*"SIAUL_WEST_MEET_NAGLIS"[\s\S]*SIAUL_WEST_MEET_NAGLIS_TRACK' -and $content.QuestComponent -match 'StaticQuestAutoTrackShouldAvoidGenericMonsterActors'
    'TUTO_SKILL_RUN is the Papaya main bridge before Battle Commander' = $content.Quests -match 'className:\s*"TUTO_SKILL_RUN"[\s\S]*questMode:\s*"MAIN"[\s\S]*startNPC:\s*"SIAUL_WEST_NAGLIS2"[\s\S]*requiredQuestName:\s*\[\s*"SIAUL_WEST_MEET_NAGLIS"\s*\]' -and $content.Quests -match 'className:\s*"SIAUL_WEST_SOLDIER3"[\s\S]*requiredQuestName:\s*\[\s*"TUTO_SKILL_RUN"\s*\]' -and $content.QuestComponent -match '1014,\s*8350,\s*1020' -and $content.QuestComponent -notmatch 'ParkStaticQuestFromWestSiauliaiMainFlow\(8350,\s*"TUTO_SKILL_RUN"\)'
    'Early Siauliai quest bosses keep Papaya Large Kepa HP and every Vubbe Fighter type at 1k HP' = $content.WestMobs -match 'MonsterId\.Onion_Big,\s*Properties\([^\r\n]*"MHP",\s*98' -and $content.WestMobs -match 'MonsterId\.Onion_Big_Q1,\s*Properties\([^\r\n]*"MHP",\s*1310' -and $content.EastMobs -match 'MonsterId\.Boss_Goblin_Warrior[\s\S]*"MHP",\s*1000' -and $content.EastMobs -match 'MonsterId\.Boss_Goblin_Warrior_Red[\s\S]*"MHP",\s*1000' -and $content.EastMobs -match 'MonsterId\.GoblinWarrior_Red[\s\S]*"MHP",\s*1000' -and $content.Mob -match 'CloverQuestBalancedVubbeFighterHp\s*=\s*1000' -and $content.Mob -match 'IsCloverQuestBalancedVubbeFighterType' -and $content.Mob -match 'NormalizeCloverMonsterTypeName' -and $content.Mob -match 'goblinwarrior'
    'Server monster, class EXP, and level attribute point rates are 10x' = $content.ExpConf -match '(?m)^\s*exp_rate\s*:\s*1000\s*$' -and $content.ExpConf -match '(?m)^\s*job_exp_rate\s*:\s*1000\s*$' -and $content.ExpConf -match '(?m)^\s*ability_points_per_level\s*:\s*100\s*$' -and $content.ExpConf -match '(?m)^\s*ability_points_per_job_level\s*:\s*100\s*$'
    'Early Siauliai quest boss handoff deduplicates equivalent targets' = $content.QuestComponent -match 'StaticQuestObjectiveMonsterMatches' -and $content.EarlyTracks -match 'QuestCombatTargetMatches' -and $content.EarlyTracks -match 'removed \{3\} duplicate actor'
    'Private encounter objective spawns are owner-aware and deduplicated' = $content.QuestComponent -match 'request\.IsPrivateEncounter' -and $content.QuestComponent -match 'OwnerHandle\s*=\s*this\.Character\.Handle' -and $content.QuestComponent -match 'ActorVisibility\.Individual' -and $content.QuestComponent -match 'removed \{0\} duplicate private encounter monster'
    'Private encounter objective monsters are explicitly sent to the owning client' = $content.QuestComponent -match 'SendStaticQuestObjectiveMonsterIfNeeded' -and $content.QuestComponent -match 'Clover\.StaticQuestObjective\.EnterSent' -and $content.QuestComponent -match 'request\.IsPrivateEncounter[\s\S]*Send\.ZC_ENTER_MONSTER'
    'Private encounter objective monsters wait until map warp is finished before entering client' = $content.QuestComponent -match 'CanSendStaticQuestObjectiveActors' -and $content.QuestComponent -match '!this\.Character\.IsWarping' -and $content.QuestComponent -match 'EnsureStaticQuestObjectiveMonsters[\s\S]*CanSendStaticQuestObjectiveActors' -and $content.QuestComponent -match 'SyncStaticQuestObjectiveMonsterMarkers[\s\S]*CanSendStaticQuestObjectiveActors'
    'Nefritas Foreseen Crisis Paladin actors are spawned' = $content.NefritasNpcs -match 'GELE573_MASTER' -and $content.NefritasNpcs -match 'GELE573_MQ_07_F' -and $content.QuestComponent -match 'GELE573_MASTER[\s\S]*x\s*=\s*871[\s\S]*z\s*=\s*-514' -and $content.QuestComponent -match 'GELE573_MQ_07_F[\s\S]*x\s*=\s*805[\s\S]*z\s*=\s*-450'
    'Nefritas Foreseen Crisis boss track requires a killable Minotaur objective' = $content.Quests -match 'GELE573_MQ_09[\s\S]*objectives:\s*\[\{[^\]]*type:\s*"Kill"[^\]]*target:\s*"boss_Minotaurs"' -and $content.SessionObjects -match 'GELE573_MQ_09[\s\S]*monsterNameGroup:\s*\[\s*"boss_Minotaurs"\s*\]' -and $content.PrivateEncounters -match '"questName"\s*:\s*"GELE573_MQ_09"[\s\S]*"target"\s*:\s*"boss_Minotaurs"[\s\S]*"f_gele_57_3 871 -68 -514 325"'
    'Nefritas Foreseen Crisis handoff stays tracked after NPCDIALOG/system bridge' = $content.QuestAuto -match '"questName"\s*:\s*"GELE573_MQ_09"[\s\S]*"successNextQuestNames"\s*:\s*\[[^\]]*"GELE573_MQ_08"' -and $content.QuestComponent -match 'TrackPapayaMainFollowUpIfVisible' -and $content.QuestComponent -match 'GELE573_MQ_09' -and $content.QuestComponent -match 'GELE573_MQ_08' -and $content.QuestComponent -match 'GELE574_MQ_09'
    'Gele Plateau next main-chain actors are spawned for post-Nefritas flow' = $content.Gele574Npcs -match 'GELE574_ALLGES' -and $content.Gele574Npcs -match 'GELE574_ARUNE_1' -and $content.QuestAuto -match '"questName"\s*:\s*"GELE574_MQ_09"[\s\S]*GELE574_MQ_09_TRACK'
    'Gele Plateau Grown Apart From Hope boss track requires a killable Chapparition objective' = $content.Quests -match 'GELE574_MQ_09[\s\S]*objectives:\s*\[\{[^\]]*type:\s*"Kill"[^\]]*target:\s*"boss_Chapparition"' -and $content.SessionObjects -match 'GELE574_MQ_09[\s\S]*monsterNameGroup:\s*\[\s*"boss_Chapparition"\s*\]' -and $content.PrivateEncounters -match '"questName"\s*:\s*"GELE574_MQ_09"[\s\S]*"target"\s*:\s*"boss_Chapparition"[\s\S]*"f_gele_57_4 1287 -78 1811 350"'
    'Remote objective map points route through current-map warps first' = $content.QuestComponent -match 'MapPointGroupsReferenceCurrentMap' -and $content.QuestComponent -match 'GetStaticQuestRouteFallbackMapPointGroups\(quest, currentMap\)[\s\S]*return routePoints'
    'Tenet B1 Church Underground Passage spawns a private killable Unknocker' = $content.Quests -match 'CHAPLE575_MQ_04[\s\S]*target:\s*"boss_Unknocker"[\s\S]*text:\s*"Defeat Unknocker"' -and $content.PrivateEncounters -match '"questName"\s*:\s*"CHAPLE575_MQ_04"[\s\S]*"target"\s*:\s*"boss_Unknocker"[\s\S]*"d_chapel_57_5 CHAPLE575_MQ_04 130"' -and $content.QuestAuto -match '"questName"\s*:\s*"CHAPLE575_MQ_04"[\s\S]*"track"\s*:\s*"SProgress/EProgress/CHAPLE575_MQ_04_TRACK/4000/m_boss_b"' -and $content.TenetB1Mobs -match 'MonsterId\.Boss_Unknocker[\s\S]*"MHP",\s*2000'
    'Tenet B1 Beyond the Darkness uses stable gate actor and pre-login route into Tenet Church 1F' = $content.TenetB1Npcs -match 'AddNpc\(73,\s*147353[\s\S]*"CHAPLE575_MQ_09"' -and $content.TenetB1Warps -match 'CHAPEL575_CHAPEL576[\s\S]*To\("d_chapel_57_6",\s*746,\s*-251\)' -and $content.QuestComponent -match 'RepairPapayaPreLoginMapState' -and $content.QuestComponent -match 'd_chapel57_5_tp04'
}

foreach ($check in $criticalChecks.GetEnumerator()) {
    if (-not $check.Value) {
        $errors.Add("Critical check failed: $($check.Key)")
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Papaya main quest flow validation passed for $($sequence.Count) captured quest links."
