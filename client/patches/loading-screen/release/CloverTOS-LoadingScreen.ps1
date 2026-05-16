Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase

$ErrorActionPreference = "Stop"

$clientDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$clientExe = Join-Path $clientDir "Client_tos_x64.exe"
$clientXml = Join-Path $clientDir "client.xml"
$serverListCache = Join-Path $clientDir "serverlist_recent.xml"
$serverHost = "127.0.0.1"
$webPort = 8080
$serverListUrl = "http://${serverHost}:${webPort}/toslive/patch/serverlist.xml"
$staticConfigUrl = "http://${serverHost}:${webPort}/toslive/patch/"
$registerUrl = "http://${serverHost}:${webPort}/register/index.html"
$imagePath = Join-Path $clientDir "assets\tos-clover-loadscreen.png"
if (-not (Test-Path -LiteralPath $imagePath)) {
    $imagePath = "C:\Users\Jean\Pictures\CloverTOS\tos-clover-loadscreen.png"
}
$fillSeconds = 32
$minVisibleSeconds = 40
$maxWaitSeconds = 75

function Write-CloverClientConfig {
    $content = @"
<?xml version="1.0" encoding="UTF-8"?>
<client>
<General Width="1280" Height="720" WindowMode="1" UseSteamClient="NO" />
<Display Shadow="3" AntiAliasing="0" VSync="0" FullScreenBloom="0" SSAO="0" />
<GameOption ServerListURL="$serverListUrl" StaticConfigURL="$staticConfigUrl" NewAccountURL="$registerUrl" PaymentURL="$staticConfigUrl" LoadingImgURL="$staticConfigUrl" LoadingImgCount="10"/>
<Locale ServiceNation="GLOBAL" Dictionary="YES" DefaultLanguage="English" />
<Security CheatCheck="NO" GameGuard="NO" XignCode="NO" />
</client>
"@
    [System.IO.File]::WriteAllText($clientXml, $content, [System.Text.UTF8Encoding]::new($false))
}

function Test-CloverTcpPort {
    param(
        [string]$HostName,
        [int]$Port
    )

    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $async = $client.BeginConnect($HostName, $Port, $null, $null)
        if (-not $async.AsyncWaitHandle.WaitOne(2000, $false)) {
            throw "timeout"
        }
        $client.EndConnect($async)
    }
    finally {
        $client.Close()
    }
}

function Test-CloverServer {
    $response = Invoke-WebRequest -UseBasicParsing -TimeoutSec 8 -Uri $serverListUrl
    if (-not $response.Content.Contains('Server0_IP="127.0.0.1"')) {
        throw "serverlist nao aponta para 127.0.0.1"
    }

    Test-CloverTcpPort -HostName $serverHost -Port 2000
}

Write-CloverClientConfig
Remove-Item -LiteralPath $serverListCache -Force -ErrorAction SilentlyContinue

try {
    Test-CloverServer
}
catch {
    [System.Windows.MessageBox]::Show("Clover local nao respondeu em 127.0.0.1. Rode o start-server.sh e tente novamente.`n`n$($_.Exception.Message)", "CloverTOS")
    exit 1
}

if (-not (Test-Path -LiteralPath $clientExe)) {
    [System.Windows.MessageBox]::Show("Client_tos_x64.exe nao encontrado em $clientDir", "CloverTOS")
    exit 1
}

if (-not (Test-Path -LiteralPath $imagePath)) {
    [System.Windows.MessageBox]::Show("Imagem de loading nao encontrada em $imagePath", "CloverTOS")
    exit 1
}

$window = New-Object System.Windows.Window
$window.Title = "CloverTOS"
$window.WindowStyle = "None"
$window.ResizeMode = "NoResize"
$window.WindowStartupLocation = "CenterScreen"
$window.Topmost = $true
$window.ShowInTaskbar = $true
$window.Background = [System.Windows.Media.Brushes]::Black
$window.SizeToContent = "WidthAndHeight"

$root = New-Object System.Windows.Controls.Grid
$root.Background = [System.Windows.Media.Brushes]::Black
$root.Margin = "0"
$window.Content = $root

$stack = New-Object System.Windows.Controls.StackPanel
$stack.Orientation = "Vertical"
$stack.HorizontalAlignment = "Center"
$stack.VerticalAlignment = "Center"
$stack.Margin = "0"
$root.Children.Add($stack) | Out-Null

