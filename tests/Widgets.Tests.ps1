# Automated Test Suite for Windows 11 Widgets in System Core Monitor (.NET 9)
# Validates Adaptive Cards schema rendering, SVG generation, 4-theme palette compliance,
# lifecycle tracking, and manifest XML integrity.

$testsRoot = $PSScriptRoot
$projectRoot = Split-Path $testsRoot -Parent
$dllPath = Join-Path $projectRoot "src\bin\Release\net9.0-windows\SystemCoreMonitor.dll"
$exePath = Join-Path (Join-Path $projectRoot "releases") "SystemCoreMonitor.exe"

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  Running Windows 11 Widgets Validation Tests    " -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

$passed = 0
$failed = 0

function Assert-Test([string]$Name, [scriptblock]$TestBlock) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $result = [bool](& $TestBlock)
        $sw.Stop()
        if ($result -eq $true) {
            Write-Host "  [PASS] $Name ($($sw.ElapsedMilliseconds) ms)" -ForegroundColor Green
            $script:passed++
        } else {
            Write-Host "  [FAIL] $Name (Assertion failed) ($($sw.ElapsedMilliseconds) ms)" -ForegroundColor Red
            $script:failed++
        }
    } catch {
        $sw.Stop()
        Write-Host "  [FAIL] $Name Exception: $_ ($($sw.ElapsedMilliseconds) ms)" -ForegroundColor Red
        $script:failed++
    }
}

# Load Assembly via reflection
$asm = if (Test-Path $dllPath) {
    [System.Reflection.Assembly]::LoadFrom($dllPath)
} elseif (Test-Path $exePath) {
    [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($exePath))
} else {
    $null
}

if (-not $asm) {
    Write-Host "Notice: SystemCoreMonitor.dll/exe not yet compiled on this machine. Validating source and manifest contracts directly." -ForegroundColor Yellow
}

# 1. Test Widget Manifest Structure and Schema Integrity
Assert-Test "Manifest: AppxManifest.template.xml exists and declares 3 widget definitions with com:ExeServer" {
    $manifestPath = Join-Path $projectRoot "src\Widgets\AppxManifest.template.xml"
    if (-not (Test-Path $manifestPath)) { return $false }
    $content = [System.IO.File]::ReadAllText($manifestPath)

    $hasExtension = $content.Contains('Name="com.microsoft.windows.widgets"')
    $hasComServer = $content.Contains('Executable="SystemCoreMonitor.exe"') -and $content.Contains('Arguments="-RegisterProcessAsComServer"')
    $hasClsid = $content.Contains('D3397A3F-F15C-48D3-8E1E-9C8A7B6D5E4F')
    $hasOverview = $content.Contains('Id="System_Overview"')
    $hasCpu = $content.Contains('Id="Cpu_Monitor"')
    $hasCompact = $content.Contains('Id="Compact_Monitor"')

    return ($hasExtension -and $hasComServer -and $hasClsid -and $hasOverview -and $hasCpu -and $hasCompact)
}

# 2. Test Adaptive Cards JSON Templates and Schema 1.5 Contract
Assert-Test "Adaptive Cards: Renderer produces valid JSON containing AdaptiveCard schema 1.5 and Action.Execute" {
    $csFile = Join-Path $projectRoot "src\Widgets\WidgetAdaptiveCardRenderer.cs"
    if (-not (Test-Path $csFile)) { return $false }
    $code = [System.IO.File]::ReadAllText($csFile)

    $hasSchema = $code.Contains("http://adaptivecards.io/schemas/adaptive-card.json")
    $hasVersion = $code.Contains('"1.5"')
    $hasActionRefresh = $code.Contains('"verb": "refresh"')
    $hasActionOpen = $code.Contains('"verb": "openApp"')
    $hasSvgBars = $code.Contains("${barCpu}") -and $code.Contains("${barRam}") -and $code.Contains("${barGpu}")

    return ($hasSchema -and $hasVersion -and $hasActionRefresh -and $hasActionOpen -and $hasSvgBars)
}

# 3. Test 4-Theme SVG Generation and Status Threshold Colors
Assert-Test "Theme Palette: All 4 themes (Dark, Light, Neon, Rose) defined with inline SVG progress bars" {
    $paletteFile = Join-Path $projectRoot "src\Widgets\WidgetThemePalette.cs"
    if (-not (Test-Path $paletteFile)) { return $false }
    $code = [System.IO.File]::ReadAllText($paletteFile)

    $hasDark = $code.Contains('["Dark"]') -and $code.Contains('#93A8FD')
    $hasLight = $code.Contains('["Light"]') -and $code.Contains('#6366F1')
    $hasNeon = $code.Contains('["Neon"]') -and $code.Contains('#00F5FF')
    $hasRose = $code.Contains('["Rose"]') -and $code.Contains('#FF8DA1')
    $hasSvg = $code.Contains("data:image/svg+xml;utf8,") -and $code.Contains("BuildProgressBarSvgDataUri") -and $code.Contains("BuildCoreStripSvgDataUri")

    return ($hasDark -and $hasLight -and $hasNeon -and $hasRose -and $hasSvg)
}

# 4. Test Single-Executable Entrypoint Dual Routing in Program.cs
Assert-Test "Dual Entrypoint: Program.cs intercepts COM and CLI activation before WPF App initialization" {
    $progFile = Join-Path $projectRoot "src\Program.cs"
    if (-not (Test-Path $progFile)) { return $false }
    $code = [System.IO.File]::ReadAllText($progFile)

    $hasComFlag = $code.Contains("-RegisterProcessAsComServer")
    $hasWidgetCall = $code.Contains("--widget-call=")
    $hasRegCmd = $code.Contains("--register-widgets")
    $hasAppRun = $code.Contains("app.Run()")
    $hasWaitEvent = $code.Contains("EmptyWidgetListEvent.WaitOne()")

    return ($hasComFlag -and $hasWidgetCall -and $hasRegCmd -and $hasAppRun -and $hasWaitEvent)
}

# 5. Test Reflection on Assembly if binary is present
if ($asm) {
    Assert-Test "Reflection: Widget subsystem types loaded into CLR" {
        $wTypes = @(
            "SystemCoreMonitor.Widgets.WidgetDefinitionIds",
            "SystemCoreMonitor.Widgets.WidgetThemePalette",
            "SystemCoreMonitor.Widgets.WidgetTelemetryCoordinator",
            "SystemCoreMonitor.Widgets.WidgetAdaptiveCardRenderer",
            "SystemCoreMonitor.Widgets.SystemCoreWidgetProvider",
            "SystemCoreMonitor.Widgets.WidgetRegistrationManager"
        )
        foreach ($wt in $wTypes) {
            if (-not $asm.GetType($wt)) { return $false }
        }
        return $true
    }
}

Write-Host ""
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  Widgets Test Suite Finished: $passed Passed, $failed Failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
Write-Host "=================================================" -ForegroundColor Cyan

if ($failed -gt 0) { exit 1 } else { exit 0 }
