using System;
using System.Runtime.InteropServices;

namespace SystemCoreMonitor.Widgets
{
    internal static class WidgetComGuids
    {
        public const string IClassFactory = "00000001-0000-0000-C000-000000000046";
        public const string IUnknown = "00000000-0000-0000-C000-000000000046";
        public const string IWidgetProvider = "5F522432-8419-5867-A968-35F924E767E2";
    }

    [ComImport]
    [ComVisible(false)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid(WidgetComGuids.IClassFactory)]
    internal interface IClassFactory
    {
        [PreserveSig]
        int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);

        [PreserveSig]
        int LockServer([MarshalAs(UnmanagedType.Bool)] bool fLock);
    }

    [ComVisible(true)]
    internal class WidgetProviderClassFactory : IClassFactory
    {
        private const int S_OK = 0;
        private const int CLASS_E_NOAGGREGATION = unchecked((int)0x80040110);
        private const int E_NOINTERFACE = unchecked((int)0x80004002);

        public int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject)
        {
            ppvObject = IntPtr.Zero;
            if (pUnkOuter != IntPtr.Zero)
            {
                return CLASS_E_NOAGGREGATION;
            }

            try
            {
                var provider = SystemCoreWidgetProvider.Instance;
                ppvObject = Marshal.GetIUnknownForObject(provider);
                return S_OK;
            }
            catch (Exception ex)
            {
                Core.CrashLogger.LogException("WidgetProviderClassFactory.CreateInstance", ex, false);
                return Marshal.GetHRForException(ex);
            }
        }

        public int LockServer(bool fLock)
        {
            return S_OK;
        }
    }

    internal static class WidgetNativeMethods
    {
        public const uint CLSCTX_LOCAL_SERVER = 0x4;
        public const uint REGCLS_MULTIPLEUSE = 0x1;
        public const uint REGCLS_SUSPENDED = 0x4;

        [DllImport("ole32.dll", ExactSpelling = true)]
        public static extern int CoRegisterClassObject(
            [MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
            [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
            uint dwClsContext,
            uint flags,
            out uint lpdwRegister);

        [DllImport("ole32.dll", ExactSpelling = true)]
        public static extern int CoRevokeClassObject(uint dwRegister);

        [DllImport("ole32.dll", ExactSpelling = true)]
        public static extern int CoResumeClassObjects();
    }
}
