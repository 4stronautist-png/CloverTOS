param(
    [string] $ClientPath = "C:\CloverTOS-Local",
    [string] $OutputPatch = "9000016_004004.ipf"
)

$ErrorActionPreference = "Stop"

$dark = "{#7B3FC6}[Dark]{/}"
$poison = "{#556B2F}[Poison]{/}"
$fire = "{#9E2424}[Fire]{/}"
$ice = "{#2E6FA8}[Ice]{/}"
$non = "{#6F6F6F}[Non]{/}"
$psy = "{#7B3FC6}[Psychokinesis]{/}"
$lightning = "{#B88A00}[Lightning]{/}"
$support = "{#2596be}[Support]{/}"
$summonControl = "{#3399FF}Summon Control{/}"
$applied = "{#3399FF}Applied Upon learning{/}"

function Format-ElementTags {
    param([string] $Text)

    $elementColors = [ordered]@{
        "Dark" = "{#5A2E8A}[Dark]{/}"
        "Poison" = "{#556B2F}[Poison]{/}"
        "Fire" = "{#8F1F1F}[Fire]{/}"
        "Ice" = "{#245F91}[Ice]{/}"
        "Non" = "{#606060}[Non]{/}"
        "Psychokinesis" = "{#5A2E8A}[Psychokinesis]{/}"
        "Lightning" = "{#9A7300}[Lightning]{/}"
    }

    foreach ($name in $elementColors.Keys) {
        $placeholder = "@@CLOVERTOS_ELEMENT_$name@@"
        $token = [regex]::Escape("[$name]")
        $Text = [regex]::Replace($Text, "\{#[0-9A-Fa-f]{6}\}(?:\{ol\})?$token(?:\{/}){1,2}", $placeholder)
        $Text = $Text -replace $token, $placeholder
        $Text = $Text -replace [regex]::Escape($placeholder), $elementColors[$name]
    }

    return $Text
}

