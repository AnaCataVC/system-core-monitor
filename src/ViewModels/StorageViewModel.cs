using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SystemCoreMonitor.Core;
using SystemCoreMonitor.Core.Mvvm;
using SystemCoreMonitor.Models;

namespace SystemCoreMonitor.ViewModels
{
    public class StorageViewModel : ViewModelBase
    {
        private ObservableCollection<DiskMetric> _drives = new();
        private ObservableCollection<BloatFinding> _bloatFindings = new();
        private ObservableCollection<FolderSizeEntry> _folderScanEntries = new();
        private List<BloatFinding> _allBloatFindings = new();
        private CancellationTokenSource? _folderScanCts;

        private bool _isScanningBloat;
        private bool _isRefreshing;
        private bool _isBusyCleaning;
        private bool _isScanningFolder;
        private string _bloatFilterMode = "All";
        private DateTime? _lastUpdatedAt;

        private string _folderScanStatusText = string.Empty;
        private string _folderScanProgressText = string.Empty;
        private string _folderScanSkippedText = string.Empty;

        public ObservableCollection<DiskMetric> Drives
        {
            get => _drives;
            set => SetProperty(ref _drives, value);
        }

        public ObservableCollection<BloatFinding> BloatFindings
        {
            get => _bloatFindings;
            set => SetProperty(ref _bloatFindings, value);
        }

        public ObservableCollection<FolderSizeEntry> FolderScanEntries
        {
            get => _folderScanEntries;
            set => SetProperty(ref _folderScanEntries, value);
        }

        public AsyncRelayCommand RefreshCommand { get; }
        public AsyncRelayCommand CleanTempCommand { get; }
        public AsyncRelayCommand EmptyRecycleBinCommand { get; }
        public AsyncRelayCommand<BloatFinding> ExecuteBloatActionCommand { get; }
        public AsyncRelayCommand<BloatFinding> CompactVhdxCommand { get; }
        public AsyncRelayCommand ScanBloatCommand { get; }
        public RelayCommand OpenStorageToolCommand { get; }
        public RelayCommand<DiskMetric> OpenDriveCommand { get; }
        public RelayCommand<string> OpenPathCommand { get; }
        public RelayCommand<string> SetBloatFilterCommand { get; }
        public AsyncRelayCommand<string> ScanFolderCommand { get; }
        public RelayCommand CancelFolderScanCommand { get; }

        public bool IsScanningBloat
        {
            get => _isScanningBloat;
            set
            {
                if (SetProperty(ref _isScanningBloat, value))
                {
                    OnPropertyChanged(nameof(ScanButtonText));
                    OnPropertyChanged(nameof(HasNoBloatFindings));
                }
            }
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set
            {
                if (SetProperty(ref _isRefreshing, value))
                {
                    OnPropertyChanged(nameof(RefreshBtnText));
                }
            }
        }

        public bool IsBusyCleaning
        {
            get => _isBusyCleaning;
            set => SetProperty(ref _isBusyCleaning, value);
        }

        public bool IsScanningFolder
        {
            get => _isScanningFolder;
            set
            {
                if (SetProperty(ref _isScanningFolder, value))
                {
                    OnPropertyChanged(nameof(ShowFolderScanIdleHint));
                }
            }
        }

        public string BloatFilterMode
        {
            get => _bloatFilterMode;
            set
            {
                if (SetProperty(ref _bloatFilterMode, value))
                {
                    OnPropertyChanged(nameof(IsFilterAll));
                    OnPropertyChanged(nameof(IsFilterCleanable));
                    OnPropertyChanged(nameof(IsFilterSystem));
                    ApplyBloatFilter();
                }
            }
        }

        public bool IsFilterAll => string.Equals(_bloatFilterMode, "All", StringComparison.OrdinalIgnoreCase);
        public bool IsFilterCleanable => string.Equals(_bloatFilterMode, "Cleanable", StringComparison.OrdinalIgnoreCase);
        public bool IsFilterSystem => string.Equals(_bloatFilterMode, "System", StringComparison.OrdinalIgnoreCase);

        public string FolderScanStatusText
        {
            get => _folderScanStatusText;
            set
            {
                if (SetProperty(ref _folderScanStatusText, value))
                {
                    OnPropertyChanged(nameof(HasFolderScanStatus));
                }
            }
        }

