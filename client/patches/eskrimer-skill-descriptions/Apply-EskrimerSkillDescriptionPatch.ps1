param(
    [string] $ClientPath = "C:\CloverTOS-Local",
    [string] $OutputPatch = "1000007_001001.ipf"
)

$ErrorActionPreference = "Stop"

$buffSeparateClassNamesToRemove = @(
    "Touche_Buff",
    "Touche_max_Buff",
    "Invitation_Buff",
    "Pret_Buff",
    "AdvantGarde_Buff",
    "Ouvert_Buff"
)

$sharedEscrimeurLua = @'
-- shared_escrimeur.lua

function SCR_ESCRIMEUR_BLOCK_USE_SKILL_C(actor, skl, buffName)
    return 0
end

function SCR_ESCRIMEUR_ALLOW_USE_SKILL_C(actor, skl, buffName)
    return 1
end

function SCR_ESCRIMEUR_HAS_TOUCHER_READY_C()
    local handle = session.GetMyHandle()
    if handle == nil then
        return 0
    end

    if info.GetBuff(handle, 7534) ~= nil then
        return 1
    end

    if info.GetBuff(handle, 3325) ~= nil then
        return 1
    end

    if info.GetBuffByName(handle, "Touche_max_Buff") ~= nil then
        return 1
    end

    return 0
end

function SCR_ESCRIMEUR_CHECK_USE_SKILL_C(actor, skl, buffName)
    return SCR_ESCRIMEUR_HAS_TOUCHER_READY_C()
end

function SCR_GET_AdvantGarde_Ratio(skill)
    local value = 10 + skill.Level * 2;

    value = value * SCR_REINFORCEABILITY_TOOLTIP(skill)

    return math.floor(value)
end

function SCR_Get_SkillFactor_Escrimeur_AttaqueEnchainee(skill)
    local pc = GetSkillOwner(skill)
    local value = SCR_Get_SkillFactor_Reinforce_Ability(skill)
    local abil = GetAbility(pc, "Escrimeur106")
    if abil ~= nil and TryGetProp(abil, "ActiveState", 0) == 1 then
        value = value * 0.8
    end

    return math.floor(value)
end

function SCR_GET_Invitation_Ratio(skill)
    local level = TryGetProp(skill, "Level", 0)
    local value = 10 + level
    if value > 20 then
        value = 20
    end

    return value
end

function SCR_GET_AttaqueEnchainee_Ratio(skill)
    local value = 5
    local pc = GetSkillOwner(skill)
    if pc ~= nil then
        value = GET_PVP_TARGET_COUNT(pc, value)
    end
    return value
end

function SCR_GET_SeptEclairs_Ratio(skill)
    local value = 5
    local pc = GetSkillOwner(skill)
    if pc ~= nil then
        value = GET_PVP_TARGET_COUNT(pc, value)
    end
    return value
end

function SCR_GET_GrandFente_Ratio(skill)
    local value = 10
    local pc = GetSkillOwner(skill)
    if pc ~= nil then
        value = GET_PVP_TARGET_COUNT(pc, value)
    end
    return value
end

function SCR_GET_Rafale_Ratio(skill)
    local value = 10
    local pc = GetSkillOwner(skill)
    if pc ~= nil then
        value = GET_PVP_TARGET_COUNT(pc, value)
    end
    return value
end

function SCR_GET_PassataSotto_Ratio(skill)
    local value = 10
    local pc = GetSkillOwner(skill)
    if pc ~= nil then
        value = GET_PVP_TARGET_COUNT(pc, value)
    end
    return value
end
'@

$enTtaque = "Pierce enemies in front. Grants [Toucher] buff to self upon use."
$enSetEclair = "Stabs enemies repeatedly to deal damage. Partially ignores enemy Defense. Grants the [Toucher] buff to self upon use."
$enGrandFente = "Dashes forward to attack. Grants the [Toucher] buff to self upon use."
$enInvitation = "Spins the tip of the sword to deflect incoming attacks, blocking 100% of damage while channeling. Upon completion, grants a [Pret] buff that increases the Final Damage of [Pasata Soto]. The Final Damage bonus cannot exceed 20%."
$enAvantGarde = "Adopt a stance for delivering lethal strikes. Increases Final Damage when an Eskrimer skill deals a Critical Hit."
$enRafale = "Delivers a rapid series of thrusts. This attack has a higher Critical Rate. Grants the [Toucher] buff to self upon use."
$enPasataSoto = "Performs a surprise lunge to deliver a rapid series of thrusts. If the [Pret] buff is active, this skill deals increased Final Damage."