$enForceAttackDesc = "Commands your summons to attack the selected target directly.{nl}Without this skill, your summons remain inactive."
$enCancelAttackDesc = "Orders your summons to stop attacking immediately and return to following you."
$enReleaseDesc = "Destroys all of your active summons."
$enGatherDesc = "Fires a $dark projectile at the target. If the target is executed by this skill, you gain Corpse Parts based on enemy size."
$enGatherInfo = "Skill Factor: #{SkillFactor}#% x 4 times{nl}Corpse Parts gained: Small 4 / Medium 6 / Large 9{nl}Cooldown: 20 sec -1 sec per active skeleton"
$enGatherTargetInfo = "* Sets [Gather Corpse] AoE Attack Ratio to 1{nl}* Increases [Gather Corpse] damage by 50%"
$enShoggothDesc = "Summons the demon of gluttony: Shoggoth. Its attributes are based on the summoner, and its race is determined by the first card equipped in the Necronomicon. Shoggoth has a low chance to execute small targets. Damage scales with $summonControl."
$enShoggothInfo = "Shoggoth Attack: +#{CaptionRatio}#%{nl}Shoggoth Defense: +#{CaptionRatio2}#%{nl}Consumes Corpse Parts x100{nl}Duration: 900 seconds"
$enShoggothEnlargeInfo = "* Grants a chance to summon a giant Shoggoth{nl}* Giant Shoggoth deals double damage and has double HP{nl}* Can execute small and medium targets{nl}* Does not work in PvP{nl}* Giant duration: 300 seconds"
$enMartyrDesc = "$applied{nl}Strengthens your summons through necromantic sacrifice. This skill is passive and cannot be cast."
$enMartyrInfo = "Summon Final Damage while below 50% Max HP: +#{SkillFactor}#%"
$enFleshDefenseDesc = "Sacrifices one of your skeletons to grant you a shield based on your Max HP."
$enFleshDefenseInfo = "Shield effect: #{SkillFactor}#%{nl}Duration: 15 seconds{nl}Royal party effect: 50%"
$enFleshDefenseRoyalInfo = "* Reduces shield effect and duration by half{nl}* Applies Flesh Defense to all party members"
$enFleshDesc = "Summons a stationary wall of flesh that protects you and party members from projectiles while they stand behind it. When attacked, it can inflict Blindness or Confusion. Its HP is based on the caster's Max HP."
$enFleshInfo = "Summon Attack: #{CaptionRatio3}#%{nl}Block Count: 5 +1 per skill level{nl}Max HP: 50% of summoner Max HP{nl}Consumes Corpse Parts x200{nl}Duration: 180 seconds{nl}Cooldown: 60 sec"
$enSkeletonSoldierDesc = "Summons a Skeleton Soldier. Its attributes are based on your character, including damage, accuracy, critical chance and critical damage. Each summon consumes part of your HP. When defeated, or when Summon: Release is used, the consumed HP is restored. Damage scales with $summonControl."
$enSkeletonArcherDesc = "Summons a Skeleton Archer. The Skull Archer has high critical chance, high attack speed and accuracy based on the caster's accuracy. Damage scales with $summonControl."
$enSkeletonMageDesc = "Summons a Skeleton Mage. The Skull Mage deals area $dark damage and has high accuracy. Damage scales with $summonControl."
$enSoldierInfo = "Skeleton Attack: #{SkillFactor}#% of Magic Attack{nl}Critical Damage: +40%{nl}Max Soldiers: 6{nl}Overheat: 6{nl}Cooldown: 20 sec"
$enArcherInfo = "Skeleton Attack: #{SkillFactor}#% of Magic Attack{nl}Critical Chance: +30%{nl}Attack Speed: +15%{nl}Accuracy: 200% of caster Accuracy{nl}Max Archers: 6{nl}Overheat: 6{nl}Cooldown: 20 sec"
$enMageInfo = "Skeleton Attack: #{SkillFactor}#% of Magic Attack{nl}Property: $dark{nl}Accuracy: 400% of caster Accuracy{nl}Casts Magic Shield Lv.4{nl}Max Mages: 2{nl}Cooldown: 10 sec"
$enRustyBladeInfo = "* Skeleton Soldier attacks have a chance to inflict $poison{nl}* Each poison tick deals 25% of Raise Dead damage"
$enEliteInfo = "* The first Skeleton Soldier summoned is an Elite version{nl}* Elite Skeleton Soldier deals more damage and has higher Critical Chance"
$enProtectionInfo = "* When Skull Mage is summoned, grants your summons a buff that prevents knockback and knockdown"
$enClericInfo = "* Summons a Skeleton Cleric instead of a Skull Mage{nl}* The Cleric grants buffs to summoned skeletons{nl}* Summon limit: 1"

