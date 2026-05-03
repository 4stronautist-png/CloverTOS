Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase

$ErrorActionPreference = "Stop"

$clientDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$clientExe = Join-Path $clientDir "Client_tos_x64.exe"
$imagePath = Join-Path $clientDir "assets\tos-clover-loadscreen.png"
if (-not (Test-Path -LiteralPath $imagePath)) {
    $imagePath = "C:\Users\Jean\Pictures\CloverTOS\tos-clover-loadscreen.png"
}
$targetSeconds = 28

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
    $elapsed = ((Get-Date) - $script:startTime).TotalSeconds
    $pct = [Math]::Min(99, [Math]::Floor(($elapsed / $script:targetSeconds) * 100))

    if ($script:started -ne $null) {
        try {
            $script:started.Refresh()
            if ($script:started.HasExited) {
                $script:timer.Stop()
                $script:window.Close()
                return
            }
        }
        catch {
        }

        if ($script:started.MainWindowHandle -ne [IntPtr]::Zero -and $elapsed -ge 8) {
            $pct = [Math]::Max($pct, 80)
        }
    }

    $script:barFill.Width = ($script:barGrid.ActualWidth * $pct) / 100
    $script:percentText.Text = "$pct%"

    if ($elapsed -ge $script:targetSeconds) {
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
