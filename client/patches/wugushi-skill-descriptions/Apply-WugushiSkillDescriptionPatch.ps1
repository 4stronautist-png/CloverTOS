param(
    [string] $ClientPath = "C:\CloverTOS-Local",
    [string] $OutputPatch = "1000006_001001.ipf"
)

$ErrorActionPreference = "Stop"

$skillFiles = @{
    "English" = @{
        "SKILL_20210809_021702" = "Fire a contagious poison arrow. Poison spreads to nearby enemies."
        "SKILL_20210318_021233" = "Fire a contagious poison arrow. Poison spreads to nearby enemies."
        "SKILL_20221125_023774" = "Skill Factor: #{SkillFactor}#% per 0.5 seconds{nl}{#339999}{ol}[Poison]{/}{/} Duration: #{CaptionRatio2}# seconds{nl}{#006633}Poison Mastery affects spread range{/}"
        "SKILL_20211217_022385" = "Apply bird poison to your weapon, turning it green.{nl}Increases Poison damage."
        "SKILL_20210809_021707" = "Apply bird poison to your weapon, turning it green.{nl}Increases Poison damage."
        "SKILL_20210809_021709" = "Fires a poisonous arrow that deals continuous damage for a long period of time."
        "SKILL_20210318_021240" = "Fires a poisonous arrow that deals continuous damage for a long period of time."
        "SKILL_20221125_023778" = "Skill Factor: #{SkillFactor}#% per 1 second{nl}Duration: 100 seconds"
        "SKILL_20190104_013201" = "Create a poison puddle by throwing a pot filled with poison. Any enemy who comes into contact with the puddle is poisoned. Targets continue to receive damage for a moment after stepping away from the puddle."
        "SKILL_20210318_021235" = "Create a poison puddle by throwing a pot filled with poison. Any enemy who comes into contact with the puddle is poisoned. Targets continue to receive damage for a moment after stepping away from the puddle."
        "SKILL_20221125_023775" = "Skill Factor per 1 second: #{SkillFactor}#%{nl}Target: #{CaptionRatio2}#{nl}Tile Duration: 15 seconds"
        "SKILL_20210809_021705" = "Tutu appears and damages nearby enemies periodically.{nl}Enemies near Tutu have Movement Speed reduced by 35%."
        "SKILL_20221125_023776" = "Skill Factor: #{SkillFactor}#%{nl}Golden Frog Duration: 10 seconds{nl}{#339999}{ol}[Golden Frog: Poison]{/}{/} Duration: 60 seconds{nl}Nearby enemies: Movement Speed -35%"
        "SKILL_20210318_021242" = "Throw a vial of poison to the ground and break it. The caster becomes invisible inside the cloud and gains [Hemotoxic Miasma]. While [Hemotoxic Miasma] is active, Wugushi skills reduce healing on bleeding enemies."
        "SKILL_20221125_023779" = "{#339999}{ol}[Stealth]{/}{/} Duration and movement speed scale with Poison Mastery.{nl}Any damage you deal or any skill use removes Stealth.{nl}{#339999}{ol}[Hemotoxic Miasma]{/}{/} Buff Duration: 5 seconds{nl}Wugushi skills that hit bleeding enemies inflict 60% healing reduction for 8 seconds."
        "SKILL_20210809_021710" = "Compresses active poison on enemies in range. Remaining poison duration is halved and poison ticks are applied twice as often.{nl}{#006633}Poison Mastery affects range{/}"
        "SKILL_20210318_021243" = "Compresses active poison on enemies in range. Remaining poison duration is halved and poison ticks are applied twice as often.{nl}{#006633}Poison Mastery affects range{/}"
        "SKILL_20180629_011823" = "Compresses active poison on enemies in range. Remaining poison duration is halved and poison ticks are applied twice as often."
        "SKILL_20221125_023780" = "Poison duration -50%{nl}Poison ticks applied twice as often{nl}{#006633}Poison Mastery affects range{/}"
        "SKILL_20210809_021980" = "* Critical hit does not occur in Wugushi skills{nl}* Can not evade or block Wugushi skill"
        "SKILL_20180629_011999" = "* Reduces the Poison property resistance of enemies within a range of 150 by 10% per attribute level when [Zhendu] is used{nl}* Increases SP consumption by 50%"
        "SKILL_20211217_022386" = "[Zhendu]{nl}- Duration: 5 minutes{nl}- Poison Damage +#{CaptionRatio2}#%{nl}Consumes Poison Pot Poison x#{SpendPoison}#"
        "SKILL_20221125_023777" = "[Zhendu]{nl}- Duration: 5 minutes{nl}- Poison Damage +#{CaptionRatio2}#%{nl}"
    }
    "Portuguese" = @{
        "SKILL_20210809_021702" = "Dispara uma flecha de veneno contagioso. O veneno se espalha para inimigos proximos."
        "SKILL_20210318_021233" = "Dispara uma flecha de veneno contagioso. O veneno se espalha para inimigos proximos."
        "SKILL_20221125_023774" = "Fator da skill: #{SkillFactor}#% por 0,5 segundos{nl}{#339999}{ol}[Veneno]{/}{/} Duracao: #{CaptionRatio2}# segundos{nl}{#006633}Poison Mastery afeta o alcance de propagacao{/}"
        "SKILL_20211217_022385" = "Aplica veneno de passaro em sua arma, deixando-a verde.{nl}Aumenta o dano de Veneno."
        "SKILL_20210809_021707" = "Aplica veneno de passaro em sua arma, deixando-a verde.{nl}Aumenta o dano de Veneno."
        "SKILL_20210809_021709" = "Dispara uma flecha venenosa que causa dano continuo por um longo periodo."
        "SKILL_20210318_021240" = "Dispara uma flecha venenosa que causa dano continuo por um longo periodo."
        "SKILL_20221125_023778" = "Fator da skill: #{SkillFactor}#% por 1 segundo{nl}Duracao: 100 segundos"
        "SKILL_20190104_013201" = "Cria uma poca de veneno ao arremessar um pote cheio de veneno. Qualquer inimigo que entrar em contato com a poca e envenenado. Alvos continuam recebendo dano por um momento depois de sair da poca."
        "SKILL_20210318_021235" = "Cria uma poca de veneno ao arremessar um pote cheio de veneno. Qualquer inimigo que entrar em contato com a poca e envenenado. Alvos continuam recebendo dano por um momento depois de sair da poca."
        "SKILL_20221125_023775" = "Fator da skill por 1 segundo: #{SkillFactor}#%{nl}Alvos: #{CaptionRatio2}#{nl}Duracao do piso: 15 segundos"
        "SKILL_20210809_021705" = "Tutu aparece e causa dano periodico aos inimigos proximos.{nl}Inimigos perto de Tutu tem Velocidade de Movimento reduzida em 35%."
        "SKILL_20221125_023776" = "Fator da skill: #{SkillFactor}#%{nl}Duracao de Golden Frog: 10 segundos{nl}{#339999}{ol}[Golden Frog: Poison]{/}{/} Duracao: 60 segundos{nl}Inimigos proximos: Velocidade de Movimento -35%"
        "SKILL_20210318_021242" = "Arremessa um frasco de veneno no chao e o quebra. O conjurador fica invisivel dentro da nuvem e recebe [Hemotoxic Miasma]. Enquanto [Hemotoxic Miasma] estiver ativo, habilidades Wugushi reduzem a cura de inimigos sangrando."
        "SKILL_20221125_023779" = "{#339999}{ol}[Furtividade]{/}{/} Duracao e velocidade de movimento escalam com Poison Mastery.{nl}Qualquer dano causado ou uso de habilidade remove a Furtividade.{nl}{#339999}{ol}[Hemotoxic Miasma]{/}{/} Duracao do buff: 5 segundos{nl}Habilidades Wugushi que acertam inimigos sangrando infligem 60% de reducao de cura por 8 segundos."
        "SKILL_20210809_021710" = "Comprime venenos ativos nos inimigos em alcance. A duracao restante e reduzida pela metade e os ticks de veneno sao aplicados duas vezes mais.{nl}{#006633}Poison Mastery afeta o alcance{/}"
        "SKILL_20210318_021243" = "Comprime venenos ativos nos inimigos em alcance. A duracao restante e reduzida pela metade e os ticks de veneno sao aplicados duas vezes mais.{nl}{#006633}Poison Mastery afeta o alcance{/}"
        "SKILL_20180629_011823" = "Comprime venenos ativos nos inimigos em alcance. A duracao restante e reduzida pela metade e os ticks de veneno sao aplicados duas vezes mais."
        "SKILL_20221125_023780" = "Duracao de veneno -50%{nl}Ticks de veneno aplicados duas vezes mais{nl}{#006633}Poison Mastery afeta o alcance{/}"
        "SKILL_20210809_021980" = "* Wugushi skills nao causam acerto critico{nl}* Wugushi skills nao podem ser esquivadas ou bloqueadas"
        "SKILL_20180629_011999" = "* Reduz em 10% por nivel de atributo a resistencia a Veneno dos inimigos em alcance de 150 ao usar [Zhendu]{nl}* Aumenta o consumo de SP em 50%"
        "SKILL_20211217_022386" = "[Zhendu]{nl}- Duracao: 5 minutos{nl}- Dano de Veneno +#{CaptionRatio2}#%{nl}Consome Poison Pot Poison x#{SpendPoison}#"
        "SKILL_20221125_023777" = "[Zhendu]{nl}- Duracao: 5 minutos{nl}- Dano de Veneno +#{CaptionRatio2}#%{nl}"
    }
}

