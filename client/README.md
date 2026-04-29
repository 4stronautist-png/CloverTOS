# CloverTOS Client

Este pacote nao inclui os 50 GB do jogo. Ele reutiliza a instalacao oficial do Tree of Savior já existente no Windows, cria uma cópia local em `C:\CloverTOS-Local` e aplica a configuração do CloverTOS.

## Requisitos

- Tree of Savior instalado pela Steam e aberto pelo menos 1x para instalar as dependências.
- Espaco livre suficiente para uma copia local do jogo.
- Internet durante a instalacao, caso faltem DirectX legado ou Visual C++ Redistributable.

## Como instalar

1. Clique com o botao direito no PowerShell e abra como administrador.
2. Va ate a pasta onde esta o script.
3. Execute:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Install-CloverTOS-Local.ps1
```

Se o Tree of Savior estiver fora da pasta padrao da Steam, informe o caminho:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Install-CloverTOS-Local.ps1 -SteamTosPath "D:\SteamLibrary\steamapps\common\TreeOfSavior"
```

## Como abrir

Depois da instalação, execute:

```txt
C:\CloverTOS-Local\release\Start-CloverTOS-Local.bat
```

## O que o script faz

- Localiza o Tree of Savior instalado pela Steam.
- Instala/verifica os pre-requisitos do Windows:
  - Microsoft Visual C++ Redistributable v14 x64/x86.
- Microsoft DirectX End-User Runtime legado.
- Copia os arquivos para `C:\CloverTOS-Local`.
- Valida se o client Steam esta na revisao compativel `402595`.
- Configura o cliente para `127.0.0.1:8080`.
- Cria `Start-CloverTOS-Local.bat`.
- Desativa ReShade se encontrar DLLs conhecidas como `dxgi.dll`.

Se o script informar outra revisao, como `403892`, nao continue com esse client.
O servidor deste pacote foi validado com `402595`; usar outra revisao causa bugs no Barracks,
como personagem travado na criacao, personagem estranho depois de relogar e botao de entrar ausente.

## Erros comuns de DLL

Se aparecer erro de `XINPUT1_3.DLL`, `MSVCP140.DLL`, `CONCRT140.DLL` ou `VCOMP140.DLL`, rode o instalador novamente em um PowerShell aberto como administrador. Esses arquivos sao componentes do DirectX legado e do Visual C++ Redistributable, nao arquivos especificos do servidor.