$ptTtaque = "Perfura inimigos a frente. Concede o buff [Toucher] ao usuario ao usar."
$ptSetEclair = "Golpeia inimigos repetidamente para causar dano. Ignora parte da Defesa inimiga. Concede o buff [Toucher] ao usuario ao usar."
$ptGrandFente = "Avanca para atacar. Concede o buff [Toucher] ao usuario ao usar."
$ptInvitation = "Gira a ponta da espada para desviar ataques recebidos, bloqueando 100% do dano enquanto canaliza. Ao concluir, concede [Pret], aumentando o Dano Final de [Pasata Soto]. O bonus de Dano Final nao pode exceder 20%."
$ptAvantGarde = "Assume uma postura para golpes letais. Aumenta o Dano Final quando uma skill de Eskrimer causa Acerto Critico."
$ptRafale = "Executa uma sequencia rapida de estocadas. Este ataque possui Taxa Critica maior. Concede o buff [Toucher] ao usuario ao usar."
$ptPasataSoto = "Executa uma estocada surpresa com uma sequencia rapida de golpes. Se o buff [Pret] estiver ativo, esta skill causa Dano Final aumentado."

$enTtaqueInfo = "Skill Factor #{SkillFactor}#% x 4{nl}Targets 5"
$enSetEclairInfo = "Skill Factor #{SkillFactor}#% x 7{nl}Defence Ignored 15%{nl}Targets 5"
$enGrandFenteInfo = "Skill Factor #{SkillFactor}#% x 5{nl}{nl}Targets 10"
$enInvitationInfo = "Blocks 100% of incoming attacks while channeling.{nl}Pasata Soto Final Damage Maximum #{CaptionRatio}#% increase{nl}Maximum Duration 2 seconds"
$enAvantGardeInfo = "Final Attack Damage +#{CaptionRatio}#%{nl}Duration: 30 minutes"
$enRafaleInfo = "Skill Factor #{SkillFactor}#% x 4{nl}Critical Rate x2 Applied{nl}Targets 10"
$enPasataSotoInfo = "Skill Factor #{SkillFactor}#% x 10{nl}{nl}Targets 10"

$ptTtaqueInfo = "Fator da skill #{SkillFactor}#% x 4{nl}Alvos 5"
$ptSetEclairInfo = "Fator da skill #{SkillFactor}#% x 7{nl}Defesa ignorada 15%{nl}Alvos 5"
$ptGrandFenteInfo = "Fator da skill #{SkillFactor}#% x 5{nl}{nl}Alvos 10"
$ptInvitationInfo = "Bloqueia 100% dos ataques recebidos enquanto canaliza.{nl}Aumento maximo de Dano Final de Pasata Soto #{CaptionRatio}#%{nl}Duracao maxima 2 segundos"
$ptAvantGardeInfo = "Dano Final de Ataque +#{CaptionRatio}#%{nl}Duracao: 30 minutos"
$ptRafaleInfo = "Fator da skill #{SkillFactor}#% x 4{nl}Taxa Critica x2 aplicada{nl}Alvos 10"
$ptPasataSotoInfo = "Fator da skill #{SkillFactor}#% x 10{nl}{nl}Alvos 10"

