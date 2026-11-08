using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SystemCoreMonitor.Core;
using SystemCoreMonitor.Models;
using SystemCoreMonitor.Modules;

namespace SystemCoreMonitor.Widgets
{
    /// <summary>
    /// Thread-safe coordinator that shares and samples existing collectors for Windows 11 Widgets
    /// without coupling to WPF Dispatcher or UI controls. Respects selective hardware polling.
    /// </summary>
    public class WidgetTelemetryCoordinator : IDisposable
    {
        private static readonly Lazy<WidgetTelemetryCoordinator> _instance =
            new Lazy<WidgetTelemetryCoordinator>(() => new WidgetTelemetryCoordinator());

        public static WidgetTelemetryCoordinator Instance => _instance.Value;

        private readonly CpuCollector _cpu = new();
        private readonly MemoryCollector _mem = new();
        private readonly DiskCollector _disk = new();
        private readonly NetworkCollector _net = new();
        private readonly GpuCollector _gpu = new();
        private readonly WindowsAcceleratorEngine _accelEngine = new();

        private readonly SemaphoreSlim _sampleGate = new(1, 1);
        private WidgetTelemetrySnapshot? _lastSnapshot;

        public WidgetTelemetryCoordinator()
        {
        }

        public async Task<WidgetTelemetrySnapshot> SampleSnapshotAsync(
            bool needsGpu = true,
            bool needsDiskAndNet = true,
            bool needsCoreLoads = true)
        {
            await _sampleGate.WaitAsync().ConfigureAwait(false);
            try
            {
                var config = ConfigManager.Load();
                string lang = config.Language;
                string theme = config.Theme;

                // 1. Fast core metrics
                var cpuTask = Task.Run(() => _cpu.Sample());
                var memTask = Task.Run(() => _mem.Sample());
                var diskTask = needsDiskAndNet ? Task.Run(() => _disk.Sample()) : null;
                var netTask = needsDiskAndNet ? Task.Run(() => _net.Sample()) : null;

                bool shouldSampleGpu = needsGpu && config.EnableAcceleratorsMonitoring;
                var accelTask = shouldSampleGpu ? Task.Run(() => _accelEngine.SampleAllEngines()) : null;

                await Task.WhenAll(cpuTask, memTask).ConfigureAwait(false);
                var cpu = cpuTask.Result;
                var mem = memTask.Result;

                // Disk & Net
                List<DiskMetric>? disks = null;
                if (diskTask != null) disks = await diskTask.ConfigureAwait(false);

                NetworkMetric? net = null;
                if (netTask != null) net = await netTask.ConfigureAwait(false);

                // GPU
                GpuMetric? gpu = null;
                if (accelTask != null)
                {
                    var loads = await accelTask.ConfigureAwait(false);
                    gpu = _gpu.Sample(loads);
                }

                var snapshot = new WidgetTelemetrySnapshot
                {
                    Theme = theme,
                    Language = lang,
                    TimestampUtc = DateTime.UtcNow,
                    UptimeDisplay = HardwareCollector.GetUptimeDisplay(),

                    // CPU
                    CpuPercent = cpu.LoadPercent,
                    CpuDisplay = cpu.LoadDisplay,
                    CpuStatus = cpu.Status,
                    ActiveCores = cpu.ActiveCoreCount,
                    TotalCores = cpu.ProcessorCount,
                    CoresDisplay = string.Format(lang == "en" ? "{0}/{1} Cores" : "{0}/{1} Núcleos", cpu.ActiveCoreCount, cpu.ProcessorCount),
                    AllCoreLoads = needsCoreLoads && cpu.CoreLoads != null ? new List<double>(cpu.CoreLoads) : new List<double>(),

                    // RAM
                    RamPercent = mem.LoadPercent,
                    RamDisplay = mem.MemoryDisplay,
                    RamStatus = mem.Status,
                    RamUsedGB = mem.UsedGB,
                    RamTotalGB = mem.TotalGB,
                    RamDetailDisplay = mem.DetailDisplay
                };

                // Top Cores for CPU Widget
                if (cpu.CoreLoads != null && cpu.CoreLoads.Count > 0)
                {
                    snapshot.TopCores = cpu.CoreLoads
                        .Select((val, idx) => new WidgetCoreLoadInfo { CoreIndex = idx + 1, LoadPercent = val })
                        .OrderByDescending(c => c.LoadPercent)
                        .Take(6)
                        .ToList();
                }

                // Primary Disk
                if (disks != null && disks.Count > 0)
                {
                    var primary = disks.FirstOrDefault(d => d.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase)) ?? disks[0];
                    snapshot.DiskName = primary.Name;
                    snapshot.DiskPercent = primary.PercentUsed;
                    snapshot.DiskDisplay = string.Format("{0:N1}%", primary.PercentUsed);
                    snapshot.DiskFreeDisplay = MetricFormatting.FormatBytesAuto((long)(primary.FreeGB * 1024L * 1024L * 1024L));
                    snapshot.DiskStatus = primary.Status;
                }

                // Network
                if (net != null)
                {
                    snapshot.NetDownloadDisplay = net.DownloadDisplay;
                    snapshot.NetUploadDisplay = net.UploadDisplay;
                    snapshot.NetPingDisplay = net.PingDisplay;
                }

                // GPU
                if (gpu != null && gpu.IsPresent)
                {
                    snapshot.GpuPercent = gpu.LoadPercent;
                    snapshot.GpuDisplay = string.Format("{0:N1}%", gpu.LoadPercent);
                    snapshot.GpuStatus = gpu.Status;
                    snapshot.GpuName = gpu.Name;
                    snapshot.GpuVendor = gpu.Vendor;
                    snapshot.IsGpuMonitoringEnabled = true;
                }
                else
                {
                    snapshot.GpuPercent = 0.0;
                    snapshot.GpuDisplay = config.EnableAcceleratorsMonitoring ? "0.0%" : "Off";
                    snapshot.GpuStatus = "Ok";
                    snapshot.GpuName = config.EnableAcceleratorsMonitoring ? "GPU" : (lang == "en" ? "Disabled" : "Desactivado");
                    snapshot.GpuVendor = "GPU";
                    snapshot.IsGpuMonitoringEnabled = config.EnableAcceleratorsMonitoring;
                }

                _lastSnapshot = snapshot;
                return snapshot;
            }
            finally
            {
                _sampleGate.Release();
            }
        }

        public WidgetTelemetrySnapshot? GetLastSnapshot() => _lastSnapshot;

        public void Dispose()
        {
            _accelEngine.Dispose();
            _sampleGate.Dispose();
        }
    }
}