$bitmap = New-Object System.Windows.Media.Imaging.BitmapImage
$bitmap.BeginInit()
$bitmap.UriSource = New-Object System.Uri($imagePath, [System.UriKind]::Absolute)
$bitmap.CacheOption = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
$bitmap.EndInit()

$image = New-Object System.Windows.Controls.Image
$image.Source = $bitmap
$image.Stretch = "Uniform"
$image.MaxWidth = 960
$image.MaxHeight = 540
$stack.Children.Add($image) | Out-Null

$barGrid = New-Object System.Windows.Controls.Grid
$barGrid.Width = 720
$barGrid.Height = 24
$barGrid.Margin = "0,16,0,0"
$stack.Children.Add($barGrid) | Out-Null

$barBack = New-Object System.Windows.Shapes.Rectangle
$barBack.Fill = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.Color]::FromRgb(24, 30, 32))
$barBack.Stroke = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.Color]::FromRgb(108, 195, 154))
$barBack.StrokeThickness = 1
$barGrid.Children.Add($barBack) | Out-Null

$barFill = New-Object System.Windows.Shapes.Rectangle
$barFill.HorizontalAlignment = "Left"
$barFill.Width = 0
$barFill.Fill = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.Color]::FromRgb(74, 214, 144))
$barGrid.Children.Add($barFill) | Out-Null

$percentText = New-Object System.Windows.Controls.TextBlock
$percentText.Text = "0%"
$percentText.Foreground = [System.Windows.Media.Brushes]::White
$percentText.FontFamily = "Segoe UI"
$percentText.FontSize = 14
$percentText.FontWeight = "SemiBold"
$percentText.HorizontalAlignment = "Center"
$percentText.VerticalAlignment = "Center"
$barGrid.Children.Add($percentText) | Out-Null

$started = $null
$startTime = Get-Date
$closing = $false

$window.Add_SourceInitialized({
    try {
        $script:started = Start-Process -FilePath $script:clientExe -ArgumentList "-SERVICE", "GLOBAL" -WorkingDirectory $script:clientDir -PassThru
    }
    catch {
        [System.Windows.MessageBox]::Show($_.Exception.Message, "CloverTOS")
        $script:window.Close()
    }
})

$timer = New-Object System.Windows.Threading.DispatcherTimer
$timer.Interval = [TimeSpan]::FromMilliseconds(120)
$timer.Add_Tick({
    if ($script:closing) {
        return
    }

    $elapsed = ((Get-Date) - $script:startTime).TotalSeconds
    $pct = [Math]::Min(95, [Math]::Floor(($elapsed / $script:fillSeconds) * 95))
    $clientLooksReady = $false

    if ($script:started -ne $null) {
        try {
            $script:started.Refresh()
            if ($script:started.HasExited) {
                $script:timer.Stop()
                $script:window.Close()
                return
            }

            $clientLooksReady = ($script:started.MainWindowHandle -ne [IntPtr]::Zero -and $script:started.Responding)
        }
        catch {
        }

        if ($script:started.MainWindowHandle -ne [IntPtr]::Zero -and $elapsed -ge 8) {
            $pct = [Math]::Max($pct, 80)
        }
    }

    if ($elapsed -ge $script:fillSeconds) {
        $extraWait = [Math]::Min(1, ($elapsed - $script:fillSeconds) / [Math]::Max(1, ($script:minVisibleSeconds - $script:fillSeconds)))
        $pct = [Math]::Max($pct, 95 + [Math]::Floor($extraWait * 4))
    }

    $script:barFill.Width = ($script:barGrid.ActualWidth * $pct) / 100
    $script:percentText.Text = "$pct%"

    $canClose = (($elapsed -ge $script:minVisibleSeconds -and $clientLooksReady) -or $elapsed -ge $script:maxWaitSeconds)
    if ($canClose) {
        $script:closing = $true
        $script:barFill.Width = $script:barGrid.ActualWidth
        $script:percentText.Text = "100%"
        $script:timer.Stop()
        $closeTimer = New-Object System.Windows.Threading.DispatcherTimer
        $closeTimer.Interval = [TimeSpan]::FromMilliseconds(350)
        $closeTimer.Add_Tick({
            $this.Stop()
            $script:window.Close()
        })
        $closeTimer.Start()
    }
})

$window.Add_ContentRendered({ $timer.Start() })
$window.ShowDialog() | Out-Null