$ptForceAttackDesc = "Faz com que suas invocacoes ataquem diretamente o alvo selecionado.{nl}Sem o uso dessa habilidade, suas invocacoes permanecerao inativas."
$ptCancelAttackDesc = "Faz com que suas invocacoes parem de atacar imediatamente e voltem a seguir voce."
$ptReleaseDesc = "Destroi todas as suas invocacoes ativas."
$ptGatherDesc = "Dispara um projetil do elemento $dark contra o alvo. Caso o alvo seja executado por essa habilidade, voce recebera Corpse Parts de acordo com o tamanho do inimigo."
$ptGatherInfo = "Fator da skill: #{SkillFactor}#% x 4 times{nl}Corpse Parts obtidos: Pequeno 4 / Medio 6 / Grande 9{nl}Cooldown: 20 s -1 s por esqueleto ativo"
$ptGatherTargetInfo = "* Define o AoE Attack Ratio de [Gather Corpse] como 1{nl}* Aumenta o dano de [Gather Corpse] em 50%"
$ptShoggothDesc = "Invoca o demonio da gula: Shoggoth. Os atributos de Shoggoth sao baseados nos atributos do invocador, enquanto sua raca sera determinada pela primeira carta equipada no Necronomicon. Shoggoth possui baixa chance de executar alvos pequenos. O dano escala com $summonControl."
$ptShoggothInfo = "Ataque do Shoggoth: +#{CaptionRatio}#%{nl}Defesa do Shoggoth: +#{CaptionRatio2}#%{nl}Consome Corpse Parts x100{nl}Duracao: 900 segundos"
$ptShoggothEnlargeInfo = "* Concede chance de invocar um Shoggoth gigante{nl}* O Shoggoth gigante causa o dobro de dano e possui o dobro de HP{nl}* Pode executar alvos pequenos e medios{nl}* Nao funciona em PvP{nl}* Duracao do gigante: 300 segundos"
$ptMartyrDesc = "$applied{nl}Fortalece suas invocacoes atraves de sacrificio necromantico. Esta skill e passiva e nao pode ser conjurada."
$ptMartyrInfo = "Dano Final das invocacoes: +#{SkillFactor}#%"
$ptFleshDefenseDesc = "Sacrifica os restos mortais de um servo invocado para protegê-lo, concedendo um escudo baseado na sua vida máxima."
$ptFleshDefenseInfo = "Efeito do escudo: #{SkillFactor}#%{nl}Duracao: 15 segundos{nl}Efeito em grupo com Royal: 50%"
$ptFleshDefenseRoyalInfo = "Cria uma barreira de corpos ao seu redor. Com [Royal], Flesh Defense tambem e aplicado ao grupo inteiro com metade do efeito e metade da duracao."
$ptFleshDesc = "Invoca um Flesh Amalgam estacionario. O dano escala com $summonControl. Ele pode bloquear ataques para aliados atras dele, e seus ataques $poison podem infligir Cegueira ou Confusao."
$ptFleshInfo = "Ataque da invocacao: #{CaptionRatio3}#%{nl}Bloqueios: 5 +1 por nivel da skill{nl}HP maximo: 50% do HP maximo do invocador{nl}Consome Corpse Parts x200{nl}Duracao: 180 segundos{nl}Cooldown: 60 s"
$ptSkeletonSoldierDesc = "Invoca um Skeleton Soldier. Enquanto estiver vivo, trava parte do seu HP maximo e usa esse valor como vida base. O dano se baseia no Ataque Magico do Necromancer e escala com $summonControl. Com [Collect Corpse] ativo, abates dos esqueletos recuperam Corpse Parts."
$ptSkeletonArcherDesc = "Invoca um Skeleton Archer. Enquanto estiver vivo, trava parte do seu HP maximo. Ele luta a distancia com maior chance critica, velocidade de ataque e precisao. O dano se baseia no Ataque Magico do Necromancer e escala com $summonControl."
$ptSkeletonMageDesc = "Invoca um Skeleton Mage. Enquanto estiver vivo, trava parte do seu SP maximo. Ele causa Dano Magico $dark, luta a distancia, herda alta precisao do invocador e pode conjurar Magic Shield em si mesmo."
$ptSoldierInfo = "Ataque do esqueleto: #{SkillFactor}#% do Ataque Magico{nl}Dano critico: +40%{nl}Maximo de Soldiers: 6{nl}Overheat: 6{nl}Cooldown: 20 s"
$ptArcherInfo = "Ataque do esqueleto: #{SkillFactor}#% do Ataque Magico{nl}Chance critica: +30%{nl}Velocidade de ataque: +15%{nl}Precisao: 200% da Precisao do invocador{nl}Maximo de Archers: 6{nl}Overheat: 6{nl}Cooldown: 20 s"
$ptMageInfo = "Ataque do esqueleto: #{SkillFactor}#% do Ataque Magico{nl}Propriedade: $dark{nl}Precisao: 400% da Precisao do invocador{nl}Conjura Magic Shield Lv.4{nl}Maximo de Mages: 3{nl}Cooldown: 10 s"