$skillFiles = @{
    "English" = @{
        "SKILL_20260227_025996" = $enTtaque
        "SKILL_20260227_025997" = $enTtaqueInfo
        "SKILL_20260227_025999" = $enSetEclair
        "SKILL_20260227_026000" = $enSetEclairInfo
        "SKILL_20260227_026002" = $enGrandFente
        "SKILL_20260227_026003" = $enGrandFenteInfo
        "SKILL_20260227_026005" = $enInvitation
        "SKILL_20260227_026006" = $enInvitationInfo
        "SKILL_20260227_026008" = $enAvantGardeInfo
        "SKILL_20260227_026010" = $enRafale
        "SKILL_20260227_026011" = $enRafaleInfo
        "SKILL_20260227_026013" = $enPasataSoto
        "SKILL_20260227_026014" = $enPasataSotoInfo
        "SKILL_20260227_026016" = "Minimum Critical Rate +1% and Accuracy +2% per stack."
        "SKILL_20260227_026017" = "Minimum Critical Rate +15% and Accuracy +30%. Pasata Soto becomes available."
        "SKILL_20260227_026019" = "Increases Final Damage of Pasata Soto."
        "SKILL_20260227_026020" = "Increases Final Damage when an Eskrimer skill deals a Critical Hit."
        "SKILL_20260227_026022" = "Eskrimer skill damage +20%."
        "SKILL_20260227_026037" = "Reduces the skill's factor by 20%, but grants the user an [Ouvert] buff for 2 seconds upon use.{nl}While in the [Ouvert] state, Eskrimer skill damage increases by 20% when attacking enemies.{nl}Can only be changed in town."
        "SKILL_20260227_026039" = "Allows you to use [Pasata Soto] in place without moving.{nl}Increases SP consumption by 30%.{nl}Can only be changed in towns."
        "SKILL_20260303_026051" = $enTtaqueInfo
        "SKILL_20260303_026052" = $enSetEclairInfo
        "SKILL_20260303_026053" = $enGrandFenteInfo
        "SKILL_20260303_026054" = $enInvitationInfo
        "SKILL_20260303_026055" = $enRafaleInfo
        "SKILL_20260303_026056" = $enPasataSotoInfo
        "SKILL_20260309_026057" = "* Grants 1 stack of the [Toucher] buff when using an Eskrimer attack skill.{nl}* Toucher: Minimum Critical Rate +1% and Accuracy +2% per stack.{nl}* Stacks up to 15 times.{nl}* At maximum stacks, Pasata Soto becomes available for 10 seconds.{nl}* Using Pasata Soto removes Toucher.{nl}* Duration: 60 seconds."
    }
    "Portuguese" = @{
        "SKILL_20260227_025996" = $ptTtaque
        "SKILL_20260227_025997" = $ptTtaqueInfo
        "SKILL_20260227_025999" = $ptSetEclair
        "SKILL_20260227_026000" = $ptSetEclairInfo
        "SKILL_20260227_026002" = $ptGrandFente
        "SKILL_20260227_026003" = $ptGrandFenteInfo
        "SKILL_20260227_026005" = $ptInvitation
        "SKILL_20260227_026006" = $ptInvitationInfo
        "SKILL_20260227_026008" = $ptAvantGardeInfo
        "SKILL_20260227_026010" = $ptRafale
        "SKILL_20260227_026011" = $ptRafaleInfo
        "SKILL_20260227_026013" = $ptPasataSoto
        "SKILL_20260227_026014" = $ptPasataSotoInfo
        "SKILL_20260227_026016" = "Taxa critica minima +1% e Precisao +2% por stack."
        "SKILL_20260227_026017" = "Taxa critica minima +15% e Precisao +30%. Pasata Soto fica disponivel."
        "SKILL_20260227_026019" = "Aumenta o Dano Final de Pasata Soto."
        "SKILL_20260227_026020" = "Aumenta o Dano Final quando uma skill de Eskrimer causa Acerto Critico."
        "SKILL_20260227_026022" = "Dano das skills de Eskrimer +20%."
        "SKILL_20260227_026037" = "Reduz o fator da skill em 20%, mas concede o buff [Ouvert] por 2 segundos ao usar.{nl}Durante [Ouvert], o dano das skills de Eskrimer aumenta em 20% ao atacar inimigos.{nl}So pode ser alterado na cidade."
        "SKILL_20260227_026039" = "Permite usar [Pasata Soto] no lugar, sem se mover.{nl}Aumenta o consumo de SP em 30%.{nl}So pode ser alterado em cidades."
        "SKILL_20260303_026051" = $ptTtaqueInfo
        "SKILL_20260303_026052" = $ptSetEclairInfo
        "SKILL_20260303_026053" = $ptGrandFenteInfo
        "SKILL_20260303_026054" = $ptInvitationInfo
        "SKILL_20260303_026055" = $ptRafaleInfo
        "SKILL_20260303_026056" = $ptPasataSotoInfo
        "SKILL_20260309_026057" = "* Concede 1 stack de [Toucher] ao usar uma skill de ataque de Eskrimer.{nl}* Toucher: Taxa critica minima +1% e Precisao +2% por stack.{nl}* Acumula ate 15 stacks.{nl}* No maximo de stacks, Pasata Soto fica disponivel por 10 segundos.{nl}* Usar Pasata Soto remove Toucher.{nl}* Duracao: 60 segundos."
    }
}