$etcFiles = @{
    "English" = @{
        "ETC_20200129_044758" = " "
    }
    "Portuguese" = @{
        "ETC_20200129_044758" = " "
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

foreach ($language in $etcFiles.Keys) {
    $etcPath = Join-Path $ClientPath "release\languageData\$language\ETC.tsv"
    if (!(Test-Path -LiteralPath $etcPath)) {
        throw "ETC.tsv nao encontrado: $etcPath"
    }

    $updates = $etcFiles[$language]
    $lines = [System.IO.File]::ReadAllLines($etcPath, [System.Text.Encoding]::UTF8)
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $parts = $lines[$i] -split "`t", -1
        if ($parts.Length -gt 1 -and $updates.ContainsKey($parts[0])) {
            $parts[1] = $updates[$parts[0]]
            $lines[$i] = [string]::Join("`t", $parts)
        }
    }
    [System.IO.File]::WriteAllLines($etcPath, $lines, [System.Text.UTF8Encoding]::new($false))
}

[xml]$buffSeparate = Get-Content -LiteralPath $buffSeparatePath -Encoding UTF8
$hemotoxicSeparate = $buffSeparate.List.Buff_Separate | Where-Object { $_.ClassName -eq "Hemotoxic_Miasma_Buff" } | Select-Object -First 1
if ($null -eq $hemotoxicSeparate) {
    $node = $buffSeparate.CreateElement("Buff_Separate")
    $node.SetAttribute("ClassName", "Hemotoxic_Miasma_Buff")
    $node.SetAttribute("Name", "Hemotoxic Miasma")
    [void]$buffSeparate.List.AppendChild($node)
    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Encoding = [System.Text.UTF8Encoding]::new($false)
    $settings.Indent = $true
    $writer = [System.Xml.XmlWriter]::Create($buffSeparatePath, $settings)
    $buffSeparate.Save($writer)
    $writer.Close()
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..\..\..")).ProviderPath
$toolPath = Join-Path $repoRoot "server\app\tools\create-ipf-animation-override.py"
$clientPatchDir = Join-Path $ClientPath "patch"
$outputPath = Join-Path $clientPatchDir $OutputPatch

if (!(Test-Path -LiteralPath $toolPath)) {
    throw "IPF writer not found: $toolPath"
}

if (!(Test-Path -LiteralPath $clientPatchDir)) {
    throw "Client patch folder not found: $clientPatchDir"
}

$env:TOOL_PATH = $toolPath
$env:CLIENT_PATH = $ClientPath
$env:OUTPUT_PATH = $outputPath

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

    for file_name in ("SKILL.tsv", "ETC.tsv"):
        file_path = language_dir / file_name
        if not file_path.is_file():
            continue

        content = file_path.read_bytes()
        entries.append(("languageData.ipf", f"{language_dir.name}/{file_name}", content))
        entries.append(("language.ipf", f"languageData/{language_dir.name}/{file_name}", content))

buff_separate_path = client_path / "release" / "buff" / "buff_separate.xml"
if buff_separate_path.is_file():
    content = buff_separate_path.read_bytes()
    entries.append(("buff.ipf", "buff/buff_separate.xml", content))

module.create_ipf(output_path, entries, new_version=1000006)
print(f"Wugushi skill description IPF patch created: {output_path}")
'@ | python -

Write-Host "Wugushi skill description patch applied to $ClientPath"