        public string FolderScanProgressText
        {
            get => _folderScanProgressText;
            set => SetProperty(ref _folderScanProgressText, value);
        }

        public string FolderScanSkippedText
        {
            get => _folderScanSkippedText;
            set
            {
                if (SetProperty(ref _folderScanSkippedText, value))
                {
                    OnPropertyChanged(nameof(HasFolderScanSkipped));
                }
            }
        }

        public bool HasFolderScanStatus => !string.IsNullOrWhiteSpace(_folderScanStatusText);
        public bool HasFolderScanSkipped => !string.IsNullOrWhiteSpace(_folderScanSkippedText);
        public bool HasFolderScanResults => FolderScanEntries.Count > 0;
        public bool ShowFolderScanIdleHint => !IsScanningFolder && FolderScanEntries.Count == 0 && !HasFolderScanStatus;

        public bool HasNoBloatFindings => !IsScanningBloat && BloatFindings.Count == 0;
        public bool HasBloatItems => _allBloatFindings.Count > 0;

        // KPI Summary Properties
        public double TotalCapacityGB => _drives.Sum(d => d.TotalGB);
        public double TotalUsedGB => _drives.Sum(d => d.UsedGB);
        public double TotalFreeGB => _drives.Sum(d => d.FreeGB);
        public double OverallPercentUsed => TotalCapacityGB > 0 ? Math.Round((TotalUsedGB * 100.0) / TotalCapacityGB, 1) : 0.0;

        public string TotalCapacityDisplay => FormatGB(TotalCapacityGB);
        public string TotalUsedDisplay => FormatGB(TotalUsedGB);
        public string TotalFreeDisplay => FormatGB(TotalFreeGB);
        public string OverallPercentDisplay => $"{OverallPercentUsed:F0}%";
        public string DrivesCountDisplay => string.Format(LocalizationManager.Get("StorageKpiVolumes"), _drives.Count);

        public long ReclaimableBytes => _allBloatFindings.Where(f => f.IsCleanable).Sum(f => f.SizeBytes);
        public long TotalBloatBytes => _allBloatFindings.Sum(f => f.SizeBytes);
        public string ReclaimableSizeDisplay => MetricFormatting.FormatBytesAuto(ReclaimableBytes);
        public string BloatSummarySubtitle => string.Format(LocalizationManager.Get("StorageKpiReclaimableSub"), MetricFormatting.FormatBytesAuto(TotalBloatBytes));
        public string BloatCountDisplay => _allBloatFindings.Count.ToString();

        public string LastUpdatedDisplay => _lastUpdatedAt.HasValue
            ? string.Format(LocalizationManager.Get("StorageLastUpdated"), _lastUpdatedAt.Value.ToString("HH:mm:ss"))
            : string.Empty;

        // Localized Labels
        public string HeaderTitleText => LocalizationManager.Get("StorageHeaderTitle");
        public string HeaderDescText => LocalizationManager.Get("StorageHeaderDesc");
        public string RefreshBtnText => LocalizationManager.Get(IsRefreshing ? "StorageRefreshingBtn" : "StorageRefreshBtn");
        public string CleanTempBtnText => LocalizationManager.Get("StorageCleanTempBtn");
        public string EmptyBinBtnText => LocalizationManager.Get("StorageEmptyBinBtn");
        public string ScanButtonText => LocalizationManager.Get(IsScanningBloat ? "StorageScanningBtn" : "StorageScanBloatBtn");
        public string OpenPcManagerBtnText => LocalizationManager.Get("StorageOpenPcManagerBtn");
        public string DrivesTitleText => LocalizationManager.Get("StorageDrivesTitle");
        public string BloatTitleText => LocalizationManager.Get("StorageBloatTitle");
        public string EmptyBloatText => _allBloatFindings.Count > 0 && BloatFindings.Count == 0
            ? LocalizationManager.Get("StorageEmptyFilteredBloat")
            : LocalizationManager.Get("StorageEmptyBloat");

