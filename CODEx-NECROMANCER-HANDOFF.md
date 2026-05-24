# Handoff: Necromancer Rework

Data: 2026-05-21

## Contexto

O trabalho atual e o rework do Necromancer no CloverTOS local. O foco do ultimo ajuste foi limpar as descricoes do client, adicionar o atributo visual `Summon Control` no F1 e alinhar parte do funcionamento das invocacoes no servidor.

Client principal:

- `C:\CloverTOS-Local`
- IPF novo gerado: `C:\CloverTOS-Local\patch\9000015_004001.ipf`
- Backup dos `SKILL.tsv`: `\\wsl.localhost\Ubuntu\home\an\CloverTOS\.codex-backups\necromancer-client-20260521-013243`

Servidor:

- Workspace: `\\wsl.localhost\Ubuntu\home\an\CloverTOS`
- Build testado com `dotnet build Melia.sln`
- Servidor reiniciado com `server/app/stop-server.sh` e `server/app/start-server.sh`

## O Que Foi Feito

- Descricoes do Necromancer encurtadas e limpas nos `SKILL.tsv` de English e Portuguese.
- Criado patch de F1 em `client/patches/summon-control-status/status/status.lua`.
- `Summon Control` aparece em azul no F1, abaixo de `Poison Mastery`.
- `Summon Control` aparece apenas para classes de summon:
  - Bokor `2022`
  - Sorcerer `2006`
  - Necromancer `2009`
- Formula do `Summon Control`:
  - usa `INT`, `CON` e `SPR/MNA`;
  - cada atributo tem cap de `5000`;
  - cada atributo contribui ate `33.33%`;
  - curva usa expoente `0.85`, dando retorno bom no inicio e menor perto do cap;
  - `5000 INT + 5000 CON + 5000 SPR = 100%`;
  - cada `1%` de `Summon Control` aumenta `1%` do dano das invocacoes.
- Textos que mencionam `Summon Control` usam a mesma cor azul `{#3399FF}`.
- `Summon: Force Attack` agora tenta trocar alvo imediatamente.
- `Summon: Cancel Attack` e `Summon: Release` foram ajustadas para handlers `Self` e `Ground`, evitando aparecer como nao implementadas.
- Skeletons comuns agora travam `20%` do HP maximo do player.
- Skeleton Mage trava `20%` do SP maximo.
- Buff visual removivel de quantidade de skeletons foi configurado no buff `Disinter_PC_Buff`/`Summoned Skeletons`.
- `Gather Corpse`:
  - cooldown base `20s`;
  - reduz `1s` por skeleton vivo;
  - dano aumenta `15%` por skeleton vivo;
  - uma utilizacao por cooldown.
- Passivas do Necromancer foram alinhadas no `abilitytree` para evitar o problema de nao conseguir upar.
- `Flesh Defense`:
  - cooldown `35s`;
  - escudo escala por nivel;
  - descricao sem skill factor ofensivo;
  - Royal sacrifica 2 skeletons e usa escudo base de `9%` do HP maximo.
- `Until Death`:
  - aumenta dano e attack speed dos skeletons em `3%` por nivel;
  - trava `30%` do SP maximo enquanto skeletons vivos.
- `Martyr`:
  - passiva;
  - tooltip mais parecida com `Deep Resonance`;
  - valor atual usa `#{SkillFactor}#%`, para refletir o nivel atual no quadro de baixo.
- `Raise Skull Mage: Cleric`:
  - nome atualizado;
  - tamanho x2;
  - loop de cura a cada 15s em invocacao aliada aleatoria, curando 20% do Max HP.
- `Create Shoggoth`:
  - remove dependencia funcional de Necronomicon/cartas;
  - aplica `Summon Control`;
  - Enlargement com chance por nivel, tamanho x2, ataque dobrado e duracao menor.
- `Flesh Amalgam`:
  - voltou ao tamanho normal para voltar a aparecer;
  - HP fixado em 50% do HP maximo do summoner;
  - dano com `Summon Control`;
  - ataques podem aplicar Blind e Confusion.

## Arquivos Mais Importantes

