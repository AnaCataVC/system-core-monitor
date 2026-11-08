using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SystemCoreMonitor.Core;

namespace SystemCoreMonitor.Widgets
{
    /// <summary>
    /// Out-of-process COM / CLI Widget Provider implementing the full Windows 11 Widget lifecycle:
    /// CreateWidget, DeleteWidget, Activate, Deactivate, OnWidgetContextChanged, OnActionInvoked.
    /// Manages state persistence and only polls when at least one widget is active (0% CPU idle).
    /// </summary>
    [ComVisible(true)]
    [Guid(WidgetComGuids.IWidgetProvider)]
    public class SystemCoreWidgetProvider : IDisposable
    {
        private static readonly Lazy<SystemCoreWidgetProvider> _instance =
            new Lazy<SystemCoreWidgetProvider>(() => new SystemCoreWidgetProvider());

        public static SystemCoreWidgetProvider Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, WidgetInstanceState> _widgets = new();
        private readonly Timer _updateTimer;
        private readonly ManualResetEvent _emptyWidgetListEvent = new(false);
        private readonly object _stateSync = new();
        private bool _isDisposed;

        private static readonly string StateFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SystemCoreMonitor",
            "widgets-state.json");

        public SystemCoreWidgetProvider()
        {
            _updateTimer = new Timer(OnTimerTick, null, Timeout.Infinite, Timeout.Infinite);
            LoadPersistedState();
        }

        public ManualResetEvent EmptyWidgetListEvent => _emptyWidgetListEvent;
        public int TrackedWidgetCount => _widgets.Count;
        public bool HasActiveWidgets => _widgets.Values.Any(w => w.IsActive);

        #region Lifecycle Methods

        public void CreateWidget(string widgetId, string definitionId, string sizeStr)
        {
            var size = ParseSize(sizeStr);
            var state = new WidgetInstanceState
            {
                WidgetId = widgetId,
                DefinitionId = definitionId,
                Size = size,
                IsActive = true,
                LastUpdatedUtc = DateTime.UtcNow
            };

            _widgets[widgetId] = state;
            _emptyWidgetListEvent.Reset();
            SavePersistedState();

            _ = UpdateSingleWidgetAsync(state);
            SyncTimerState();
        }

        public void DeleteWidget(string widgetId, string customState)
        {
            _widgets.TryRemove(widgetId, out _);
            SavePersistedState();

            if (_widgets.IsEmpty)
            {
                _emptyWidgetListEvent.Set();
            }
            SyncTimerState();
        }

        public void Activate(string widgetId)
        {
            if (_widgets.TryGetValue(widgetId, out var state))
            {
                state.IsActive = true;
                _ = UpdateSingleWidgetAsync(state);
            }
            SyncTimerState();
        }

        public void Deactivate(string widgetId)
        {
            if (_widgets.TryGetValue(widgetId, out var state))
            {
                state.IsActive = false;
            }
            SyncTimerState();
        }

        public void OnWidgetContextChanged(string widgetId, string sizeStr)
        {
            if (_widgets.TryGetValue(widgetId, out var state))
            {
                state.Size = ParseSize(sizeStr);
                _ = UpdateSingleWidgetAsync(state);
            }
        }

        public void OnActionInvoked(string widgetId, string verb, string data)
        {
            if (string.Equals(verb, "refresh", StringComparison.OrdinalIgnoreCase))
            {
                if (_widgets.TryGetValue(widgetId, out var state))
                {
                    _ = UpdateSingleWidgetAsync(state);
                }
            }
            else if (string.Equals(verb, "openApp", StringComparison.OrdinalIgnoreCase))
            {
                LaunchMainApplication();
            }
        }

        #endregion

        #region Update & Dispatch

