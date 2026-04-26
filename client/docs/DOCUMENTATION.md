# CloverTOS Client Documentation

## Conteúdo

- `config/`: exemplos de configuração do cliente
- `release/`: exemplo do launcher Clover
- `tools/`: instalador leve e script para abrir o cliente
- `patches/login-media/`: patch opcional de mídia da tela de login

## Instalação

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Install-CloverTOS-Local.ps1
```

## Execução

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Start-CloverTOS-Client.ps1
```

## Configuração padrão

- server name: `Clover`
- service nation: `GLOBAL`
- web URL: `http://127.0.0.1:8080/toslive/patch/`
- register URL: `http://127.0.0.1:8080/register/index.html`
