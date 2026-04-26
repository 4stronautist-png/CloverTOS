# CloverTOS Login Media Patch

Patch portatil para aplicar o video e a musica customizados da tela de login/barracks.

## Como aplicar

1. Feche o jogo.
2. Extraia este pacote em qualquer pasta.
3. Abra PowerShell na pasta extraida.
4. Rode:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-LoginMediaPatch.ps1
```

Se o cliente estiver em outro caminho:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-LoginMediaPatch.ps1 -ClientPath "C:\CloverTOS-Local"
```

## O que o patch troca

- `release\video\login_video.avi`
- `release\video\zmei_video.avi`
- `release\bgm\tos_Kevin_TOS_Carol_2017.mp3`
- `release\bgm\tos_Tree_of_Savior_Piano.mp3`
- `release\bgm\tos_SFA_Openup_Po10.mp3`
- `release\bgm\tos_Tree_of_Savior.mp3`

Antes de substituir, o instalador cria backup em:

```txt
release\custom-backups\login-media-patch-YYYYMMDD-HHMMSS
```