- `server/app/src/ZoneServer/Packages/Laima/Skills/Wizards/Necromancer/NecromancerSkillHelper.cs`
- `server/app/src/ZoneServer/Packages/Laima/Skills/Wizards/Necromancer/Necromancer_SummonCommands.cs`
- `server/app/src/ZoneServer/Packages/Laima/Skills/Wizards/Necromancer/Necromancer_GatherCorpse.cs`
- `server/app/src/ZoneServer/Packages/Laima/Skills/Wizards/Necromancer/Necromancer_CreateShoggoth.cs`
- `server/app/src/ZoneServer/Packages/Laima/Skills/Wizards/Necromancer/Necromancer_FleshHoop.cs`
- `server/app/src/ZoneServer/Packages/Laima/Skills/Wizards/Necromancer/Necromancer_Disinter.cs`
- `server/app/src/ZoneServer/Packages/Laima/Skills/Wizards/Necromancer/Necromancer_CorpseTower.cs`
- `server/app/src/ZoneServer/Packages/Laima/Skills/Wizards/Necromancer/Necromancer_RaiseDead.cs`
- `server/app/src/ZoneServer/Packages/Laima/Skills/Wizards/Necromancer/Necromancer_RaiseSkullarcher.cs`
- `server/app/src/ZoneServer/Packages/Laima/Skills/Wizards/Necromancer/Necromancer_RaiseSkullwizard.cs`
- `server/app/src/ZoneServer/Packages/Laima/Buffs/Wizards/Sorcerer/Sorcerer_SorcererBuffs.cs`
- `server/app/src/ZoneServer/Packages/Laima/Buffs/Wizards/Necromancer/Necromancer_FleshHoop_Buff.cs`
- `server/app/ZoneServer.cs`
- `server/app/packages/laima/db/skills.txt`
- `server/app/packages/laima/db/skills_overrides.txt`
- `server/app/packages/laima/db/buffs.txt`
- `server/app/packages/laima/db/abilitytree.txt`
- `server/app/system/db/skills.txt`
- `server/app/system/db/buffs.txt`
- `server/app/system/db/abilitytree.txt`
- `client/patches/summon-control-status/status/status.lua`

## Pendencias / Proximo Passo

Atualizacao em 2026-05-22:

- `dotnet build Melia.sln` passou com 0 erros.
- Servidor local reiniciado com sucesso depois dos ajustes.
- IPF de textos atualizado: `C:\CloverTOS-Local\patch\9000016_004001.ipf`.
- Corpse Parts agora sao consumidos apenas por:
  - `Create Shoggoth`: 100 partes;
  - `Until Death`: 200 partes;
  - `Flesh Amalgam`: 200 partes.
- `Gather Corpse` agora adiciona Corpse Parts via helper centralizado e atualiza a UI.
- A passiva `Necromancer17`/`Disinter: Collect Corpse` agora concede Corpse Parts quando esqueletos matam monstros.
- Skeleton Soldier/Archer/Elite continuam baseados em HP maximo travado, nao em Corpse Parts; Skeleton Mage continua baseado em SP maximo travado.
- `Until Death` agora:
  - tem casting fixo de 5 segundos;
  - consome 200 Corpse Parts;
  - aplica buff visivel `UntilDeath_Buff` por 30 segundos;
  - aumenta dano final e velocidade de ataque dos esqueletos em 3% por nivel;
  - trava 30% do SP maximo enquanto houver esqueletos vivos;
  - usa efeitos verdes/toxicos inspirados na leitura visual do Meteor.
- `Flesh Amalgam` agora tenta spawnar como `PC_Summon`, nomeado `Flesh Amalgam`, com 50% do HP maximo do invocador e custo de 200 Corpse Parts.
- Passivas principais do Necromancer no `abilitytree` foram liberadas por `UNLOCK_BASE_LEVEL`, para voltarem a poder ser upadas.
- Textos do client foram alinhados ao padrao pedido:
  - descricao superior explica a funcao;
  - bloco de valores usa placeholders dinamicos como `#{SkillFactor}#`, `#{CaptionRatio}#`, etc. para refletir nivel atual/proximo no F1.

Status em 2026-05-22:

- `dotnet build Melia.sln` no WSL passou com 0 erros.
- `./stop-server.sh && ./start-server.sh` concluido com sucesso em Release.
- Portas 2000, 7001, 7002, 8080, 9001 e 9002 ficaram OK.
- Serverlist e Barracks ficaram acessiveis pelo Windows.
- API de criacao de conta validada.
- ZoneServer1/ZoneServer2 carregaram scripts.
- A parte de barreira do `Flesh Amalgam` ja esta implementada no servidor:
  - `Character.Combat.cs` tenta redirecionar dano de personagens aliados para o Flesh Amalgam quando ele esta geometricamente entre atacante e alvo;
  - cada redirecionamento consome 1 bloco de `Melia.Necromancer.FleshAmalgam.Blocks`;
  - o Flesh Amalgam recebe o dano no lugar do personagem protegido.

1. Testar no client com ele fechado e aberto novamente, porque o IPF novo precisa ser carregado.
2. Conferir F1 em personagem Necromancer/Sorcerer/Bokor:
   - `Poison Mastery` deve continuar verde quando aplicavel;
   - `Summon Control` deve aparecer azul somente nas classes de summon;
   - percentual deve bater aproximadamente com a formula do servidor.
