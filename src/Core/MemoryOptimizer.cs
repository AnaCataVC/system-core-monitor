using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SystemCoreMonitor.Core
{
    public static class MemoryOptimizer
    {
        [DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hwProc);

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, int min, int max);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public static double OptimizeWorkingSet(out int processesTrimmed)
        {
            processesTrimmed = 0;

            var memBefore = NativeMethods.MEMORYSTATUSEX.Create();
            bool hasBefore = NativeMethods.GlobalMemoryStatusEx(ref memBefore);

            int foregroundPid = 0;
            try
            {
                IntPtr fgHwnd = GetForegroundWindow();
                if (fgHwnd != IntPtr.Zero)
                {
                    GetWindowThreadProcessId(fgHwnd, out uint fgPid);
                    foregroundPid = (int)fgPid;
                }
            }
            catch { }

            int selfPid = 0;
            try { selfPid = Process.GetCurrentProcess().Id; } catch { }

            try
            {
                // Force CLR Garbage Collection on self
                GC.Collect(2, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced);

                using (var selfProc = Process.GetCurrentProcess())
                {
                    SetProcessWorkingSetSize(selfProc.Handle, -1, -1);
                    processesTrimmed++;
                }

                // Safely trim eligible background user processes (skipping active foreground app to avoid UI page-fault stutters)
                var procs = Process.GetProcesses();
                foreach (var p in procs)
                {
                    try
                    {
                        if (p.Id <= 4 || p.Id == selfPid) continue;
                        if (foregroundPid > 0 && p.Id == foregroundPid && p.Id != selfPid) continue;
                        if (ProcessManager.IsProtected(p.ProcessName)) continue;

                        if (EmptyWorkingSet(p.Handle) != 0)
                        {
                            processesTrimmed++;
                        }
                    }
                    catch { }
                    finally
                    {
                        p.Dispose();
                    }
                }
            }
            catch { }

            var memAfter = NativeMethods.MEMORYSTATUSEX.Create();
            if (hasBefore && NativeMethods.GlobalMemoryStatusEx(ref memAfter) && memAfter.ullAvailPhys > memBefore.ullAvailPhys)
            {
                ulong freedBytes = memAfter.ullAvailPhys - memBefore.ullAvailPhys;
                return Math.Round((double)freedBytes / (1024.0 * 1024.0), 1);
            }

            return 0.0;
        }
    }
}
