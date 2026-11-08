using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using SystemCoreMonitor.Core;

namespace SystemCoreMonitor.Widgets
{
    public static class WidgetAdaptiveCardRenderer
    {
        public const string AdaptiveCardSchema = "http://adaptivecards.io/schemas/adaptive-card.json";
        public const string CardVersion = "1.5";

        /// <summary>
        /// Renders the JSON Adaptive Card template for the given definition and size.
        /// </summary>
        public static string RenderTemplate(string definitionId, WidgetCardSize size)
        {
            return definitionId switch
            {
                WidgetDefinitionIds.SystemOverview => RenderSystemOverviewTemplate(size),
                WidgetDefinitionIds.CpuMonitor => RenderCpuMonitorTemplate(size),
                WidgetDefinitionIds.CompactMonitor => RenderCompactMonitorTemplate(size),
                _ => RenderSystemOverviewTemplate(size)
            };
        }

        /// <summary>
        /// Generates the dynamic JSON data payload for an Adaptive Card template based on a telemetry snapshot.
        /// </summary>
        public static string RenderDataPayload(string definitionId, WidgetCardSize size, WidgetTelemetrySnapshot snapshot)
        {
            var colors = WidgetThemePalette.GetColors(snapshot.Theme);
            bool isEs = snapshot.Language == "es";

            // SVG Progress bars generated with theme tokens
            string cpuBarSvg = WidgetThemePalette.BuildProgressBarSvgDataUri(
                snapshot.CpuPercent,
                WidgetThemePalette.ResolveBarColor(snapshot.CpuPercent, colors.AccentCpu, colors),
                colors.Track);

            string ramBarSvg = WidgetThemePalette.BuildProgressBarSvgDataUri(
                snapshot.RamPercent,
                WidgetThemePalette.ResolveBarColor(snapshot.RamPercent, colors.AccentRam, colors),
                colors.Track);

            string gpuBarSvg = WidgetThemePalette.BuildProgressBarSvgDataUri(
                snapshot.GpuPercent,
                WidgetThemePalette.ResolveBarColor(snapshot.GpuPercent, colors.AccentGpu, colors),
                colors.Track);

            string diskBarSvg = WidgetThemePalette.BuildProgressBarSvgDataUri(
                snapshot.DiskPercent,
                WidgetThemePalette.ResolveBarColor(snapshot.DiskPercent, colors.AccentDisk, colors),
                colors.Track);

            string coreStripSvg = WidgetThemePalette.BuildCoreStripSvgDataUri(snapshot.AllCoreLoads, colors, 280, 14);

            var dict = new Dictionary<string, object>
            {
                ["title"] = "System Core Monitor",
                ["headerTitle"] = definitionId switch
                {
                    WidgetDefinitionIds.CpuMonitor => isEs ? "PROCESADOR (CPU)" : "PROCESSOR (CPU)",
                    WidgetDefinitionIds.CompactMonitor => isEs ? "MÉTRICAS DEL SISTEMA" : "SYSTEM METRICS",
                    _ => isEs ? "RESUMEN DEL SISTEMA" : "SYSTEM OVERVIEW"
                },
                ["uptime"] = isEs ? $"Activo: {snapshot.UptimeDisplay}" : $"Uptime: {snapshot.UptimeDisplay}",
                ["updatedTime"] = DateTime.Now.ToString("HH:mm:ss"),
                ["lblCpu"] = "CPU",
                ["valCpu"] = snapshot.CpuDisplay,
                ["barCpu"] = cpuBarSvg,
                ["lblRam"] = "RAM",
                ["valRam"] = snapshot.RamDisplay,
                ["detailRam"] = snapshot.RamDetailDisplay,
                ["barRam"] = ramBarSvg,
                ["lblGpu"] = "GPU",
                ["valGpu"] = snapshot.GpuDisplay,
                ["nameGpu"] = snapshot.GpuName,
                ["barGpu"] = gpuBarSvg,
                ["lblDisk"] = isEs ? $"Disco ({snapshot.DiskName})" : $"Disk ({snapshot.DiskName})",
                ["valDisk"] = snapshot.DiskDisplay,
                ["freeDisk"] = snapshot.DiskFreeDisplay,
                ["barDisk"] = diskBarSvg,
                ["lblNet"] = isEs ? "Red" : "Network",
                ["netRx"] = $"↓ {snapshot.NetDownloadDisplay}",
                ["netTx"] = $"↑ {snapshot.NetUploadDisplay}",
                ["netPing"] = snapshot.NetPingDisplay,
                ["lblCores"] = isEs ? "Núcleos en Uso" : "Cores in Use",
                ["valCores"] = snapshot.CoresDisplay,
                ["coreStripSvg"] = coreStripSvg,
                ["btnRefresh"] = isEs ? "Actualizar" : "Refresh",
                ["btnOpenApp"] = isEs ? "Abrir Panel" : "Open App"
            };

            // Top cores items for CPU large cards
            var topCoresList = new List<Dictionary<string, string>>();
            foreach (var core in snapshot.TopCores)
            {
                topCoresList.Add(new Dictionary<string, string>
                {
                    ["coreLabel"] = $"C{core.CoreIndex}",
                    ["coreLoad"] = $"{core.LoadPercent:N0}%"
                });
            }
            dict["topCores"] = topCoresList;

            return JsonSerializer.Serialize(dict);
        }

