using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using SystemCoreMonitor.Core;
using SystemCoreMonitor.Core.Mvvm;
using SystemCoreMonitor.Models;

namespace SystemCoreMonitor.ViewModels
{
    public class StorageViewModel : ViewModelBase
    {
        private ObservableCollection<DiskMetric> _drives = new();
        private ObservableCollection<BloatFinding> _bloatFindings = new();

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

        public AsyncRelayCommand CleanTempCommand { get; }
        public AsyncRelayCommand EmptyRecycleBinCommand { get; }
        public AsyncRelayCommand<BloatFinding> ExecuteBloatActionCommand { get; }
        public AsyncRelayCommand ScanBloatCommand { get; }

        private bool _isScanningBloat;
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

        public bool HasNoBloatFindings => !IsScanningBloat && BloatFindings.Count == 0;
        public string CleanTempBtnText => LocalizationManager.Get("StorageCleanTempBtn");
        public string EmptyBinBtnText => LocalizationManager.Get("StorageEmptyBinBtn");
        public string ScanButtonText => LocalizationManager.Get(IsScanningBloat ? "StorageScanningBtn" : "StorageScanBloatBtn");
        public string DrivesTitleText => LocalizationManager.Get("StorageDrivesTitle");
        public string BloatTitleText => LocalizationManager.Get("StorageBloatTitle");
        public string EmptyBloatText => LocalizationManager.Get("StorageEmptyBloat");

        public event Action<string, NotificationType>? ShowToastRequested;
        public event Action<string>? ShowProgressRequested;

        public StorageViewModel()
        {
            LocalizationManager.LanguageChanged += () => OnPropertyChanged(string.Empty);

            CleanTempCommand = new AsyncRelayCommand(async () =>
            {
                ShowProgressRequested?.Invoke(LocalizationManager.Get("ActionCleaningTemp"));
                var res = await Task.Run(() => SafeTempCleaner.CleanDeepStorage(false));
                ShowToastRequested?.Invoke(string.Format(LocalizationManager.Get("ToastTempCleaned"), res.HumanSize, res.FilesDeleted), NotificationType.Success);
            });

            EmptyRecycleBinCommand = new AsyncRelayCommand(async () =>
            {
                ShowProgressRequested?.Invoke(LocalizationManager.Get("ActionEmptyingRecycleBin"));
                await Task.Run(() => NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null, NativeMethods.SHERB_NOCONFIRMATION | NativeMethods.SHERB_NOPROGRESSUI));
                ShowToastRequested?.Invoke(LocalizationManager.Get("ToastRecycleBinEmptied"), NotificationType.Success);
            });

            ExecuteBloatActionCommand = new AsyncRelayCommand<BloatFinding>(async finding =>
            {
                if (finding == null) return;
                if (finding.ActionKind == "SafeDelete")
                {
                    if (finding.Category == "RecycleBin")
                    {
                        ShowProgressRequested?.Invoke(LocalizationManager.Get("ActionEmptyingRecycleBin"));
                        await Task.Run(() => NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null, NativeMethods.SHERB_NOCONFIRMATION | NativeMethods.SHERB_NOPROGRESSUI));
                        BloatFindings.Remove(finding);
                        OnPropertyChanged(nameof(HasNoBloatFindings));
                        ShowToastRequested?.Invoke(LocalizationManager.Get("ToastRecycleBinEmptied"), NotificationType.Success);
                    }
                    else
                    {
                        ShowProgressRequested?.Invoke(string.Format("Limpiando caché {0}...", finding.Category));
                        var res = await Task.Run(() => BloatDetector.DeleteWhitelistedCache(finding.Path));
                        ShowToastRequested?.Invoke(string.Format(LocalizationManager.Get("ToastTempCleaned"), res.HumanSize, res.FilesDeleted), NotificationType.Success);
                    }
                }
                else if (finding.ActionKind == "LaunchTool")
                {
                    ToolLauncher.StartPCManager();
                }
            });

            ScanBloatCommand = new AsyncRelayCommand(async () =>
            {
                if (IsScanningBloat) return;
                IsScanningBloat = true;
                ShowProgressRequested?.Invoke(LocalizationManager.CurrentLanguage == "es" ? "Analizando cachés y archivos voluminosos..." : "Scanning bloat and cache files...");
                try
                {
                    var findings = await Task.Run(() => BloatDetector.Scan());
                    UpdateBloat(findings);
                    ShowToastRequested?.Invoke(LocalizationManager.CurrentLanguage == "es" ? "Análisis de almacenamiento completado" : "Storage scan completed", NotificationType.Info);
                }
                finally
                {
                    IsScanningBloat = false;
                }
            });
        }

        public void UpdateDrives(List<DiskMetric> drives)
        {
            _drives.SyncInPlace(drives ?? new());
        }

        public void UpdateBloat(List<BloatFinding> findings)
        {
            var filtered = (findings ?? new())
                .FindAll(f => !string.Equals(f.Category, "RecycleBin", StringComparison.OrdinalIgnoreCase));
            BloatFindings = new ObservableCollection<BloatFinding>(filtered);
            OnPropertyChanged(nameof(HasNoBloatFindings));
        }

        public void Update(List<DiskMetric> drives, List<BloatFinding> findings)
        {
            UpdateDrives(drives);
            UpdateBloat(findings);
        }
    }
}