        public string KpiCapacityLabel => LocalizationManager.Get("StorageKpiCapacity");
        public string KpiUsedLabel => LocalizationManager.Get("StorageKpiUsed");
        public string KpiFreeLabel => LocalizationManager.Get("StorageKpiFree");
        public string KpiReclaimableLabel => LocalizationManager.Get("StorageKpiReclaimable");

        public string DriveUsedLabel => LocalizationManager.Get("StorageDriveUsed");
        public string DriveFreeLabel => LocalizationManager.Get("StorageDriveFree");
        public string DriveTotalLabel => LocalizationManager.Get("StorageDriveTotal");
        public string DriveOpenBtnText => LocalizationManager.Get("StorageDriveOpenBtn");
        public string DriveScanBtnText => LocalizationManager.Get("StorageDriveScanBtn");

        public string AnalyzerTitleText => LocalizationManager.Get("StorageScanTitle");
        public string AnalyzerSubtitleText => LocalizationManager.Get("StorageAnalyzerSubtitle");
        public string AnalyzerScanProfileBtnText => LocalizationManager.Get("StorageAnalyzerScanProfileBtn");
        public string AnalyzerCancelBtnText => LocalizationManager.Get("StorageScanCancel");
        public string AnalyzerIdleHintText => LocalizationManager.Get("StorageAnalyzerIdleHint");
        public string AnalyzerOpenFolderTip => LocalizationManager.Get("StorageAnalyzerOpenFolderTip");

        public string BloatFilterAllText => LocalizationManager.Get("StorageBloatFilterAll");
        public string BloatFilterCleanableText => LocalizationManager.Get("StorageBloatFilterCleanable");
        public string BloatFilterSystemText => LocalizationManager.Get("StorageBloatFilterSystem");
        public string BloatInfoBadgeText => LocalizationManager.Get("StorageBloatInfoBadge");
        public string BloatOpenPathTip => LocalizationManager.Get("StorageBloatOpenPathTip");
        public string CompactVhdxShortBtnText => LocalizationManager.Get("BloatActionCompactShort");

        public event Action<string, NotificationType>? ShowToastRequested;
        public event Action<string>? ShowProgressRequested;
        public event Func<Task>? RefreshRequested;

        public StorageViewModel()
        {
            LocalizationManager.LanguageChanged += () => OnPropertyChanged(string.Empty);

            RefreshCommand = new AsyncRelayCommand(async () =>
            {
                if (IsRefreshing) return;
                IsRefreshing = true;
                try
                {
                    if (RefreshRequested != null)
                    {
                        await RefreshRequested.Invoke();
                    }
                    _lastUpdatedAt = DateTime.Now;
                    OnPropertyChanged(nameof(LastUpdatedDisplay));
                    ShowToastRequested?.Invoke(LocalizationManager.Get("StorageRefreshedToast"), NotificationType.Info);
                }
                finally
                {
                    IsRefreshing = false;
                }
            });

            CleanTempCommand = new AsyncRelayCommand(async () =>
            {
                if (IsBusyCleaning) return;
                IsBusyCleaning = true;
                try
                {
                    ShowProgressRequested?.Invoke(LocalizationManager.Get("ActionCleaningTemp"));
                    var res = await Task.Run(() => SafeTempCleaner.CleanDeepStorage(false));
                    ShowToastRequested?.Invoke(string.Format(LocalizationManager.Get("ToastTempCleaned"), res.HumanSize, res.FilesDeleted), NotificationType.Success);
                    await TriggerPostActionRefreshAsync();
                }
                finally
                {
                    IsBusyCleaning = false;
                }
            });

            EmptyRecycleBinCommand = new AsyncRelayCommand(async () =>
            {
                if (IsBusyCleaning) return;
                IsBusyCleaning = true;
                try
                {
                    ShowProgressRequested?.Invoke(LocalizationManager.Get("ActionEmptyingRecycleBin"));
                    await Task.Run(() => NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null!, NativeMethods.SHERB_NOCONFIRMATION | NativeMethods.SHERB_NOPROGRESSUI));
                    ShowToastRequested?.Invoke(LocalizationManager.Get("ToastRecycleBinEmptied"), NotificationType.Success);
                    await TriggerPostActionRefreshAsync();
                }
                finally
                {
                    IsBusyCleaning = false;
                }
            });