        #region System Overview Template

        private static string RenderSystemOverviewTemplate(WidgetCardSize size)
        {
            var sb = new StringBuilder();
            sb.Append($$"""
            {
              "$schema": "{{AdaptiveCardSchema}}",
              "type": "AdaptiveCard",
              "version": "{{CardVersion}}",
              "body": [
                {
                  "type": "ColumnSet",
                  "columns": [
                    {
                      "type": "Column",
                      "width": "stretch",
                      "items": [
                        {
                          "type": "TextBlock",
                          "text": "${headerTitle}",
                          "size": "Medium",
                          "weight": "Bolder",
                          "wrap": true
                        },
                        {
                          "type": "TextBlock",
                          "text": "${uptime} • ${updatedTime}",
                          "size": "Small",
                          "isSubtle": true,
                          "spacing": "None",
                          "wrap": true
                        }
                      ]
                    }
                  ]
                },
                {
                  "type": "Container",
                  "spacing": "Medium",
                  "items": [
                    {
                      "type": "ColumnSet",
                      "columns": [
                        { "type": "Column", "width": "stretch", "items": [{ "type": "TextBlock", "text": "${lblCpu}", "weight": "Bolder" }] },
                        { "type": "Column", "width": "auto", "items": [{ "type": "TextBlock", "text": "${valCpu}", "weight": "Bolder", "horizontalAlignment": "Right" }] }
                      ]
                    },
                    { "type": "Image", "url": "${barCpu}", "size": "Stretch", "spacing": "Small" }
                  ]
                },
                {
                  "type": "Container",
                  "spacing": "Small",
                  "items": [
                    {
                      "type": "ColumnSet",
                      "columns": [
                        { "type": "Column", "width": "stretch", "items": [{ "type": "TextBlock", "text": "${lblRam}", "weight": "Bolder" }] },
                        { "type": "Column", "width": "auto", "items": [{ "type": "TextBlock", "text": "${valRam} (${detailRam})", "weight": "Bolder", "horizontalAlignment": "Right" }] }
                      ]
                    },
                    { "type": "Image", "url": "${barRam}", "size": "Stretch", "spacing": "Small" }
                  ]
                },
                {
                  "type": "Container",
                  "spacing": "Small",
                  "items": [
                    {
                      "type": "ColumnSet",
                      "columns": [
                        { "type": "Column", "width": "stretch", "items": [{ "type": "TextBlock", "text": "${lblGpu}", "weight": "Bolder" }] },
                        { "type": "Column", "width": "auto", "items": [{ "type": "TextBlock", "text": "${valGpu}", "weight": "Bolder", "horizontalAlignment": "Right" }] }
                      ]
                    },
                    { "type": "Image", "url": "${barGpu}", "size": "Stretch", "spacing": "Small" }
                  ]
                }
            """);

            // Include Disk and Network on medium and large cards
            sb.Append($$"""
                ,
                {
                  "type": "Container",
                  "spacing": "Small",
                  "items": [
                    {
                      "type": "ColumnSet",
                      "columns": [
                        { "type": "Column", "width": "stretch", "items": [{ "type": "TextBlock", "text": "${lblDisk}", "weight": "Bolder" }] },
                        { "type": "Column", "width": "auto", "items": [{ "type": "TextBlock", "text": "${valDisk} (${freeDisk})", "weight": "Bolder", "horizontalAlignment": "Right" }] }
                      ]
                    },
                    { "type": "Image", "url": "${barDisk}", "size": "Stretch", "spacing": "Small" }
                  ]
                },
                {
                  "type": "ColumnSet",
                  "spacing": "Small",
                  "columns": [
                    {
                      "type": "Column",
                      "width": "stretch",
                      "items": [
                        { "type": "TextBlock", "text": "${lblNet}", "size": "Small", "weight": "Bolder" },
                        { "type": "TextBlock", "text": "${netRx}  ${netTx}", "size": "Small", "isSubtle": true, "spacing": "None" }
                      ]
                    },
                    {
                      "type": "Column",
                      "width": "auto",
                      "items": [
                        { "type": "TextBlock", "text": "${netPing}", "size": "Small", "horizontalAlignment": "Right", "weight": "Bolder" }
                      ]
                    }
                  ]
                }
            """);

            // Utility buttons
            sb.Append($$"""
              ],
              "actions": [
                {
                  "type": "Action.Execute",
                  "title": "${btnRefresh}",
                  "verb": "refresh"
                },
                {
                  "type": "Action.Execute",
                  "title": "${btnOpenApp}",
                  "verb": "openApp"
                }
              ]
            }
            """);

            return sb.ToString();
        }