        private void SyncTimerState()
        {
            bool anyActive = HasActiveWidgets;
            if (anyActive)
            {
                var cfg = ConfigManager.Load();
                int intervalSec = Math.Max(2, cfg.RefreshIntervalSeconds);
                _updateTimer.Change(TimeSpan.FromSeconds(intervalSec), TimeSpan.FromSeconds(intervalSec));
            }
            else
            {
                _updateTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        private void OnTimerTick(object? state)
        {
            if (_isDisposed) return;
            _ = UpdateAllActiveWidgetsAsync();
        }

        public async Task UpdateAllActiveWidgetsAsync()
        {
            var activeWidgets = _widgets.Values.Where(w => w.IsActive).ToList();
            if (activeWidgets.Count == 0)
            {
                SyncTimerState();
                return;
            }

            bool needsGpu = activeWidgets.Any(w => w.DefinitionId != WidgetDefinitionIds.CpuMonitor);
            bool needsDiskNet = activeWidgets.Any(w => w.DefinitionId == WidgetDefinitionIds.SystemOverview);
            bool needsCores = activeWidgets.Any(w => w.DefinitionId == WidgetDefinitionIds.CpuMonitor);

            var snapshot = await WidgetTelemetryCoordinator.Instance
                .SampleSnapshotAsync(needsGpu, needsDiskNet, needsCores)
                .ConfigureAwait(false);

            foreach (var widget in activeWidgets)
            {
                await PushWidgetUpdatePayloadAsync(widget, snapshot).ConfigureAwait(false);
            }
        }

        public async Task UpdateSingleWidgetAsync(WidgetInstanceState widget)
        {
            bool needsGpu = widget.DefinitionId != WidgetDefinitionIds.CpuMonitor;
            bool needsDiskNet = widget.DefinitionId == WidgetDefinitionIds.SystemOverview;
            bool needsCores = widget.DefinitionId == WidgetDefinitionIds.CpuMonitor;

            var snapshot = await WidgetTelemetryCoordinator.Instance
                .SampleSnapshotAsync(needsGpu, needsDiskNet, needsCores)
                .ConfigureAwait(false);

            await PushWidgetUpdatePayloadAsync(widget, snapshot).ConfigureAwait(false);
        }

        private static async Task PushWidgetUpdatePayloadAsync(WidgetInstanceState widget, WidgetTelemetrySnapshot snapshot)
        {
            string template = WidgetAdaptiveCardRenderer.RenderTemplate(widget.DefinitionId, widget.Size);
            string data = WidgetAdaptiveCardRenderer.RenderDataPayload(widget.DefinitionId, widget.Size, snapshot);

            // WinRT WidgetManager dispatch (dynamic invocation if available, or logging in fallback)
            await Task.Run(() =>
            {
                try
                {
                    Type? widgetManagerType = Type.GetType("Microsoft.Windows.Widgets.Providers.WidgetManager, Microsoft.Windows.Widgets.Providers");
                    if (widgetManagerType != null)
                    {
                        var getDefaultMethod = widgetManagerType.GetMethod("GetDefault");
                        var managerInstance = getDefaultMethod?.Invoke(null, null);

                        Type? updateOptionsType = Type.GetType("Microsoft.Windows.Widgets.Providers.WidgetUpdateRequestOptions, Microsoft.Windows.Widgets.Providers");
                        if (managerInstance != null && updateOptionsType != null)
                        {
                            var options = Activator.CreateInstance(updateOptionsType, widget.WidgetId);
                            updateOptionsType.GetProperty("Template")?.SetValue(options, template);
                            updateOptionsType.GetProperty("Data")?.SetValue(options, data);
                            updateOptionsType.GetProperty("CustomState")?.SetValue(options, widget.CustomState);

                            var updateMethod = widgetManagerType.GetMethod("UpdateWidget", new[] { updateOptionsType });
                            updateMethod?.Invoke(managerInstance, new[] { options });
                        }
                    }
                }
                catch (Exception ex)
                {
                    CrashLogger.LogException("SystemCoreWidgetProvider.PushWidgetUpdatePayloadAsync", ex, false);
                }
            }).ConfigureAwait(false);
        }

        private static void LaunchMainApplication()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                CrashLogger.LogException("SystemCoreWidgetProvider.LaunchMainApplication", ex, false);
            }
        }

        #endregion

        #region Protocol Helper (--widget-call parser)