3. Testar no jogo:
   - Force Attack troca alvo;
   - Cancel Attack para as invocacoes;
   - Release remove invocacoes e devolve HP/SP travado;
   - buff de skeleton count aparece e atualiza;
   - skeletons reduzem HP maximo em 20%;
   - mage reduz SP maximo em 20%.
4. Validar tooltip de Martyr no client:
   - nivel atual deve mostrar `#{SkillFactor}#%` no quadro atual;
   - proximo nivel deve aumentar o valor corretamente.
5. Validar passivas que antes nao upavam.
6. Flesh Amalgam ainda merece teste em jogo da barreira tipo `Deploy Pavise`: o redirecionamento de dano ja esta implementado no servidor, mas precisa ser validado com atacante, alvo aliado e Flesh Amalgam alinhados.

## Observacoes Para Continuar

- Nao sobrescrever alteracoes antigas do usuario; o worktree ja estava bem sujo antes.
- O client sincronizado da Steam apagou patches antigos da pasta `client/patches`, mas alguns ainda existem no `git HEAD`. O patch de F1 novo foi baseado no antigo `poison-mastery-status/status/status.lua`.
- Ao mexer em client text, atualizar tanto `English/SKILL.tsv` quanto `Portuguese/SKILL.tsv` e empacotar no IPF.
- Para reiniciar servidor, o usuario ja autorizou via skill local `restartautomatico`; usar:
  - `bash ./stop-server.sh`
  - `bash ./start-server.sh`
- Sempre avisar se o client precisa ser fechado e aberto. Para mudancas em IPF/status/SKILL, precisa.

## Atualizacao 2026-05-22 18h

- Build validado: `dotnet build Melia.sln` passou com 0 warnings e 0 errors.
- IPF novo de descricoes gerado em `C:\CloverTOS-Local\patch\9000016_004003.ipf`.
- `9000016_004001.ipf` estava travado pelo Windows/client e nao pode ser sobrescrito; por isso foi gerado um arquivo novo.
- `Gather Corpse` agora esta como propriedade `Dark` no DB e tooltip usa `[Dark]` em roxo.
- `Martyr` foi alinhado como passiva:
  - DB principal como `PassiveSkill`;
  - override sem SP/cast ofensivo;
  - tooltip estilo Deep Resonance com `{#005060}Applied Upon learning{/}`;
  - sem `[Magic] [Non]`.
- `Until Death` teve o efeito de meteoro caindo removido; o casting agora usa efeitos verdes/toxicos no chao durante os 5 segundos e aplica o buff depois do cast.
- `Until Death_Buff` tambem toca efeito verde sob Skeleton Soldier, Archer e Mage enquanto o buff esta ativo.
- `Summon: Cancel Attack` agora reseta hate/target e manda as invocacoes pararem imediatamente.
- `Create Shoggoth` agora consulta Corpse Parts pelo maior valor entre contador e slots, evitando falha quando a UI/slot esta com stacks mas o contador esta defasado.
- `Flesh Amalgam` agora tenta spawnar com `className: "CorpseTower"` e handler chamando `Spawn(..., "CorpseTower", ...)`, para bater com os assets existentes do client Steam.
- `Flesh Defense: Royal` agora aplica Flesh Defense no grupo no mesmo mapa com metade do efeito e metade da duracao.
- `Raise Dead`, `Raise Skull Archer` e `Raise Skull Mage` foram alinhados para dano baseado em Magic Attack:
  - Soldier/Warrior: 45% no nivel 1, +5% por nivel, limite 6, overheat 6, cooldown 20s;
  - Archer: 80% do perfil do Soldier, +30% crit, +15% attack speed, precisao maior, limite 6, overheat 6, cooldown 20s;
  - Mage: propriedade Dark, precisao maior que Archer, Magic Shield Lv.4 automatico, limite 3, cooldown 10s, sem overheat.
- `Raise Skull Mage: Cleric`:
  - limita Mage a 1 quando ativo;
  - so pode alternar em cidade;
  - mage cleric fica maior, nao causa dano, e a cada 5s tem chance de curar o jogador ou dar +10% dano final nas invocacoes;
  - tambem tem 1% de chance por tick de remover um buff removivel do invocador.
- Passivas problemáticas tiveram `unlockScript` e/ou `priceTimeScript` liberados:
  - `Necromancer31` `[Arts] Flesh Amalgam: Enhanced Upgrade`;
  - `Necromancer32` `[Arts] Raise Dead: Enhanced Upgrade`;
  - `Necromancer33` `[Arts] Raise Skull Archer: Enhanced Upgrade`;
  - `Necromancer34` `Gather Corpse: Target`;
  - `Necromancer35` `[Arts] Raise Skeleton Soldier: Elite`.
- Descricoes tocadas foram atualizadas no padrao pedido: descricao superior sem valores de nivel atual; valores dinamicos ficam nos blocos de informacao com `#{SkillFactor}#`, `#{CaptionRatio}#`, etc.