$ptMartyrInfo = "Dano final das invocacoes abaixo de 50% do HP maximo: +#{SkillFactor}#%"
$ptFleshDefenseDesc = "Sacrifica um de seus esqueletos para conceder a voce um escudo baseado no seu HP maximo."
$ptFleshDefenseRoyalInfo = "* Reduz o efeito e a duracao do escudo pela metade{nl}* Aplica Flesh Defense a todos os membros do grupo"
$ptFleshDesc = "Invoca uma parede de carne estacionaria que protege voce e membros do grupo contra projeteis quando estiverem atras dela. Ao receber ataques, pode causar Cegueira ou Confusao. O HP da parede e baseado no HP maximo do conjurador."
$ptSkeletonSoldierDesc = "Invoca um esqueleto guerreiro. Seus atributos sao baseados nos atributos do personagem, incluindo dano, precisao, chance critica e dano critico. Cada invocacao consome parte do seu HP. Quando derrotado, ou quando Summon: Release for utilizado, o HP consumido sera restaurado. O dano escala com $summonControl."
$ptSkeletonArcherDesc = "Invoca um esqueleto arqueiro. O Skull Archer possui alta chance critica, alta velocidade de ataque e precisao baseada na precisao do conjurador. O dano escala com $summonControl."
$ptSkeletonMageDesc = "Invoca um esqueleto mago. O Skull Mage causa dano em area do elemento $dark e possui alta taxa de precisao. O dano escala com $summonControl."
$ptMageInfo = "Ataque do esqueleto: #{SkillFactor}#% do Ataque Magico{nl}Propriedade: $dark{nl}Precisao: 400% da Precisao do invocador{nl}Conjura Magic Shield Lv.4{nl}Maximo de Mages: 2{nl}Cooldown: 10 s"
$ptRustyBladeInfo = "* Ataques do Skeleton Soldier tem chance de causar $poison{nl}* Cada tic do veneno causa 25% do dano de Raise Dead"
$ptEliteInfo = "* O primeiro Skeleton Soldier invocado e uma versao de elite{nl}* O Elite causa mais dano e possui chance critica maior"
$ptProtectionInfo = "* Ao invocar um Skull Mage, concede um buff as suas invocacoes que impede knockback e knockdown"
$ptClericInfo = "* Invoca um Skeleton Cleric em vez de um Skull Mage{nl}* O Cleric concede buffs aos esqueletos invocados{nl}* Limite de invocacao: 1"

