using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using SystemCoreMonitor.Core;

namespace SystemCoreMonitor.Widgets
{
    public static class WidgetRegistrationManager
    {
        public static string GetManifestPath(string installDirectory) => Path.Combine(installDirectory, "AppxManifest.xml");
        public static string GetAssetsDirectory(string installDirectory) => Path.Combine(installDirectory, "Assets");

        /// <summary>
        /// Deploys AppxManifest.xml and essential widget visual placeholder assets to the target installation directory.
        /// </summary>
        public static bool DeployManifestAndAssets(string installDirectory)
        {
            try
            {
                if (!Directory.Exists(installDirectory))
                    Directory.CreateDirectory(installDirectory);

                string assetsDir = GetAssetsDirectory(installDirectory);
                if (!Directory.Exists(assetsDir))
                    Directory.CreateDirectory(assetsDir);

                string publicDir = Path.Combine(installDirectory, "Public");
                if (!Directory.Exists(publicDir))
                    Directory.CreateDirectory(publicDir);

                // 1. Write AppxManifest.xml from template
                string targetManifest = GetManifestPath(installDirectory);
                string templateXml = LoadEmbeddedManifestTemplate();
                File.WriteAllText(targetManifest, templateXml);

                // 2. Ensure icon assets exist
                string rootIcon = Path.Combine(installDirectory, "icon.png");
                string widgetIcon = Path.Combine(assetsDir, "WidgetIcon.png");
                string storeLogo = Path.Combine(assetsDir, "StoreLogo.png");
                string square150 = Path.Combine(assetsDir, "Square150x150Logo.png");
                string square44 = Path.Combine(assetsDir, "Square44x44Logo.png");

                if (File.Exists(rootIcon))
                {
                    if (!File.Exists(widgetIcon)) File.Copy(rootIcon, widgetIcon, true);
                    if (!File.Exists(storeLogo)) File.Copy(rootIcon, storeLogo, true);
                    if (!File.Exists(square150)) File.Copy(rootIcon, square150, true);
                    if (!File.Exists(square44)) File.Copy(rootIcon, square44, true);
                }

                // 3. Ensure screenshot placeholders exist
                CreatePlaceholderPngIfMissing(Path.Combine(assetsDir, "ScreenshotOverview.png"), 300, 200);
                CreatePlaceholderPngIfMissing(Path.Combine(assetsDir, "ScreenshotCpu.png"), 300, 200);
                CreatePlaceholderPngIfMissing(Path.Combine(assetsDir, "ScreenshotCompact.png"), 300, 200);

                return true;
            }
            catch (Exception ex)
            {
                CrashLogger.LogException("WidgetRegistrationManager.DeployManifestAndAssets", ex, false);
                return false;
            }
        }

