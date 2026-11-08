> **Created:** 2026-11-08
> **Last Updated:** 2026-11-08

# Windows 11 Widgets & Windows App SDK Provider Architecture Reference

## 1. Executive Summary

Windows 11 Widgets (hosted by the Windows Widgets Board / `Widgets.exe` via Windows Web Experience Pack) use an out-of-process provider model where third-party Win32/.NET applications supply **Adaptive Cards JSON templates** and **data payloads** to the OS Widget Host rather than rendering pixels directly.

Primary official references:
- [Implement a widget provider in a C# Windows App (Microsoft Learn)](https://learn.microsoft.com/en-us/windows/apps/develop/widgets/implement-widget-provider-cs)
- [Widget provider package manifest XML format (Microsoft Learn)](https://learn.microsoft.com/en-us/windows/apps/develop/widgets/widget-provider-manifest)
- [Widget provider ActivateApplication protocol (Microsoft Learn)](https://learn.microsoft.com/en-us/windows/apps/develop/widgets/widget-provider-activateapplication-protocol)
- [IWidgetProvider Interface Reference (Microsoft Learn)](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.windows.widgets.providers.iwidgetprovider)

---

## 2. Core Architecture & Provider Lifecycle

### 2.1 Three-Tier Separation
1. **Widget Host (Windows Widgets Board)**: Owns the surface, manages pinned widget instances, controls visibility state (`Activate` / `Deactivate`), and merges Adaptive Card templates with JSON data payloads.
2. **Widget Provider (`IWidgetProvider` Out-of-Process COM Server)**: Implemented by the application executable. Launched on demand by COM (`-RegisterProcessAsComServer` or custom CLI flag declared in `com:ExeServer`) without starting the full interactive GUI when invoked headless.
3. **Adaptive Cards Templates (Schema 1.5 / 1.6)**: Declarative JSON UI supporting `TextBlock`, `ColumnSet`, `Container`, `FactSet`, `Image` (including inline SVG data URIs `data:image/svg+xml;utf8,...` or base64 PNGs for progress bars/gauges), conditional `$when` expressions (`${$host.widgetSize == "small"}`), and `$host.hostTheme` (`"dark"` / `"light"`).

### 2.2 `IWidgetProvider` Methods (`Microsoft.Windows.Widgets.Providers`)
- `CreateWidget(WidgetContext widgetContext)`: Invoked when a user pins a widget from the Widget Picker. Provider records `widgetContext.Id` and `widgetContext.DefinitionId` and pushes an initial `WidgetManager.GetDefault().UpdateWidget(WidgetUpdateRequestOptions)` call.
- `DeleteWidget(string widgetId, string customState)`: Invoked when a user unpins a widget. Provider removes the instance from tracking; if no widgets remain pinned, the headless COM server can exit cleanly.
- `Activate(WidgetContext widgetContext)`: Invoked when the Widgets Board becomes visible or the host wants live updates for `widgetContext.Id`. **Critical for CPU/RAM efficiency**: periodic telemetry polling should ONLY run while at least one widget is in the `isActive = true` state.
- `Deactivate(string widgetId)`: Invoked when the user closes the Widgets Board. Provider marks `isActive = false` and pauses background timers when zero widgets are active.
- `OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs)`: Invoked when the user resizes the widget (`small`, `medium`, `large`). Provider updates the stored size and pushes a refreshed card payload.
- `OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)`: Invoked when the user clicks an `Action.Execute` button on the card.
- **Startup Recovery**: On provider construction, calling `WidgetManager.GetDefault().GetWidgetInfos()` recovers all already-pinned widgets across OS reboots or process restarts.

### 2.3 Single-Executable Headless COM Activation vs. Full WPF UI
- A single `.exe` (`SystemCoreMonitor.exe`) can serve both interactive WPF launch and headless Widget Provider COM activation by defining a custom `[STAThread] Main(string[] args)` entrypoint (`<StartupObject>SystemCoreMonitor.Program</StartupObject>`):
  - When `args` contains `-RegisterProcessAsComServer` (or `--widget-provider`), `Program.Main` **does not instantiate WPF `Application` or `MainWindow`**. Instead, it registers the `IClassFactory` for `WidgetProvider` via `CoRegisterClassObject` and waits on a lightweight wait handle without loading XAML resource dictionaries, tray icons, or UI threads.
  - Conversely, when the user launches `SystemCoreMonitor.exe` normally, `Program.Main` starts the standard WPF `App` (and can optionally register the COM class factory or coexist with the widget host).

---

## 3. Packaging, Registration & Distribution Constraints

### 3.1 Package Identity Requirement
According to Microsoft's official specification:
> *"In order to be displayed in the widgets host, apps that support Windows widgets must register their widget provider with the system. For Win32 apps, only packaged apps are currently supported and widget providers specify their registration information in the app package manifest file."*

Specifically, the Windows Widget Host discovers providers exclusively via AppX/MSIX package extensions (`uap3:AppExtension Name="com.microsoft.windows.widgets"`) paired with a packaged `windows.comServer` (`com:ExeServer`).

### 3.2 Registration Models for Win32 / .NET Desktop Apps
There are two viable registration mechanisms for a Win32 desktop app like `SystemCoreMonitor`:
1. **Loose Manifest / External-Location Registration via `AppxManifest.xml` (Zero-MSIX Overhead inside `SystemCoreMonitor-Setup.exe`)**:
   - The Setup Wizard (`SystemCoreMonitor-Setup.exe`) extracts `AppxManifest.xml` and widget assets (`Assets\Widget\*.png`) alongside `SystemCoreMonitor.exe` in `%LOCALAPPDATA%\Programs\SystemCoreMonitor`, and invokes `Add-AppxPackage -Register "<InstallDir>\AppxManifest.xml"` during installation (and `Remove-AppxPackage` on uninstall).
   - **Trade-offs**: Preserves the **Single Deliverable Policy** (`SystemCoreMonitor-Setup.exe` as the sole release asset) and keeps the binary lightweight. Requires Windows Developer Mode enabled for unsigned loose manifest registration (or a local self-signed certificate installed if Developer Mode is off).
2. **Full MSIX / Sparse Package with Self-Signed Certificate**:
   - Builds an `.msix` or sparse package signed with a generated certificate.
   - **Trade-offs**: Higher build tool complexity (`makeappx.exe`, `signtool.exe`), requires administrative elevation to trust the root certificate on target machines if not Microsoft Store signed.

### 3.3 SDK & Binary Size Impact (`Microsoft.WindowsAppSDK` vs. Lightweight WinRT Projection)
- Referencing the full `Microsoft.WindowsAppSDK` NuGet meta-package pulls in WinUI 3, WebView2, and dozens of MBs of transitive assets unless carefully trimmed or isolated, which conflicts with the `< 2 MB` binary health test (`tests/Metrics.Tests.ps1`) and zero-bloat philosophy if bundled carelessly.
- **Architectural options for the COM contract**:
  - **Option A (Minimal CsWinRT / Custom WinRT COM Interop orActivateApplication + Lightweight COM Bridge)**: Keep `SystemCoreMonitor` collectors 100% free of third-party dependencies, and either bind the `Microsoft.Windows.Widgets.Providers` WinRT interfaces via lightweight WinRT / COM projections or isolate the provider activation path so `SystemCoreMonitor.exe` remains lean and passes all 43 existing tests.
  - **Option B (Direct `Microsoft.WindowsAppSDK` Reference)**: Reference `Microsoft.WindowsAppSDK` (or sub-packages) and adjust publish/size limits.