$skillFiles = @{
    "English" = @{
        "SKILL_20171128_009676" = $enForceAttackDesc
        "SKILL_20171128_009678" = $enCancelAttackDesc
        "SKILL_20171128_009680" = $enReleaseDesc
        "SKILL_20150317_001213" = $enGatherDesc
        "SKILL_20150317_001217" = $enMartyrDesc
        "SKILL_20150317_001219" = $enFleshDefenseDesc
        "SKILL_20150401_003233" = $enFleshDefenseDesc
        "SKILL_20200129_018185" = $enMartyrDesc
        "SKILL_20200129_018186" = $enMartyrInfo
        "SKILL_20200129_018187" = $enFleshDefenseDesc
        "SKILL_20200129_018188" = $enFleshDefenseInfo
        "SKILL_20150729_004942" = "Flesh Defense"
        "SKILL_20180517_011165" = "Flesh Defense"
        "SKILL_20190104_014121" = "Increases the shield amount of [Flesh Defense] per attribute level."
        "SKILL_20191024_016881" = "[Arts] Flesh Defense: Enhanced Upgrade"
        "SKILL_20191024_016882" = "* Increases the shield amount of [Flesh Defense] by 1.25% per attribute level"
        "SKILL_20191024_016872" = $enFleshDefenseRoyalInfo
        "SKILL_20240415_025007" = $enFleshDefenseRoyalInfo
        "SKILL_20151001_005279" = $enShoggothEnlargeInfo
        "SKILL_20161005_006945" = $enRustyBladeInfo
        "SKILL_20200128_017917" = $enGatherTargetInfo
        "SKILL_20000826_020031" = $enEliteInfo
        "SKILL_20200826_020031" = $enEliteInfo
        "SKILL_20190223_015121" = $enProtectionInfo
        "SKILL_20191024_016874" = $enClericInfo
        "SKILL_20190104_012995" = $enGatherInfo
        "SKILL_20200129_018182" = $enGatherInfo
        "SKILL_20200710_019379" = $enGatherInfo

        "SKILL_20150317_001215" = $enShoggothDesc
        "SKILL_20150401_003230" = $enShoggothDesc
        "SKILL_20150414_003735" = $enShoggothDesc
        "SKILL_20150717_004723" = $enShoggothDesc
        "SKILL_20150730_004993" = $enShoggothDesc
        "SKILL_20160224_006204" = $enShoggothDesc
        "SKILL_20170510_009353" = $enShoggothDesc
        "SKILL_20200129_018183" = $enShoggothDesc
        "SKILL_20200129_018184" = $enShoggothInfo
        "SKILL_20200710_019380" = $enShoggothDesc
        "SKILL_20200727_019701" = $enShoggothDesc
        "SKILL_20210115_020536" = $enShoggothDesc
        "SKILL_20210115_020537" = $enShoggothInfo
        "SKILL_20190223_015002" = $enShoggothInfo

        "SKILL_20190223_015006" = $enMartyrInfo
        "SKILL_20210115_020538" = $enFleshDesc
        "SKILL_20210115_020539" = $enFleshInfo

        "SKILL_20180827_012148" = $enFleshInfo
        "SKILL_20190104_013004" = $enFleshInfo
        "SKILL_20190223_015008" = $enFleshDesc
        "SKILL_20190223_015009" = $enFleshInfo
        "SKILL_20190419_015213" = $enFleshInfo
        "SKILL_20200129_018190" = $enFleshDesc
        "SKILL_20200129_018191" = $enFleshInfo
        "SKILL_20200710_019381" = $enFleshDesc
        "SKILL_20200727_019702" = $enFleshDesc

        "SKILL_20190104_013005" = $enSkeletonSoldierDesc
        "SKILL_20190223_015010" = $enSkeletonSoldierDesc
        "SKILL_20190419_015214" = $enSkeletonSoldierDesc
        "SKILL_20200129_018192" = $enSkeletonSoldierDesc
        "SKILL_20200710_019382" = $enSkeletonSoldierDesc
        "SKILL_20200727_019703" = $enSkeletonSoldierDesc
        "SKILL_20210115_020540" = $enSkeletonSoldierDesc
        "SKILL_20190104_013006" = $enSoldierInfo
        "SKILL_20190223_015011" = $enSoldierInfo
        "SKILL_20200129_018193" = $enSoldierInfo
        "SKILL_20210115_020541" = $enSoldierInfo

        "SKILL_20190104_013009" = $enSkeletonArcherDesc
        "SKILL_20190223_015012" = $enSkeletonArcherDesc
        "SKILL_20190419_015215" = $enSkeletonArcherDesc
        "SKILL_20200129_018194" = $enSkeletonArcherDesc
        "SKILL_20200710_019383" = $enSkeletonArcherDesc
        "SKILL_20200727_019704" = $enSkeletonArcherDesc
        "SKILL_20210115_020542" = $enSkeletonArcherDesc
        "SKILL_20190104_013010" = $enArcherInfo
        "SKILL_20190223_015013" = $enArcherInfo
        "SKILL_20200129_018195" = $enArcherInfo
        "SKILL_20210115_020543" = $enArcherInfo

        "SKILL_20190104_013012" = $enSkeletonMageDesc
        "SKILL_20190223_015014" = $enSkeletonMageDesc
        "SKILL_20190223_015015" = $enMageInfo
        "SKILL_20190419_015216" = $enSkeletonMageDesc
        "SKILL_20200129_018196" = $enSkeletonMageDesc
        "SKILL_20200129_018197" = $enMageInfo
        "SKILL_20200710_019384" = $enSkeletonMageDesc
        "SKILL_20200727_019705" = $enSkeletonMageDesc
        "SKILL_20210115_020544" = $enSkeletonMageDesc
        "SKILL_20210115_020545" = $enMageInfo
    }
    "Portuguese" = @{
        "SKILL_20171128_009676" = $ptForceAttackDesc
        "SKILL_20171128_009678" = $ptCancelAttackDesc
        "SKILL_20171128_009680" = $ptReleaseDesc
        "SKILL_20150317_001213" = $ptGatherDesc
        "SKILL_20150317_001217" = $ptMartyrDesc
        "SKILL_20150317_001219" = $ptFleshDefenseDesc
        "SKILL_20150401_003233" = $ptFleshDefenseDesc
        "SKILL_20200129_018185" = $ptMartyrDesc
        "SKILL_20200129_018186" = $ptMartyrInfo
        "SKILL_20200129_018187" = $ptFleshDefenseDesc
        "SKILL_20200129_018188" = $ptFleshDefenseInfo
        "SKILL_20150729_004942" = "Flesh Defense"
        "SKILL_20180517_011165" = "Flesh Defense"
        "SKILL_20190104_014121" = "Aumenta o valor do escudo de [Flesh Defense] por nivel da atributo."
        "SKILL_20191024_016881" = "[Arts] Flesh Defense: Enhanced Upgrade"
        "SKILL_20191024_016882" = "* Aumenta o valor do escudo de [Flesh Defense] em 1.25% por nivel da atributo"
        "SKILL_20191024_016872" = $ptFleshDefenseRoyalInfo
        "SKILL_20240415_025007" = $ptFleshDefenseRoyalInfo
        "SKILL_20151001_005279" = $ptShoggothEnlargeInfo
        "SKILL_20161005_006945" = $ptRustyBladeInfo
        "SKILL_20200128_017917" = $ptGatherTargetInfo
        "SKILL_20200826_020031" = $ptEliteInfo
        "SKILL_20190223_015121" = $ptProtectionInfo
        "SKILL_20191024_016874" = $ptClericInfo
        "SKILL_20190104_012995" = $ptGatherInfo
        "SKILL_20200129_018182" = $ptGatherInfo
        "SKILL_20200710_019379" = $ptGatherInfo

        "SKILL_20150317_001215" = $ptShoggothDesc
        "SKILL_20150401_003230" = $ptShoggothDesc
        "SKILL_20150414_003735" = $ptShoggothDesc
        "SKILL_20150717_004723" = $ptShoggothDesc
        "SKILL_20150730_004993" = $ptShoggothDesc
        "SKILL_20160224_006204" = $ptShoggothDesc
        "SKILL_20170510_009353" = $ptShoggothDesc
        "SKILL_20200129_018183" = $ptShoggothDesc
        "SKILL_20200129_018184" = $ptShoggothInfo
        "SKILL_20200710_019380" = $ptShoggothDesc
        "SKILL_20200727_019701" = $ptShoggothDesc
        "SKILL_20210115_020536" = $ptShoggothDesc
        "SKILL_20210115_020537" = $ptShoggothInfo
        "SKILL_20190223_015002" = $ptShoggothInfo

        "SKILL_20190223_015006" = $ptMartyrInfo
        "SKILL_20210115_020538" = $ptFleshDesc
        "SKILL_20210115_020539" = $ptFleshInfo

        "SKILL_20180827_012148" = $ptFleshInfo
        "SKILL_20190104_013004" = $ptFleshInfo
        "SKILL_20190223_015008" = $ptFleshDesc
        "SKILL_20190223_015009" = $ptFleshInfo
        "SKILL_20190419_015213" = $ptFleshInfo
        "SKILL_20200129_018190" = $ptFleshDesc
        "SKILL_20200129_018191" = $ptFleshInfo
        "SKILL_20200710_019381" = $ptFleshDesc
        "SKILL_20200727_019702" = $ptFleshDesc

        "SKILL_20190104_013005" = $ptSkeletonSoldierDesc
        "SKILL_20190223_015010" = $ptSkeletonSoldierDesc
        "SKILL_20190419_015214" = $ptSkeletonSoldierDesc
        "SKILL_20200129_018192" = $ptSkeletonSoldierDesc
        "SKILL_20200710_019382" = $ptSkeletonSoldierDesc
        "SKILL_20200727_019703" = $ptSkeletonSoldierDesc
        "SKILL_20210115_020540" = $ptSkeletonSoldierDesc
        "SKILL_20190104_013006" = $ptSoldierInfo
        "SKILL_20190223_015011" = $ptSoldierInfo
        "SKILL_20200129_018193" = $ptSoldierInfo
        "SKILL_20210115_020541" = $ptSoldierInfo

        "SKILL_20190104_013009" = $ptSkeletonArcherDesc
        "SKILL_20190223_015012" = $ptSkeletonArcherDesc
        "SKILL_20190419_015215" = $ptSkeletonArcherDesc
        "SKILL_20200129_018194" = $ptSkeletonArcherDesc
        "SKILL_20200710_019383" = $ptSkeletonArcherDesc
        "SKILL_20200727_019704" = $ptSkeletonArcherDesc
        "SKILL_20210115_020542" = $ptSkeletonArcherDesc
        "SKILL_20190104_013010" = $ptArcherInfo
        "SKILL_20190223_015013" = $ptArcherInfo
        "SKILL_20200129_018195" = $ptArcherInfo
        "SKILL_20210115_020543" = $ptArcherInfo

        "SKILL_20190104_013012" = $ptSkeletonMageDesc
        "SKILL_20190223_015014" = $ptSkeletonMageDesc
        "SKILL_20190223_015015" = $ptMageInfo
        "SKILL_20190419_015216" = $ptSkeletonMageDesc
        "SKILL_20200129_018196" = $ptSkeletonMageDesc
        "SKILL_20200129_018197" = $ptMageInfo
        "SKILL_20200710_019384" = $ptSkeletonMageDesc
        "SKILL_20200727_019705" = $ptSkeletonMageDesc
        "SKILL_20210115_020544" = $ptSkeletonMageDesc
        "SKILL_20210115_020545" = $ptMageInfo
    }
}

