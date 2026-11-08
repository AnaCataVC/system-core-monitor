using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SystemCoreMonitor.Widgets
{
    public class WidgetThemeColors
    {
        public string Name { get; set; } = "Dark";
        public string BgCard { get; set; } = "#1F2333";
        public string Track { get; set; } = "#2A2F45";
        public string TextPrimary { get; set; } = "#F2F5FB";
        public string TextSecondary { get; set; } = "#8F97B2";
        public string TextMuted { get; set; } = "#5D6582";

        public string AccentCpu { get; set; } = "#93A8FD";
        public string AccentRam { get; set; } = "#FFC371";
        public string AccentGpu { get; set; } = "#7AE5F5";
        public string AccentDisk { get; set; } = "#7EE7B8";
        public string AccentNet { get; set; } = "#68D391";

        public string StatusOk { get; set; } = "#7EE7B8";
        public string StatusWarn { get; set; } = "#FFC371";
        public string StatusCrit { get; set; } = "#FF7E8D";
    }

    public static class WidgetThemePalette
    {
        private static readonly Dictionary<string, WidgetThemeColors> Palettes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Dark"] = new WidgetThemeColors
            {
                Name = "Dark",
                BgCard = "#1F2333",
                Track = "#2A2F45",
                TextPrimary = "#F2F5FB",
                TextSecondary = "#8F97B2",
                TextMuted = "#5D6582",
                AccentCpu = "#93A8FD",
                AccentRam = "#FFC371",
                AccentGpu = "#7AE5F5",
                AccentDisk = "#7EE7B8",
                AccentNet = "#68D391",
                StatusOk = "#7EE7B8",
                StatusWarn = "#FFC371",
                StatusCrit = "#FF7E8D"
            },
            ["Light"] = new WidgetThemeColors
            {
                Name = "Light",
                BgCard = "#FFFFFF",
                Track = "#E2E8F0",
                TextPrimary = "#1E293B",
                TextSecondary = "#64748B",
                TextMuted = "#94A3B8",
                AccentCpu = "#6366F1",
                AccentRam = "#D97706",
                AccentGpu = "#0284C7",
                AccentDisk = "#0EA5E9",
                AccentNet = "#10B981",
                StatusOk = "#10B981",
                StatusWarn = "#F59E0B",
                StatusCrit = "#EF4444"
            },
            ["Neon"] = new WidgetThemeColors
            {
                Name = "Neon",
                BgCard = "#161C2B",
                Track = "#222B3D",
                TextPrimary = "#00F5FF",
                TextSecondary = "#7DD3FC",
                TextMuted = "#4A6587",
                AccentCpu = "#00F5FF",
                AccentRam = "#FFE600",
                AccentGpu = "#38BDF8",
                AccentDisk = "#A855F7",
                AccentNet = "#00FF88",
                StatusOk = "#00FF88",
                StatusWarn = "#FFE600",
                StatusCrit = "#FF0055"
            },
            ["Rose"] = new WidgetThemeColors
            {
                Name = "Rose",
                BgCard = "#2B222D",
                Track = "#3F3242",
                TextPrimary = "#FFF0F5",
                TextSecondary = "#E8B4CB",
                TextMuted = "#997387",
                AccentCpu = "#FF8DA1",
                AccentRam = "#FFD3B6",
                AccentGpu = "#A8E6CF",
                AccentDisk = "#FFAAA5",
                AccentNet = "#DCEDC1",
                StatusOk = "#A8E6CF",
                StatusWarn = "#FFD3B6",
                StatusCrit = "#FF8DA1"
            }
        };

        public static WidgetThemeColors GetColors(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName) || !Palettes.TryGetValue(themeName, out var colors))
            {
                return Palettes["Dark"];
            }
            return colors;
        }

        public static string ResolveBarColor(double percent, string baseAccentHex, WidgetThemeColors theme)
        {
            if (percent >= 90.0) return theme.StatusCrit;
            if (percent >= 80.0) return theme.StatusWarn;
            return baseAccentHex;
        }

        /// <summary>
        /// Generates a lightweight, self-contained SVG progress bar data URI (e.g., data:image/svg+xml;utf8,...)
        /// to render high-contrast smooth telemetry bars inside Adaptive Cards across light/dark themes.
        /// </summary>
        public static string BuildProgressBarSvgDataUri(double percent, string barColor, string trackColor, int width = 280, int height = 8, int radius = 4)
        {
            double clamped = Math.Max(0.0, Math.Min(100.0, percent));
            double barWidth = (width * clamped) / 100.0;
            string wStr = barWidth.ToString("F1", CultureInfo.InvariantCulture);

            string svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='0 0 {width} {height}'>" +
                         $"<rect width='{width}' height='{height}' rx='{radius}' fill='{trackColor}'/>" +
                         $"<rect width='{wStr}' height='{height}' rx='{radius}' fill='{barColor}'/>" +
                         $"</svg>";

            return "data:image/svg+xml;utf8," + Uri.EscapeDataString(svg);
        }

        /// <summary>
        /// Generates an SVG segmented strip of core loads for multi-core CPU breakdown in Adaptive Cards.
        /// </summary>
        public static string BuildCoreStripSvgDataUri(IReadOnlyList<double> coreLoads, WidgetThemeColors theme, int width = 280, int height = 12)
        {
            if (coreLoads == null || coreLoads.Count == 0)
            {
                return BuildProgressBarSvgDataUri(0, theme.AccentCpu, theme.Track, width, height, 3);
            }

            int count = Math.Min(coreLoads.Count, 32);
            double gap = 2.0;
            double totalGaps = gap * (count - 1);
            double barWidth = Math.Max(2.0, (width - totalGaps) / count);

            var sb = new StringBuilder();
            sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='0 0 {width} {height}'>");

            for (int i = 0; i < count; i++)
            {
                double load = Math.Max(0.0, Math.Min(100.0, coreLoads[i]));
                double x = i * (barWidth + gap);
                string xStr = x.ToString("F1", CultureInfo.InvariantCulture);
                string bwStr = barWidth.ToString("F1", CultureInfo.InvariantCulture);
                string color = ResolveBarColor(load, theme.AccentCpu, theme);

                // Background track
                sb.Append($"<rect x='{xStr}' y='0' width='{bwStr}' height='{height}' rx='2' fill='{theme.Track}'/>");

                // Filled portion from bottom up
                double fillHeight = Math.Max(1.0, (height * load) / 100.0);
                double y = height - fillHeight;
                string yStr = y.ToString("F1", CultureInfo.InvariantCulture);
                string fhStr = fillHeight.ToString("F1", CultureInfo.InvariantCulture);

                sb.Append($"<rect x='{xStr}' y='yStr' width='{bwStr}' height='{fhStr}' rx='2' fill='{color}'/>"
                    .Replace("yStr", yStr));
            }

            sb.Append("</svg>");
            return "data:image/svg+xml;utf8," + Uri.EscapeDataString(sb.ToString());
        }
    }
}
