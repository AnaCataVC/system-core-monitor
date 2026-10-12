using System;
using System.Collections.Generic;
using SystemCoreMonitor.Core;

namespace SystemCoreMonitor.Models
{
    public enum AiTranscriptTargetType
    {
        ClaudeCli,
        GeminiAntigravity,
        ClaudeDesktop
    }

    public class AiTranscriptStoreInfo
    {
        public string StoreName { get; set; } = string.Empty;
        public string BasePath { get; set; } = string.Empty;
        public AiTranscriptTargetType TargetType { get; set; }
        public int TotalFileCount { get; set; }
        public long TotalSizeBytes { get; set; }
        public int StaleFileCount { get; set; }
        public long StaleSizeBytes { get; set; }

        public string TotalSizeDisplay => MetricFormatting.FormatBytesAuto(TotalSizeBytes);
        public string StaleSizeDisplay => MetricFormatting.FormatBytesAuto(StaleSizeBytes);
    }

    public class AiTranscriptItem
    {
        public string SessionId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public AiTranscriptTargetType TargetType { get; set; }
        public DateTime LastModified { get; set; }
        public long FileSizeBytes { get; set; }
        public bool IsStale { get; set; }
        public bool IsActive { get; set; }
        public int? ProcessId { get; set; }

        public string FileSizeDisplay => MetricFormatting.FormatBytesAuto(FileSizeBytes);
        public string LastModifiedDisplay => LastModified != DateTime.MinValue ? LastModified.ToString("yyyy-MM-dd HH:mm") : "N/A";
    }

    public class AiTranscriptReport
    {
        public List<AiTranscriptStoreInfo> Stores { get; set; } = new();
        public List<AiTranscriptItem> Items { get; set; } = new();
        public long TotalSizeBytes { get; set; }
        public long StaleSizeBytes { get; set; }
        public int TotalFilesCount { get; set; }
        public int StaleFilesCount { get; set; }
        public bool ClaudeIsRunning { get; set; }
        public bool AntigravityIsRunning { get; set; }

        public string TotalSizeDisplay => MetricFormatting.FormatBytesAuto(TotalSizeBytes);
        public string StaleSizeDisplay => MetricFormatting.FormatBytesAuto(StaleSizeBytes);
    }

    public class AiTranscriptCleanupResult
    {
        public int FilesProcessed { get; set; }
        public long BytesFreed { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Failures { get; set; } = new();
        public string Message { get; set; } = string.Empty;
        public bool Skipped { get; set; }

        public string BytesFreedDisplay => MetricFormatting.FormatBytesAuto(BytesFreed);
    }
}