        #endregion

        #region CPU Monitor Template

        private static string RenderCpuMonitorTemplate(WidgetCardSize size)
        {
            return $$"""
            {
              "$schema": "{{AdaptiveCardSchema}}",
              "type": "AdaptiveCard",
              "version": "{{CardVersion}}",
              "body": [
                {
                  "type": "ColumnSet",
                  "columns": [
                    {
                      "type": "Column",
                      "width": "stretch",
                      "items": [
                        { "type": "TextBlock", "text": "${headerTitle}", "size": "Medium", "weight": "Bolder" },
                        { "type": "TextBlock", "text": "${valCores} • ${uptime}", "size": "Small", "isSubtle": true, "spacing": "None" }
                      ]
                    },
                    {
                      "type": "Column",
                      "width": "auto",
                      "items": [
                        { "type": "TextBlock", "text": "${valCpu}", "size": "ExtraLarge", "weight": "Bolder", "horizontalAlignment": "Right" }
                      ]
                    }
                  ]
                },
                {
                  "type": "Container",
                  "spacing": "Small",
                  "items": [
                    { "type": "Image", "url": "${barCpu}", "size": "Stretch" }
                  ]
                },
                {
                  "type": "Container",
                  "spacing": "Medium",
                  "items": [
                    { "type": "TextBlock", "text": "${lblCores}", "size": "Small", "weight": "Bolder" },
                    { "type": "Image", "url": "${coreStripSvg}", "size": "Stretch", "spacing": "Small" }
                  ]
                }
              ],
              "actions": [
                {
                  "type": "Action.Execute",
                  "title": "${btnRefresh}",
                  "verb": "refresh"
                },
                {
                  "type": "Action.Execute",
                  "title": "${btnOpenApp}",
                  "verb": "openApp"
                }
              ]
            }
            """;
        }

        #endregion

        #region Compact Monitor Template

        private static string RenderCompactMonitorTemplate(WidgetCardSize size)
        {
            return $$"""
            {
              "$schema": "{{AdaptiveCardSchema}}",
              "type": "AdaptiveCard",
              "version": "{{CardVersion}}",
              "body": [
                {
                  "type": "ColumnSet",
                  "columns": [
                    {
                      "type": "Column",
                      "width": "stretch",
                      "items": [
                        { "type": "TextBlock", "text": "CPU", "size": "Small", "weight": "Bolder", "horizontalAlignment": "Center" },
                        { "type": "TextBlock", "text": "${valCpu}", "size": "Large", "weight": "Bolder", "horizontalAlignment": "Center", "spacing": "None" }
                      ]
                    },
                    {
                      "type": "Column",
                      "width": "stretch",
                      "items": [
                        { "type": "TextBlock", "text": "RAM", "size": "Small", "weight": "Bolder", "horizontalAlignment": "Center" },
                        { "type": "TextBlock", "text": "${valRam}", "size": "Large", "weight": "Bolder", "horizontalAlignment": "Center", "spacing": "None" }
                      ]
                    },
                    {
                      "type": "Column",
                      "width": "stretch",
                      "items": [
                        { "type": "TextBlock", "text": "GPU", "size": "Small", "weight": "Bolder", "horizontalAlignment": "Center" },
                        { "type": "TextBlock", "text": "${valGpu}", "size": "Large", "weight": "Bolder", "horizontalAlignment": "Center", "spacing": "None" }
                      ]
                    }
                  ]
                }
              ],
              "actions": [
                {
                  "type": "Action.Execute",
                  "title": "${btnRefresh}",
                  "verb": "refresh"
                },
                {
                  "type": "Action.Execute",
                  "title": "${btnOpenApp}",
                  "verb": "openApp"
                }
              ]
            }
            """;
        }

        #endregion
    }
}