            ExecuteBloatActionCommand = new AsyncRelayCommand<BloatFinding>(async finding =>
            {
                if (finding == null) return;
                if (finding.ActionKind == BloatDetector.ActionSafeDelete)
                {
                    if (IsBusyCleaning) return;
                    IsBusyCleaning = true;
                    try
                    {
                        if (finding.Category == "RecycleBin")
                        {
                            ShowProgressRequested?.Invoke(LocalizationManager.Get("ActionEmptyingRecycleBin"));
                            await Task.Run(() => NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null!, NativeMethods.SHERB_NOCONFIRMATION | NativeMethods.SHERB_NOPROGRESSUI));
                            _allBloatFindings.Remove(finding);
                            ApplyBloatFilter();
                            ShowToastRequested?.Invoke(LocalizationManager.Get("ToastRecycleBinEmptied"), NotificationType.Success);
                            await TriggerPostActionRefreshAsync();
                        }
                        else
                        {
                            ShowProgressRequested?.Invoke(string.Format(LocalizationManager.Get("StorageCleaningCacheProgress"), finding.Category));
                            var res = await Task.Run(() => BloatDetector.DeleteWhitelistedCache(finding.Path));
                            ShowToastRequested?.Invoke(string.Format(LocalizationManager.Get("ToastTempCleaned"), res.HumanSize, res.FilesDeleted), NotificationType.Success);
                            var refreshedBloat = await Task.Run(() => BloatDetector.Scan(includeComponentStore: false));
                            UpdateBloat(refreshedBloat);
                            await TriggerPostActionRefreshAsync();
                        }
                    }
                    finally
                    {
                        IsBusyCleaning = false;
                    }
                }
                else if (finding.ActionKind == BloatDetector.ActionDockerPrune)
                {
                    await RunDockerPruneAsync();
                }
                else if (finding.ActionKind == BloatDetector.ActionCompactVhdx)
                {
                    await CompactVhdxAsync(finding);
                }
                else if (finding.ActionKind == BloatDetector.ActionLaunchTool)
                {
                    ToolLauncher.StartPCManager();
                }
            });

            CompactVhdxCommand = new AsyncRelayCommand<BloatFinding>(async finding =>
            {
                if (finding == null) return;
                await CompactVhdxAsync(finding);
            });

            ScanBloatCommand = new AsyncRelayCommand(async () =>
            {
                if (IsScanningBloat) return;
                IsScanningBloat = true;
                ShowProgressRequested?.Invoke(LocalizationManager.Get("StorageBloatScanning"));
                try
                {
                    var findings = await Task.Run(() => BloatDetector.Scan());
                    UpdateBloat(findings);
                    _lastUpdatedAt = DateTime.Now;
                    OnPropertyChanged(nameof(LastUpdatedDisplay));
                    ShowToastRequested?.Invoke(LocalizationManager.CurrentLanguage == "es" ? "Análisis de almacenamiento completado" : "Storage scan completed", NotificationType.Info);
                }
                finally
                {
                    IsScanningBloat = false;
                }
            });

            OpenStorageToolCommand = new RelayCommand(() => ToolLauncher.StartPCManager());

            OpenDriveCommand = new RelayCommand<DiskMetric>(drive =>
            {
                if (drive == null || string.IsNullOrWhiteSpace(drive.RootPath)) return;
                OpenInExplorer(drive.RootPath);
            });