        public static string LoadEmbeddedManifestTemplate()
        {
            // Look for template alongside executable or compile fallback
            string localTemplate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Widgets", "AppxManifest.template.xml");
            if (File.Exists(localTemplate))
            {
                return File.ReadAllText(localTemplate);
            }

            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith("AppxManifest.template.xml", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = asm.GetManifestResourceStream(name);
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream);
                        return reader.ReadToEnd();
                    }
                }
            }

            // Fallback hardcoded manifest
            return """
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                     xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
                     xmlns:uap3="http://schemas.microsoft.com/appx/manifest/uap/windows10/3"
                     xmlns:com="http://schemas.microsoft.com/appx/manifest/com/windows10"
                     xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
                     IgnorableNamespaces="uap uap3 com rescap">
              <Identity Name="SystemCoreMonitor.Widgets" Publisher="CN=AnaCata" Version="3.7.0.0" ProcessorArchitecture="x64" />
              <Properties>
                <DisplayName>System Core Monitor Widgets</DisplayName>
                <PublisherDisplayName>AnaCata</PublisherDisplayName>
                <Logo>Assets\StoreLogo.png</Logo>
              </Properties>
              <Dependencies>
                <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.22000.0" MaxVersionTested="10.0.26100.0" />
              </Dependencies>
              <Capabilities>
                <rescap:Capability Name="runFullTrust" />
              </Capabilities>
              <Applications>
                <Application Id="App" Executable="SystemCoreMonitor.exe" EntryPoint="Windows.FullTrustApplication">
                  <uap:VisualElements DisplayName="System Core Monitor" Description="Widgets" BackgroundColor="transparent"
                                      Square150x150Logo="Assets\Square150x150Logo.png" Square44x44Logo="Assets\Square44x44Logo.png" />
                  <Extensions>
                    <com:Extension Category="windows.comServer">
                      <com:ComServer>
                        <com:ExeServer Executable="SystemCoreMonitor.exe" Arguments="-RegisterProcessAsComServer" DisplayName="System Core Monitor Widget Provider">
                          <com:Class Id="D3397A3F-F15C-48D3-8E1E-9C8A7B6D5E4F" DisplayName="SystemCoreWidgetProvider" />
                        </com:ExeServer>
                      </com:ComServer>
                    </com:Extension>
                    <uap3:Extension Category="windows.appExtension">
                      <uap3:AppExtension Name="com.microsoft.windows.widgets" DisplayName="System Core Monitor Widgets" Id="SystemCoreMonitorWidgetsExtension" PublicFolder="Public">
                        <uap3:Properties>
                          <WidgetProvider>
                            <ProviderIcons><Icon Path="Assets\WidgetIcon.png" /></ProviderIcons>
                            <Activation><CreateInstance ClassId="D3397A3F-F15C-48D3-8E1E-9C8A7B6D5E4F" /></Activation>
                            <Definitions>
                              <Definition Id="System_Overview" DisplayName="System Core Overview" Description="Métricas en vivo de CPU, RAM, GPU, Disco y Red" AllowMultiple="true">
                                <Capabilities><Capability><Size Name="medium" /></Capability><Capability><Size Name="large" /></Capability></Capabilities>
                                <ThemeResources><Icons><Icon Path="Assets\WidgetIcon.png" /></Icons><Screenshots><Screenshot Path="Assets\ScreenshotOverview.png" /></Screenshots></ThemeResources>
                              </Definition>
                              <Definition Id="Cpu_Monitor" DisplayName="System Core CPU" Description="Uso y distribución de núcleos CPU" AllowMultiple="true">
                                <Capabilities><Capability><Size Name="small" /></Capability><Capability><Size Name="medium" /></Capability><Capability><Size Name="large" /></Capability></Capabilities>
                                <ThemeResources><Icons><Icon Path="Assets\WidgetIcon.png" /></Icons><Screenshots><Screenshot Path="Assets\ScreenshotCpu.png" /></Screenshots></ThemeResources>
                              </Definition>
                              <Definition Id="Compact_Monitor" DisplayName="System Core Compact" Description="Monitoreo ultra-compacto de CPU, RAM y GPU" AllowMultiple="true">
                                <Capabilities><Capability><Size Name="small" /></Capability><Capability><Size Name="medium" /></Capability></Capabilities>
                                <ThemeResources><Icons><Icon Path="Assets\WidgetIcon.png" /></Icons><Screenshots><Screenshot Path="Assets\ScreenshotCompact.png" /></Screenshots></ThemeResources>
                              </Definition>
                            </Definitions>
                          </WidgetProvider>
                        </uap3:Properties>
                      </uap3:AppExtension>
                    </uap3:Extension>
                  </Extensions>
                </Application>
              </Applications>
            </Package>
            """;
        }

        public static (bool Success, string Message) RegisterPackage(string installDirectory)
        {
            try
            {
                string manifest = GetManifestPath(installDirectory);
                if (!File.Exists(manifest))
                {
                    DeployManifestAndAssets(installDirectory);
                }

                if (!File.Exists(manifest))
                {
                    return (false, "AppxManifest.xml could not be created.");
                }

                string script = $"Add-AppxPackage -Register \"{manifest}\"";
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return (false, "Failed to launch PowerShell for registration.");

                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit(15000);

                if (proc.ExitCode == 0)
                {
                    return (true, "Widgets registrados exitosamente en Windows 11.");
                }
                else
                {
                    return (false, string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
                }
            }
            catch (Exception ex)
            {
                CrashLogger.LogException("WidgetRegistrationManager.RegisterPackage", ex, false);
                return (false, ex.Message);
            }
        }

        public static (bool Success, string Message) UnregisterPackage()
        {
            try
            {
                string script = "Get-AppxPackage SystemCoreMonitor.Widgets | Remove-AppxPackage";
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(10000);
                return (true, "Widgets desregistrados.");
            }
            catch (Exception ex)
            {
                CrashLogger.LogException("WidgetRegistrationManager.UnregisterPackage", ex, false);
                return (false, ex.Message);
            }
        }

        public static bool IsPackageRegistered()
        {
            try
            {
                string script = "if (Get-AppxPackage SystemCoreMonitor.Widgets) { exit 0 } else { exit 1 }";
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(5000);
                return proc != null && proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public static void OpenWidgetsBoard()
        {
            try
            {
                // Launches Windows 11 Widgets Board (Win + W simulation or ms-launchuri)
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "ms-launchuri:widgetboard",
                    UseShellExecute = true
                });
            }
            catch
            {
                try
                {
                    // Fallback to ShellExecute
                    Process.Start(new ProcessStartInfo("ms-widgets:") { UseShellExecute = true });
                }
                catch { }
            }
        }

        private static void CreatePlaceholderPngIfMissing(string path, int width, int height)
        {
            try
            {
                if (File.Exists(path)) return;
                // Generate minimal 1x1 transparent/colored PNG bytes if missing
                byte[] minimalPng = new byte[]
                {
                    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
                    0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                    0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
                    0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
                    0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49,
                    0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
                };
                File.WriteAllBytes(path, minimalPng);
            }
            catch { }
        }
    }
}
