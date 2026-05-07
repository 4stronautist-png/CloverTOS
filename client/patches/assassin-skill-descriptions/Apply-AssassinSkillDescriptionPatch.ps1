param(
    [string] $ClientPath = "C:\CloverTOS-Local"
)

$skillFiles = @{
    "English" = @{
        "SKILL_20190104_013515" = "Evasion: -#{CaptionRatio}#%{nl}Accuracy: -#{CaptionRatio}#%{nl}Smoke Duration: 8 seconds{nl}Debuff Duration: 10 seconds"
        "SKILL_20201030_020269" = "Evasion: -#{CaptionRatio}#%{nl}Accuracy: -#{CaptionRatio}#%{nl}Smoke Duration: 8 seconds{nl}Debuff Duration: 10 seconds"
        "SKILL_20211217_022559" = "Evasion: -#{CaptionRatio}#%{nl}Accuracy: -#{CaptionRatio}#%{nl}Smoke Duration: 8 seconds{nl}Debuff Duration: 10 seconds"
        "SKILL_20191024_017386" = "* Applies [Assassination Target] to one enemy inside [Hallucination Smoke] for 10 seconds{nl}* When using [Behead] on that target, teleport behind the target first, then execute [Behead]{nl}* Assassin skills from the caster deal 10% more damage to the target"
        "SKILL_20200826_020144" = "* Applies [Assassination Target] to one enemy inside [Hallucination Smoke] for 10 seconds{nl}* When using [Behead] on that target, teleport behind the target first, then execute [Behead]{nl}* Assassin skills from the caster deal 10% more damage to the target"
        "SKILL_20210809_022043" = "* Applies [Assassination Target] to one enemy inside [Hallucination Smoke] for 10 seconds{nl}* When using [Behead] on that target, teleport behind the target first, then execute [Behead]{nl}* Assassin skills from the caster deal 10% more damage to the target"
        "SKILL_20211217_022916" = "* Applies [Assassination Target] to one enemy inside [Hallucination Smoke] for 10 seconds{nl}* When using [Behead] on that target, teleport behind the target first, then execute [Behead]{nl}* Assassin skills from the caster deal 10% more damage to the target"
        "SKILL_20220831_023726" = "* Applies [Assassination Target] to one enemy inside [Hallucination Smoke] for 10 seconds{nl}* When using [Behead] on that target, teleport behind the target first, then execute [Behead]{nl}* Assassin skills from the caster deal 10% more damage to the target"
        "SKILL_20200826_020146" = "* Halves [Annihilation] Skill Factor, but doubles total hits{nl}* The attack animation is much faster and does not hold the character in place{nl}* Cannot be active at the same time as [Annihilation: Exit Scene]"
    }
    "Portuguese" = @{
        "SKILL_20190104_013515" = "Esquiva: -#{CaptionRatio}#%{nl}Precisao: -#{CaptionRatio}#%{nl}Duracao da fumaca: 8 segundos{nl}Duracao do debuff: 10 segundos"
        "SKILL_20201030_020269" = "Esquiva: -#{CaptionRatio}#%{nl}Precisao: -#{CaptionRatio}#%{nl}Duracao da fumaca: 8 segundos{nl}Duracao do debuff: 10 segundos"
        "SKILL_20211217_022559" = "Esquiva: -#{CaptionRatio}#%{nl}Precisao: -#{CaptionRatio}#%{nl}Duracao da fumaca: 8 segundos{nl}Duracao do debuff: 10 segundos"
        "SKILL_20191024_017386" = "* Aplica [Assassination Target] em um inimigo dentro de [Hallucination Smoke] por 10 segundos{nl}* Ao usar [Behead] nesse alvo, teleporta para tras do alvo primeiro e entao executa [Behead]{nl}* Skills de Assassin do conjurador causam 10% mais dano ao alvo"
        "SKILL_20200826_020144" = "* Aplica [Assassination Target] em um inimigo dentro de [Hallucination Smoke] por 10 segundos{nl}* Ao usar [Behead] nesse alvo, teleporta para tras do alvo primeiro e entao executa [Behead]{nl}* Skills de Assassin do conjurador causam 10% mais dano ao alvo"
        "SKILL_20210809_022043" = "* Aplica [Assassination Target] em um inimigo dentro de [Hallucination Smoke] por 10 segundos{nl}* Ao usar [Behead] nesse alvo, teleporta para tras do alvo primeiro e entao executa [Behead]{nl}* Skills de Assassin do conjurador causam 10% mais dano ao alvo"
        "SKILL_20211217_022916" = "* Aplica [Assassination Target] em um inimigo dentro de [Hallucination Smoke] por 10 segundos{nl}* Ao usar [Behead] nesse alvo, teleporta para tras do alvo primeiro e entao executa [Behead]{nl}* Skills de Assassin do conjurador causam 10% mais dano ao alvo"
        "SKILL_20220831_023726" = "* Aplica [Assassination Target] em um inimigo dentro de [Hallucination Smoke] por 10 segundos{nl}* Ao usar [Behead] nesse alvo, teleporta para tras do alvo primeiro e entao executa [Behead]{nl}* Skills de Assassin do conjurador causam 10% mais dano ao alvo"
        "SKILL_20200826_020146" = "* Reduz pela metade o Skill Factor de [Annihilation], mas dobra o total de hits{nl}* A animacao de ataque fica muito mais rapida e nao prende o personagem no lugar{nl}* Nao pode ficar ativa ao mesmo tempo que [Annihilation: Exit Scene]"
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
            $lines[$i] = [string]::Join("`t", $parts)
        }
    }
    [System.IO.File]::WriteAllLines($skillPath, $lines, [System.Text.UTF8Encoding]::new($false))
}

Write-Host "Assassin skill description patch applied to $ClientPath"
