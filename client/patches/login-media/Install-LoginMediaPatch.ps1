param(
    [string] $ClientPath = "C:\CloverTOS-Local",
    [switch] $ForceCloseClient
)

$ErrorActionPreference = "Stop"

function Write-Step($Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Ok($Message) {
    Write-Host "OK  $Message" -ForegroundColor Green
}

$patchRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$assetsRoot = Join-Path $patchRoot "assets"
$videoSource = Join-Path $assetsRoot "login_video.avi"
$defaultLoginMusic = Join-Path $ClientPath "release\bgm\tos_SFA_Deives_Veliava_feat_Romanas(DLC_special_edition).mp3"
$musicSource = if (Test-Path -LiteralPath $defaultLoginMusic) { $defaultLoginMusic } else { Join-Path $assetsRoot "tos_maldra_intro2.mp3" }

if (-not (Test-Path -LiteralPath $videoSource)) {
    throw "Arquivo do video nao encontrado: $videoSource"
}

if (-not (Test-Path -LiteralPath $musicSource)) {
    throw "Arquivo da musica nao encontrado: $musicSource"
}

$clientRoot = (Resolve-Path -LiteralPath $ClientPath -ErrorAction Stop).Path
$releasePath = Join-Path $clientRoot "release"
$clientExe = Join-Path $releasePath "Client_tos_x64.exe"

if (-not (Test-Path -LiteralPath $clientExe)) {
    throw "Nao encontrei Client_tos_x64.exe em '$releasePath'. Use -ClientPath com a pasta raiz do cliente CloverTOS."
}

$runningClients = Get-Process -Name "Client_tos_x64", "Client_tos" -ErrorAction SilentlyContinue
if ($runningClients) {
    if ($ForceCloseClient) {
        Write-Step "Fechando cliente em execucao"
        $runningClients | Stop-Process -Force
    }
    else {
        throw "Feche o jogo antes de aplicar o patch, ou rode com -ForceCloseClient."
    }
}

$backupRoot = Join-Path $releasePath ("custom-backups\login-media-patch-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
$videoDir = Join-Path $releasePath "video"
$bgmDir = Join-Path $releasePath "bgm"

New-Item -ItemType Directory -Force -Path $backupRoot, $videoDir, $bgmDir | Out-Null

$targets = @(
    @{ Source = $videoSource; Target = Join-Path $videoDir "login_video.avi" },
    @{ Source = $videoSource; Target = Join-Path $videoDir "zmei_video.avi" },
    @{ Source = $musicSource; Target = Join-Path $bgmDir "tos_Kevin_TOS_Carol_2017.mp3" },
    @{ Source = $musicSource; Target = Join-Path $bgmDir "Tree_of_Savior.mp3" },
    @{ Source = $musicSource; Target = Join-Path $bgmDir "tos_Tree_of_Savior_Piano.mp3" },
    @{ Source = $musicSource; Target = Join-Path $bgmDir "Tree_of_Savior_Piano.mp3" },
    @{ Source = $musicSource; Target = Join-Path $bgmDir "Orgel_Tree_of_Savior.mp3" },
    @{ Source = $musicSource; Target = Join-Path $bgmDir "tos_SFA_Openup_Po10.mp3" },
    @{ Source = $musicSource; Target = Join-Path $bgmDir "tos_Tree_of_Savior.mp3" }
)

Write-Step "Criando backup dos arquivos atuais"
foreach ($item in $targets) {
    if (Test-Path -LiteralPath $item.Target) {
        $relative = $item.Target.Substring($releasePath.Length).TrimStart("\")
        $backupPath = Join-Path $backupRoot $relative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $backupPath) | Out-Null
        Copy-Item -LiteralPath $item.Target -Destination $backupPath -Force
    }
}
Write-Ok "Backup salvo em $backupRoot"

Write-Step "Aplicando video e musica do login"
foreach ($item in $targets) {
    Copy-Item -LiteralPath $item.Source -Destination $item.Target -Force
}

Write-Ok "Patch de login aplicado"
Write-Host ""
Write-Host "Abra o jogo novamente para testar a tela de login." -ForegroundColor Yellow