            OpenPathCommand = new RelayCommand<string>(path =>
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                OpenInExplorer(path);
            });

            SetBloatFilterCommand = new RelayCommand<string>(mode =>
            {
                if (!string.IsNullOrWhiteSpace(mode))
                {
                    BloatFilterMode = mode;
                }
            });

            ScanFolderCommand = new AsyncRelayCommand<string>(async targetPath =>
            {
                string rootPath = string.IsNullOrWhiteSpace(targetPath)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    : targetPath;

                if (string.IsNullOrWhiteSpace(rootPath)) return;

                _folderScanCts?.Cancel();
                _folderScanCts?.Dispose();
                _folderScanCts = new CancellationTokenSource();
                var token = _folderScanCts.Token;

                IsScanningFolder = true;
                FolderScanEntries.Clear();
                OnPropertyChanged(nameof(HasFolderScanResults));
                FolderScanSkippedText = string.Empty;
                FolderScanStatusText = string.Format(LocalizationManager.Get("StorageScanRunning"), rootPath);
                FolderScanProgressText = string.Empty;

                var progress = new Progress<FolderScanProgress>(p =>
                {
                    string scanned = MetricFormatting.FormatBytesAuto(p.BytesScannedSoFar);
                    FolderScanProgressText = $"{scanned} · {p.DirectoriesVisited:N0} — {p.CurrentPath}";
                });

                try
                {
                    var result = await Task.Run(() => FolderSizeScanner.Scan(rootPath, 10, progress, token));

                    if (result.WasCancelled)
                    {
                        FolderScanStatusText = LocalizationManager.Get("StorageScanCancelled");
                        FolderScanProgressText = string.Empty;
                    }
                    else
                    {
                        FolderScanEntries = new ObservableCollection<FolderSizeEntry>(result.TopEntries);
                        OnPropertyChanged(nameof(HasFolderScanResults));

                        double seconds = result.ElapsedMilliseconds / 1000.0;
                        FolderScanStatusText = result.TopEntries.Count > 0
                            ? string.Format(LocalizationManager.Get("StorageScanDone"), MetricFormatting.FormatBytesAuto(result.TotalScannedBytes), result.TopEntries.Count, seconds)
                            : LocalizationManager.Get("StorageScanEmpty");
                        FolderScanProgressText = string.Empty;
                        FolderScanSkippedText = result.SkippedCount > 0
                            ? string.Format(LocalizationManager.Get("StorageScanSkipped"), result.SkippedCount)
                            : string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    CrashLogger.LogException("StorageViewModel.ScanFolder", ex, false);
                    FolderScanStatusText = LocalizationManager.Get("StorageScanEmpty");
                    FolderScanProgressText = string.Empty;
                }
                finally
                {
                    IsScanningFolder = false;
                }
            });

            CancelFolderScanCommand = new RelayCommand(() =>
            {
                try { _folderScanCts?.Cancel(); } catch { }
            });
        }

        private async Task RunDockerPruneAsync()
        {
            if (IsBusyCleaning) return;

            var answer = MessageBox.Show(
                LocalizationManager.Get("ConfirmDockerPruneBody"),
                LocalizationManager.Get("ConfirmDockerPruneTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;

            IsBusyCleaning = true;
            try
            {
                ShowProgressRequested?.Invoke(LocalizationManager.Get("ProgressDockerPrune"));
                var res = await Task.Run(() => BloatDetector.RunDockerPrune());
                if (res.Success)
                {
                    string msg = !string.IsNullOrWhiteSpace(res.ReclaimedSummary)
                        ? string.Format(LocalizationManager.Get("ToastDockerPruneDone"), res.ReclaimedSummary)
                        : LocalizationManager.Get("ToastDockerPruneDoneSimple");
                    ShowToastRequested?.Invoke(msg, NotificationType.Success);

                    var refreshedBloat = await Task.Run(() => BloatDetector.Scan(includeComponentStore: false));
                    UpdateBloat(refreshedBloat);
                    await TriggerPostActionRefreshAsync();
                }
                else
                {
                    ShowToastRequested?.Invoke(res.ErrorMessage, NotificationType.Warning);
                }
            }
            finally
            {
                IsBusyCleaning = false;
            }
        }

        private async Task CompactVhdxAsync(BloatFinding finding)
        {
            if (IsBusyCleaning || finding == null || !finding.CanCompactVhdx) return;

            string body = string.Format(LocalizationManager.Get("ConfirmCompactVhdxBody"), finding.Path);
            var answer = MessageBox.Show(
                body,
                LocalizationManager.Get("ConfirmCompactVhdxTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;

            IsBusyCleaning = true;
            try
            {
                ShowProgressRequested?.Invoke(LocalizationManager.Get("ProgressCompactVhdx"));
                var res = await Task.Run(() => BloatDetector.CompactVirtualDisk(finding.Path));
                if (res.Cancelled)
                {
                    ShowToastRequested?.Invoke(LocalizationManager.Get("ToastCompactVhdxCancelled"), NotificationType.Info);
                }
                else if (res.Success)
                {
                    string msg = res.ReclaimedBytes > 0
                        ? string.Format(LocalizationManager.Get("ToastCompactVhdxDone"), res.ReclaimedSummary)
                        : LocalizationManager.Get("ToastCompactVhdxNoChange");
                    ShowToastRequested?.Invoke(msg, NotificationType.Success);

                    var refreshedBloat = await Task.Run(() => BloatDetector.Scan(includeComponentStore: false));
                    UpdateBloat(refreshedBloat);
                    await TriggerPostActionRefreshAsync();
                }
                else
                {
                    ShowToastRequested?.Invoke(string.Format(LocalizationManager.Get("ToastCompactVhdxFailed"), res.ErrorMessage), NotificationType.Warning);
                }
            }
            finally
            {
                IsBusyCleaning = false;
            }
        }

        private async Task TriggerPostActionRefreshAsync()
        {
            if (RefreshRequested != null)
            {
                await RefreshRequested.Invoke();
            }
            _lastUpdatedAt = DateTime.Now;
            OnPropertyChanged(nameof(LastUpdatedDisplay));
        }

        public void UpdateDrives(List<DiskMetric> drives)
        {
            var incoming = drives ?? new List<DiskMetric>();
            bool changed = _drives.Count != incoming.Count;
            if (!changed)
            {
                for (int i = 0; i < incoming.Count; i++)
                {
                    if (!ReferenceEquals(_drives[i], incoming[i]))
                    {
                        changed = true;
                        break;
                    }
                }
            }

            _drives.SyncInPlace(incoming);

            if (changed || !_lastUpdatedAt.HasValue)
            {
                _lastUpdatedAt = DateTime.Now;
                NotifyDriveSummariesChanged();
            }
        }

        public void UpdateBloat(List<BloatFinding> findings)
        {
            _allBloatFindings = (findings ?? new List<BloatFinding>())
                .FindAll(f => !string.Equals(f.Category, "RecycleBin", StringComparison.OrdinalIgnoreCase));
            ApplyBloatFilter();
        }

        public void Update(List<DiskMetric> drives, List<BloatFinding> findings)
        {
            UpdateDrives(drives);
            UpdateBloat(findings);
        }

        private void ApplyBloatFilter()
        {
            IEnumerable<BloatFinding> query = _allBloatFindings;
            if (IsFilterCleanable)
            {
                query = query.Where(f => f.IsCleanable);
            }
            else if (IsFilterSystem)
            {
                query = query.Where(f => !f.IsCleanable);
            }

            BloatFindings = new ObservableCollection<BloatFinding>(query);
            OnPropertyChanged(nameof(HasNoBloatFindings));
            OnPropertyChanged(nameof(HasBloatItems));
            OnPropertyChanged(nameof(EmptyBloatText));
            OnPropertyChanged(nameof(ReclaimableSizeDisplay));
            OnPropertyChanged(nameof(BloatSummarySubtitle));
            OnPropertyChanged(nameof(BloatCountDisplay));
        }

        private void NotifyDriveSummariesChanged()
        {
            OnPropertyChanged(nameof(TotalCapacityGB));
            OnPropertyChanged(nameof(TotalUsedGB));
            OnPropertyChanged(nameof(TotalFreeGB));
            OnPropertyChanged(nameof(OverallPercentUsed));
            OnPropertyChanged(nameof(TotalCapacityDisplay));
            OnPropertyChanged(nameof(TotalUsedDisplay));
            OnPropertyChanged(nameof(TotalFreeDisplay));
            OnPropertyChanged(nameof(OverallPercentDisplay));
            OnPropertyChanged(nameof(DrivesCountDisplay));
            OnPropertyChanged(nameof(LastUpdatedDisplay));
        }

        private static string FormatGB(double gb)
        {
            if (gb >= 1024.0)
            {
                return $"{gb / 1024.0:F2} TB";
            }
            return $"{gb:F1} GB";
        }

        private static void OpenInExplorer(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{path}\"",
                        UseShellExecute = true
                    });
                }
                else if (Directory.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                CrashLogger.LogException("StorageViewModel.OpenInExplorer", ex, false);
            }
        }
    }
}