foreach ($language in $skillFiles.Keys) {
    $skillPath = Join-Path $ClientPath "release\languageData\$language\SKILL.tsv"
    if (!(Test-Path -LiteralPath $skillPath)) {
        throw "SKILL.tsv nao encontrado: $skillPath"
    }

    $updates = $skillFiles[$language]
    $lines = [System.IO.File]::ReadAllLines($skillPath, [System.Text.Encoding]::UTF8)
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $parts = $lines[$i] -split "`t", -1
        if ($parts.Length -gt 1 -and $updates.ContainsKey($parts[0])) {
            $text = $updates[$parts[0]]
            $parts[1] = $text
            if ($parts.Length -gt 4 -and $parts[3] -match "^SKILL_") {
                $parts[4] = $text
            }
        }
        if ($parts.Length -gt 1) {
            for ($partIndex = 1; $partIndex -lt $parts.Length; $partIndex++) {
                $parts[$partIndex] = Format-ElementTags $parts[$partIndex]
                $parts[$partIndex] = [regex]::Replace($parts[$partIndex], "(?<!\})Summon Control(?!\{/\})", $summonControl)
            }
            $lines[$i] = [string]::Join("`t", $parts)
        }
    }
    [System.IO.File]::WriteAllLines($skillPath, $lines, [System.Text.UTF8Encoding]::new($false))
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..\..\..")).ProviderPath
$toolPath = Join-Path $repoRoot "server\app\tools\create-ipf-animation-override.py"
$skillabilityLua = Join-Path $repoRoot "client\patches\pied-piper-skillability-layout\skillability\skillability.lua"
$clientPatchDir = Join-Path $ClientPath "patch"
$outputPath = Join-Path $clientPatchDir $OutputPatch

