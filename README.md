# CloverTOS

Ambiente local do CloverTOS/Melia para rodar no Ubuntu WSL, com cliente Windows instalado a partir do Tree of Savior da Steam.

Este repositorio empacota:

- `server/app/`: codigo-fonte do servidor Clover/Melia.
- `server/db/clover_local.sql.gz`: dump do banco `clover_local` usado para restaurar o ambiente local.
- `server/scripts/install-wsl.sh`: instalador completo do servidor para Ubuntu WSL.
- `server/scripts/up.sh`, `down.sh`, `logs.sh`: controle do servidor no WSL.
- `server/scripts/Manage-CloverTOS.ps1`: wrapper opcional para controlar o WSL pelo PowerShell.
- `client/tools/Install-CloverTOS-Local.ps1`: copia o cliente da Steam para `C:\CloverTOS-Local` e aplica a config CloverTOS.

O repositorio nao inclui os arquivos proprietarios do cliente Tree of Savior. O cliente e sempre copiado da instalacao Steam local.

## Fluxo do zero

No Ubuntu WSL:

```bash
git clone https://github.com/4stronautist-png/CloverTOS.git
cd CloverTOS
./server/scripts/install-wsl.sh
```

O instalador do WSL faz:

- instala dependencias APT;
- instala .NET SDK 8, se necessario;
- instala e inicia MariaDB;
- cria usuario/banco `melia / melia123` e `clover_local`;
- restaura `server/db/clover_local.sql.gz`;
- repara personagens com slot invalido;
- escreve `server/app/user/conf/database.conf`;
- escreve `server/app/user/db/servers.txt`;
- compila `server/app/Melia.sln`;
- configura portproxy/firewall do Windows quando possivel;
- sobe Barracks, Zone 1/2, Social 1/2 e WebServer;
- cria/verifica a conta padrao `clover / clover123`.

Depois, no Windows PowerShell, dentro da pasta do repo:

```powershell
powershell -ExecutionPolicy Bypass -File .\client\tools\Install-CloverTOS-Local.ps1
```

Se o Tree of Savior estiver fora da pasta padrao:

```powershell
powershell -ExecutionPolicy Bypass -File .\client\tools\Install-CloverTOS-Local.ps1 -SteamTosPath "D:\SteamLibrary\steamapps\common\TreeOfSavior"
```

Para abrir o jogo:

```txt
C:\CloverTOS-Local\release\Start-CloverTOS-Local.bat
```

## Instalador unico pelo PowerShell

Tambem existe um wrapper que chama o instalador WSL e depois instala o cliente:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-CloverTOS-Full.ps1 -StartClient
```

Esse wrapper ainda exige que o Ubuntu WSL ja exista. O servidor continua sendo instalado dentro do Ubuntu WSL, sem Docker.

## Comandos do servidor

No Ubuntu WSL:

```bash
cd CloverTOS
./server/scripts/up.sh
./server/scripts/down.sh
./server/scripts/logs.sh
```

Pelo PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\server\scripts\Manage-CloverTOS.ps1 -Action install
powershell -ExecutionPolicy Bypass -File .\server\scripts\Manage-CloverTOS.ps1 -Action up
powershell -ExecutionPolicy Bypass -File .\server\scripts\Manage-CloverTOS.ps1 -Action down
powershell -ExecutionPolicy Bypass -File .\server\scripts\Manage-CloverTOS.ps1 -Action logs
```

## Portas

- `8080` -> web/patch/register.
- `2000` -> Barracks.
- `7001`, `7002` -> Zone.
- `9001`, `9002` -> Social.

ServerListURL esperado:

```txt
http://127.0.0.1:8080/toslive/patch/serverlist.xml
```

## Conta padrao

```txt
clover / clover123
```

Para criar outra conta:

```powershell
powershell -ExecutionPolicy Bypass -File .\server\scripts\Create-CloverTOS-Account.ps1 -Username meuuser -Password minhasenha
```
