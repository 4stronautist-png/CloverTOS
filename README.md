# CloverTOS

Monorepo do ambiente local do CloverTOS.

Este repositório empacota:

- `server/`: o código-fonte do servidor Clover atual, bootstrap Docker, compose e dump do banco `clover_local`.
- `client/`: instalador leve e launcher do cliente CloverTOS para Windows.

## Estrutura

- `server/app`
  - código-fonte do servidor Clover baseado no estado atual que está rodando localmente
- `server/docker`
  - `Dockerfile`
  - `docker-compose.yml`
  - `entrypoint.sh`
  - `db/init/20-clover_local.sql.gz`
- `server/scripts`
  - `up.sh`
  - `down.sh`
  - `logs.sh`
  - `Manage-CloverTOS.ps1`
- `client/tools`
  - `Install-CloverTOS-Local.ps1`
  - `Start-CloverTOS-Client.ps1`

## Subindo o server no WSL

No WSL:

```bash
cd server/scripts
./up.sh
```

Para parar:

```bash
cd server/scripts
./down.sh
```

Para acompanhar logs:

```bash
cd server/scripts
./logs.sh
```

## Subindo o server pelo PowerShell

No Windows PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\server\scripts\Manage-CloverTOS.ps1 -Action up
```

Para parar:

```powershell
powershell -ExecutionPolicy Bypass -File .\server\scripts\Manage-CloverTOS.ps1 -Action down
```

Para acompanhar logs:

```powershell
powershell -ExecutionPolicy Bypass -File .\server\scripts\Manage-CloverTOS.ps1 -Action logs
```

Para ver containers:

```powershell
powershell -ExecutionPolicy Bypass -File .\server\scripts\Manage-CloverTOS.ps1 -Action ps
```

O compose sobe:

- MariaDB
- BarracksServer
- ZoneServer1
- ZoneServer2
- SocialServer1
- SocialServer2
- WebServer

Portas expostas:

- `18080` -> web/patch/register
- `2000` -> barracks
- `7001`, `7002` -> zone
- `9001`, `9002` -> social

## Instalando o cliente no Windows

Este repositório nao inclui os binários proprietários completos do Tree of Savior. Em vez disso, o instalador reutiliza a instalação oficial já existente no Windows e cria uma cópia local configurada para o CloverTOS.

No Windows PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\client\tools\Install-CloverTOS-Local.ps1
```

Depois, para abrir o jogo:

```powershell
powershell -ExecutionPolicy Bypass -File .\client\tools\Start-CloverTOS-Client.ps1
```

Ou diretamente:

```txt
C:\CloverTOS-Local\release\Start-CloverTOS-Local.bat
```

## Instalacao completa em outro PC

No Windows PowerShell, execute dentro da pasta do CloverTOS:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-CloverTOS-Full.ps1 -StartClient
```

O script verifica Git e Docker Desktop, clona/atualiza o repositorio quando necessario, sobe MariaDB + servidores via Docker Compose, instala o cliente local a partir do Tree of Savior da Steam, cria a conta local padrao `clover / clover123`, e deixa o launcher em:

```txt
C:\CloverTOS-Local\release\Start-CloverTOS-Local.bat
```

Para informar a pasta do Tree of Savior manualmente:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-CloverTOS-Full.ps1 -SteamTosPath "C:\Program Files (x86)\Steam\steamapps\common\TreeOfSavior" -StartClient
```

Tambem e possivel criar conta depois:

```powershell
powershell -ExecutionPolicy Bypass -File .\server\scripts\Create-CloverTOS-Account.ps1 -Username meuuser -Password minhasenha
```

## Observações

- O dump do banco representa o estado atual do `clover_local` no momento do empacotamento.
- Na primeira subida do compose, o banco é restaurado automaticamente.
- Em subidas seguintes, o volume do MariaDB preserva o estado salvo.