if (!(Test-Path -LiteralPath $toolPath)) {
    throw "IPF writer not found: $toolPath"
}
if (!(Test-Path -LiteralPath $skillabilityLua)) {
    throw "skillability.lua not found: $skillabilityLua"
}
if (!(Test-Path -LiteralPath $clientPatchDir)) {
    throw "Client patch folder not found: $clientPatchDir"
}

$env:TOOL_PATH = $toolPath
$env:CLIENT_PATH = $ClientPath
$env:OUTPUT_PATH = $outputPath
$env:SKILLABILITY_LUA = $skillabilityLua
$readerPath = Join-Path $repoRoot "server\app\tools\sync-itemmonsters-from-client.py"
$env:IPF_READER_PATH = $readerPath

@'
import importlib.util
import os
from pathlib import Path

tool_path = Path(os.environ["TOOL_PATH"])
client_path = Path(os.environ["CLIENT_PATH"])
output_path = Path(os.environ["OUTPUT_PATH"])
skillability_lua = Path(os.environ["SKILLABILITY_LUA"])
reader_path = Path(os.environ["IPF_READER_PATH"])

spec = importlib.util.spec_from_file_location("ipf_writer", tool_path)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

reader_spec = importlib.util.spec_from_file_location("ipf_reader", reader_path)
reader = importlib.util.module_from_spec(reader_spec)
reader_spec.loader.exec_module(reader)

