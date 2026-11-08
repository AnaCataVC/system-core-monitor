using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SystemCoreMonitor.Widgets
{
    public static class WidgetDefinitionIds
    {
        public const string SystemOverview = "System_Overview";
        public const string CpuMonitor = "Cpu_Monitor";
        public const string CompactMonitor = "Compact_Monitor";

        public const string ProviderClsidString = "D3397A3F-F15C-48D3-8E1E-9C8A7B6D5E4F";
        public static readonly Guid ProviderClsid = Guid.Parse(ProviderClsidString);

        public const string PackageIdentityName = "SystemCoreMonitor.Widgets";
        public const string AppExtensionName = "com.microsoft.windows.widgets";
    }

    public enum WidgetCardSize
    {
        Small,
        Medium,
        Large
    }

    public class WidgetInstanceState
    {
        public string WidgetId { get; set; } = "";
        public string DefinitionId { get; set; } = "";
        public WidgetCardSize Size { get; set; } = WidgetCardSize.Medium;
        public bool IsActive { get; set; }
        public string CustomState { get; set; } = "";
        public DateTime LastUpdatedUtc { get; set; } = DateTime.MinValue;
    }

    public class WidgetCoreLoadInfo
    {
        public int CoreIndex { get; set; }
        public double LoadPercent { get; set; }
    }

    public class WidgetTelemetrySnapshot
    {
        // CPU
        public double CpuPercent { get; set; }
        public string CpuDisplay { get; set; } = "0.0%";
        public string CpuStatus { get; set; } = "Ok";
        public int ActiveCores { get; set; }
        public int TotalCores { get; set; }
        public string CoresDisplay { get; set; } = "";
        public List<WidgetCoreLoadInfo> TopCores { get; set; } = new List<WidgetCoreLoadInfo>();
        public List<double> AllCoreLoads { get; set; } = new List<double>();

        // RAM
        public double RamPercent { get; set; }
        public string RamDisplay { get; set; } = "0.0%";
        public string RamStatus { get; set; } = "Ok";
        public double RamUsedGB { get; set; }
        public double RamTotalGB { get; set; }
        public string RamDetailDisplay { get; set; } = "";

        // GPU
        public double GpuPercent { get; set; }
        public string GpuDisplay { get; set; } = "0.0%";
        public string GpuStatus { get; set; } = "Ok";
        public string GpuName { get; set; } = "Graphics Adapter";
        public string GpuVendor { get; set; } = "GPU";
        public bool IsGpuMonitoringEnabled { get; set; } = true;

        // Disk (Primary system drive)
        public string DiskName { get; set; } = "C:";
        public double DiskPercent { get; set; }
        public string DiskDisplay { get; set; } = "0.0%";
        public string DiskFreeDisplay { get; set; } = "";
        public string DiskStatus { get; set; } = "Ok";

        // Network
        public string NetDownloadDisplay { get; set; } = "0 KB/s";
        public string NetUploadDisplay { get; set; } = "0 KB/s";
        public string NetPingDisplay { get; set; } = "-- ms";

        // Metadata
        public string Theme { get; set; } = "Dark";
        public string Language { get; set; } = "es";
        public string UptimeDisplay { get; set; } = "";
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }

    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(WidgetInstanceState))]
    [JsonSerializable(typeof(List<WidgetInstanceState>))]
    [JsonSerializable(typeof(Dictionary<string, WidgetInstanceState>))]
    [JsonSerializable(typeof(WidgetTelemetrySnapshot))]
    internal partial class WidgetJsonContext : JsonSerializerContext { }
}