        public bool HandleWidgetCallJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("WidgetCall", out var callProp)) return false;

                string call = callProp.GetString() ?? "";

                switch (call)
                {
                    case "CreateWidget":
                        if (root.TryGetProperty("WidgetContext", out var ctx))
                        {
                            string id = ctx.GetProperty("Id").GetString() ?? "";
                            string def = ctx.GetProperty("DefinitionId").GetString() ?? "";
                            string size = ctx.TryGetProperty("Size", out var sz) ? (sz.GetString() ?? "Medium") : "Medium";
                            CreateWidget(id, def, size);
                            return true;
                        }
                        break;

                    case "DeleteWidget":
                        if (root.TryGetProperty("WidgetId", out var delId))
                        {
                            string id = delId.GetString() ?? "";
                            string customState = root.TryGetProperty("CustomState", out var cs) ? (cs.GetString() ?? "") : "";
                            DeleteWidget(id, customState);
                            return true;
                        }
                        break;

                    case "Activate":
                        if (root.TryGetProperty("WidgetContext", out var actCtx))
                        {
                            string id = actCtx.GetProperty("Id").GetString() ?? "";
                            Activate(id);
                            return true;
                        }
                        break;

                    case "Deactivate":
                        if (root.TryGetProperty("WidgetId", out var deactId))
                        {
                            string id = deactId.GetString() ?? "";
                            Deactivate(id);
                            return true;
                        }
                        break;

                    case "OnWidgetContextChanged":
                        if (root.TryGetProperty("Args", out var args) && args.TryGetProperty("WidgetContext", out var chCtx))
                        {
                            string id = chCtx.GetProperty("Id").GetString() ?? "";
                            string size = chCtx.TryGetProperty("Size", out var sz) ? (sz.GetString() ?? "Medium") : "Medium";
                            OnWidgetContextChanged(id, size);
                            return true;
                        }
                        break;

                    case "OnActionInvoked":
                        if (root.TryGetProperty("Args", out var actArgs))
                        {
                            string verb = actArgs.TryGetProperty("Verb", out var v) ? (v.GetString() ?? "") : "";
                            string data = actArgs.TryGetProperty("Data", out var d) ? (d.GetString() ?? "") : "";
                            string id = "";
                            if (actArgs.TryGetProperty("WidgetContext", out var wCtx))
                            {
                                id = wCtx.GetProperty("Id").GetString() ?? "";
                            }
                            OnActionInvoked(id, verb, data);
                            return true;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                CrashLogger.LogException("SystemCoreWidgetProvider.HandleWidgetCallJson", ex, false);
            }
            return false;
        }

        #endregion

        #region State Persistence

        private void LoadPersistedState()
        {
            try
            {
                lock (_stateSync)
                {
                    if (File.Exists(StateFilePath))
                    {
                        string json = File.ReadAllText(StateFilePath);
                        var list = JsonSerializer.Deserialize(json, WidgetJsonContext.Default.ListWidgetInstanceState);
                        if (list != null)
                        {
                            _widgets.Clear();
                            foreach (var item in list)
                            {
                                // Upon recovery from disk, mark inactive until the OS sends Activate
                                item.IsActive = false;
                                _widgets[item.WidgetId] = item;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CrashLogger.LogException("SystemCoreWidgetProvider.LoadPersistedState", ex, false);
            }
        }

        private void SavePersistedState()
        {
            try
            {
                lock (_stateSync)
                {
                    string dir = Path.GetDirectoryName(StateFilePath) ?? "";
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    var list = _widgets.Values.ToList();
                    string json = JsonSerializer.Serialize(list, WidgetJsonContext.Default.ListWidgetInstanceState);
                    File.WriteAllText(StateFilePath, json);
                }
            }
            catch (Exception ex)
            {
                CrashLogger.LogException("SystemCoreWidgetProvider.SavePersistedState", ex, false);
            }
        }

        private static WidgetCardSize ParseSize(string? size)
        {
            if (string.Equals(size, "Small", StringComparison.OrdinalIgnoreCase)) return WidgetCardSize.Small;
            if (string.Equals(size, "Large", StringComparison.OrdinalIgnoreCase)) return WidgetCardSize.Large;
            return WidgetCardSize.Medium;
        }

        #endregion

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _updateTimer.Dispose();
            _emptyWidgetListEvent.Dispose();
        }
    }
}