entries = []
for language_name in ("English", "Portuguese"):
    language_dir = client_path / "release" / "languageData" / language_name
    file_path = language_dir / "SKILL.tsv"
    if file_path.is_file():
        content = file_path.read_bytes()
        entries.append(("languageData.ipf", f"{language_dir.name}/SKILL.tsv", content))
        entries.append(("language.ipf", f"languageData/{language_dir.name}/SKILL.tsv", content))

entries.append(("addon.ipf", "skillability/skillability.lua", skillability_lua.read_bytes()))

toolskill_base = client_path / "patch" / "386975_001001.ipf"
if toolskill_base.is_file():
    try:
        toolskill_lua = reader.extract_ipf_file(toolskill_base, "skill/toolskill_enable.lua")
        marker = b"function SKL_CHECK_BOSS_CARD_C(self, skl)\n"
        if marker in toolskill_lua and b'Necromancer_CreateShoggoth' not in toolskill_lua[toolskill_lua.find(marker):toolskill_lua.find(marker) + 700]:
            toolskill_lua = toolskill_lua.replace(
                marker,
                marker + b"    if skl ~= nil then\n        local sklClassName = TryGetProp(skl, \"ClassName\", \"\");\n        local sklClassID = TryGetProp(skl, \"ClassID\", 0);\n        if sklClassName == \"Necromancer_CreateShoggoth\" or sklClassID == 20902 then\n            return 1;\n        end\n    end\n",
                1,
            )
        old = b'''    if skl.ClassName == "Necromancer_RaiseSkullarcher" or skl.ClassName == "Necromancer_RaiseDead" or skl.ClassName == "Necromancer_RaiseSkullwizard" then
        local mymapname = session.GetMapName();
        local map = GetClass("Map", mymapname);
        if nil == map then
            return 0;
        end
        
        if 'City' == map.MapType then
            return 0;
        end
    end
'''
        new = b'''    if skl.ClassName == "Necromancer_RaiseSkullarcher" or skl.ClassName == "Necromancer_RaiseDead" or skl.ClassName == "Necromancer_RaiseSkullwizard" then
        local mymapname = session.GetMapName();
        local map = GetClass("Map", mymapname);
        if nil == map then
            return 0;
        end
        
        if 'City' == map.MapType then
            return 0;
        end

        return 1;
    end
'''
        if old in toolskill_lua:
            toolskill_lua = toolskill_lua.replace(old, new, 1)
        entries.append(("addon.ipf", "skill/toolskill_enable.lua", toolskill_lua))
    except PermissionError:
        print(f"Skipped locked IPF: {toolskill_base}")

module.create_ipf(output_path, entries, new_version=9000016)
print(f"Necromancer skill description IPF patch created: {output_path}")
'@ | python -

Write-Host "Necromancer skill description patch applied to $ClientPath"