$buffSeparatePath = Join-Path $ClientPath "release\buff\buff_separate.xml"
if (!(Test-Path -LiteralPath $buffSeparatePath)) {
    throw "buff_separate.xml nao encontrado: $buffSeparatePath"
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
            $lines[$i] = [string]::Join("`t", $parts)
        }
    }
    [System.IO.File]::WriteAllLines($skillPath, $lines, [System.Text.UTF8Encoding]::new($false))
}

[xml]$buffSeparate = Get-Content -LiteralPath $buffSeparatePath -Encoding UTF8
foreach ($className in $buffSeparateClassNamesToRemove) {
    $nodes = @($buffSeparate.List.Buff_Separate | Where-Object { $_.ClassName -eq $className })
    foreach ($node in $nodes) {
        [void]$buffSeparate.List.RemoveChild($node)
    }
}

$settings = [System.Xml.XmlWriterSettings]::new()
$settings.Encoding = [System.Text.UTF8Encoding]::new($false)
$settings.Indent = $true
$writer = [System.Xml.XmlWriter]::Create($buffSeparatePath, $settings)
$buffSeparate.Save($writer)
$writer.Close()

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..\..\..")).ProviderPath
$toolPath = Join-Path $repoRoot "server\app\tools\create-ipf-animation-override.py"
$clientPatchDir = Join-Path $ClientPath "patch"
$outputPath = Join-Path $clientPatchDir $OutputPatch
$newVersion = [int](($OutputPatch -split "_")[0])

if (!(Test-Path -LiteralPath $toolPath)) {
    throw "IPF writer not found: $toolPath"
}

if (!(Test-Path -LiteralPath $clientPatchDir)) {
    throw "Client patch folder not found: $clientPatchDir"
}

$env:TOOL_PATH = $toolPath
$env:CLIENT_PATH = $ClientPath
$env:OUTPUT_PATH = $outputPath
$env:SHARED_ESCRIMEUR_LUA = $sharedEscrimeurLua
$env:NEW_VERSION = $newVersion

@'
import importlib.util
import os
from pathlib import Path

tool_path = Path(os.environ["TOOL_PATH"])
client_path = Path(os.environ["CLIENT_PATH"])
output_path = Path(os.environ["OUTPUT_PATH"])

spec = importlib.util.spec_from_file_location("ipf_writer", tool_path)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

entries = []
for language_name in ("English", "Portuguese"):
    language_dir = client_path / "release" / "languageData" / language_name
    if not language_dir.is_dir():
        continue

    file_path = language_dir / "SKILL.tsv"
    if file_path.is_file():
        content = file_path.read_bytes()
        entries.append(("languageData.ipf", f"{language_dir.name}/SKILL.tsv", content))
        entries.append(("language.ipf", f"languageData/{language_dir.name}/SKILL.tsv", content))

buff_separate_path = client_path / "release" / "buff" / "buff_separate.xml"
if buff_separate_path.is_file():
    entries.append(("buff.ipf", "buff/buff_separate.xml", buff_separate_path.read_bytes()))

shared_escrimeur_lua = os.environ["SHARED_ESCRIMEUR_LUA"].replace("\r\n", "\n").encode("utf-8")
entries.append(("shared.ipf", "script/shared_escrimeur.lua", shared_escrimeur_lua))

module.create_ipf(output_path, entries, new_version=int(os.environ["NEW_VERSION"]))
print(f"Eskrimer skill description IPF patch created: {output_path}")
'@ | python -

Write-Host "Eskrimer skill description patch applied to $ClientPath"
