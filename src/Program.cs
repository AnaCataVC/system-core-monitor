using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SystemCoreMonitor.Core;
using SystemCoreMonitor.Widgets;

namespace SystemCoreMonitor
{
    public static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [STAThread]
        public static int Main(string[] args)
        {
            CrashLogger.Initialize();

            // 1. Check for CLI commands / Widget activation flags
            if (args != null && args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];

                    // Out-of-Process COM server activation from Windows Widget Host
                    if (string.Equals(arg, "-RegisterProcessAsComServer", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(arg, "/RegisterProcessAsComServer", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(arg, "--widget-provider", StringComparison.OrdinalIgnoreCase))
                    {
                        return RunWidgetComServer();
                    }

                    // ActivateApplication protocol: --widget-call=[base64url]
                    if (arg.StartsWith("--widget-call=", StringComparison.OrdinalIgnoreCase))
                    {
                        return RunWidgetCallProtocol(arg.Substring("--widget-call=".Length));
                    }

                    // Self-registration flag invoked by Setup Wizard or Settings
                    if (string.Equals(arg, "--register-widgets", StringComparison.OrdinalIgnoreCase))
                    {
                        string installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                        var (ok, msg) = WidgetRegistrationManager.RegisterPackage(installDir);
                        return ok ? 0 : 1;
                    }

                    if (string.Equals(arg, "--unregister-widgets", StringComparison.OrdinalIgnoreCase))
                    {
                        var (ok, msg) = WidgetRegistrationManager.UnregisterPackage();
                        return ok ? 0 : 1;
                    }
                }
            }

            // 2. Default: Launch standard interactive WPF Application
            var app = new App();
            app.InitializeComponent();
            return app.Run();
        }

        private static int RunWidgetComServer()
        {
            uint cookie = 0;
            try
            {
                var factory = new WidgetProviderClassFactory();
                int hr = WidgetNativeMethods.CoRegisterClassObject(
                    WidgetDefinitionIds.ProviderClsid,
                    factory,
                    WidgetNativeMethods.CLSCTX_LOCAL_SERVER,
                    WidgetNativeMethods.REGCLS_MULTIPLEUSE,
                    out cookie);

                if (hr != 0)
                {
                    return hr;
                }

                // If running attached to a console, wait for Enter; otherwise wait until all widgets are deleted
                if (GetConsoleWindow() != IntPtr.Zero)
                {
                    Console.ReadLine();
                }
                else
                {
                    // Wait on the manual reset event that signals when zero widgets are pinned
                    SystemCoreWidgetProvider.Instance.EmptyWidgetListEvent.WaitOne();
                }

                return 0;
            }
            catch (Exception ex)
            {
                CrashLogger.LogException("Program.RunWidgetComServer", ex, false);
                return -1;
            }
            finally
            {
                if (cookie != 0)
                {
                    try { WidgetNativeMethods.CoRevokeClassObject(cookie); } catch { }
                }
                SystemCoreWidgetProvider.Instance.Dispose();
            }
        }

        private static int RunWidgetCallProtocol(string base64Payload)
        {
            try
            {
                string json = DecodeBase64Url(base64Payload);
                bool handled = SystemCoreWidgetProvider.Instance.HandleWidgetCallJson(json);
                return handled ? 0 : 1;
            }
            catch (Exception ex)
            {
                CrashLogger.LogException("Program.RunWidgetCallProtocol", ex, false);
                return -1;
            }
        }

        private static string DecodeBase64Url(string base64Url)
        {
            string padded = base64Url.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            byte[] bytes = Convert.FromBase64String(padded);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
