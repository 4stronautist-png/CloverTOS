# CloverTOS - guia rápido para outro PC

Este pacote acompanha o monorepo `CloverTOS` e foi pensado para quem quiser subir o ambiente completo em outro computador.

## Fluxo recomendado

1. No WSL, suba o servidor:

```bash
cd server/scripts
./up.sh
```

2. No Windows, instale a cópia leve do cliente:

```powershell
powershell -ExecutionPolicy Bypass -File .\client\tools\Install-CloverTOS-Local.ps1
```

3. Abra o cliente:

```powershell
powershell -ExecutionPolicy Bypass -File .\client\tools\Start-CloverTOS-Client.ps1
```

## Endpoints padrão

- patch/web: `http://127.0.0.1:18080/toslive/patch/serverlist.xml`
- barracks: `127.0.0.1:2000`

## Observação

O repositório não inclui o payload proprietário completo do Tree of Savior. O instalador reaproveita a instalação oficial já existente na máquina Windows.
