using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SystemCoreMonitor.Core;
using SystemCoreMonitor.Core.Mvvm;
using SystemCoreMonitor.Models;
using SystemCoreMonitor.Modules;

namespace SystemCoreMonitor.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private CpuMetric _cpu = new();
        private MemoryMetric _memory = new();
        private List<DiskMetric> _drives = new();
        private NetworkMetric _network = new();
        private GpuMetric _gpu = new();
        private NpuMetric _npu = new();

        public CpuMetric Cpu
        {
            get => _cpu;
            set => SetProperty(ref _cpu, value);
        }

        public MemoryMetric Memory
        {
            get => _memory;
            set => SetProperty(ref _memory, value);
        }

        public List<DiskMetric> Drives
        {
            get => _drives;
            set => SetProperty(ref _drives, value);
        }

        public NetworkMetric Network
        {
            get => _network;
            set => SetProperty(ref _network, value);
        }

        public GpuMetric Gpu
        {
            get => _gpu;
            set => SetProperty(ref _gpu, value);
        }

        public NpuMetric Npu
        {
            get => _npu;
            set => SetProperty(ref _npu, value);
        }

        private string _currentPowerPlan = "Balanced";
        public string CurrentPowerPlan
        {
            get => _currentPowerPlan;
            set => SetProperty(ref _currentPowerPlan, value);
        }

        public AsyncRelayCommand OptimizeMemoryCommand { get; }
        public AsyncRelayCommand CleanTempCommand { get; }
        public AsyncRelayCommand FlushDnsCommand { get; }
        public AsyncRelayCommand CopySnapshotCommand { get; }
        public RelayCommand SwitchPowerPlanCommand { get; }
        public RelayCommand LaunchTaskMgrCommand { get; }
        public RelayCommand LaunchResMonCommand { get; }

        public event Action<string, NotificationType>? ShowToastRequested;
        public event Action<string>? ShowProgressRequested;
        public event Func<Task<bool>>? CopySnapshotRequested;

        public string TrimRamText => LocalizationManager.Get("TrimRam");
        public string TrimRamTooltip => LocalizationManager.Get("TrimRamTooltip");
        public string SnapshotText => LocalizationManager.Get("Snapshot");
        public string SnapshotTooltip => LocalizationManager.Get("SnapshotTooltip");
        public string FlushDnsText => LocalizationManager.Get("FlushDns");
        public string FlushDnsTooltip => LocalizationManager.Get("FlushDnsTooltip");
        public string PlanLabelText => LocalizationManager.Get("DashPlanLabel");
        public string PlanSaverText => LocalizationManager.Get("PowerPlanSaver");
        public string PlanBalancedText => LocalizationManager.Get("PowerPlanBalanced");
        public string PlanHighPerfText => LocalizationManager.Get("PowerPlanHighPerf");
        public string CpuTitleText => LocalizationManager.Get("DashCpuTitle");
        public string RamTitleText => LocalizationManager.Get("DashRamTitle");
        public string GpuTitleText => LocalizationManager.Get("DashGpuTitle");
        public string NetTitleText => LocalizationManager.Get("DashNetTitle");
        public string VramLabelText => LocalizationManager.Get("DashVramLabel");
        public string Engine3DText => LocalizationManager.Get("DashEngine3D");
        public string EngineComputeText => LocalizationManager.Get("DashEngineCompute");
        public string EngineVideoText => LocalizationManager.Get("DashEngineVideo");
        public string NpuChipText => LocalizationManager.Get("DashNpuChip");

        public DashboardViewModel()
        {
            PowerPlanManager.GetActiveScheme(out string initialScheme);
            _currentPowerPlan = MapSchemeToKey(initialScheme);

            LocalizationManager.LanguageChanged += () => OnPropertyChanged(string.Empty);

            OptimizeMemoryCommand = new AsyncRelayCommand(async () =>
            {
                ShowProgressRequested?.Invoke(LocalizationManager.Get("ActionOptimizingRam"));
                int trimmed = 0;
                double freedMB = await Task.Run(() => MemoryOptimizer.OptimizeWorkingSet(out trimmed));
                ShowToastRequested?.Invoke(string.Format(LocalizationManager.Get("ToastMemoryOptimized"), freedMB, trimmed), NotificationType.Success);
            });

            CleanTempCommand = new AsyncRelayCommand(async () =>
            {
                ShowProgressRequested?.Invoke(LocalizationManager.Get("ActionCleaningTemp"));
                var result = await Task.Run(() => SafeTempCleaner.CleanDeepStorage(false));
                ShowToastRequested?.Invoke(string.Format(LocalizationManager.Get("ToastTempCleaned"), result.HumanSize, result.FilesDeleted), NotificationType.Success);
            });

            FlushDnsCommand = new AsyncRelayCommand(async () =>
            {
                ShowProgressRequested?.Invoke(LocalizationManager.Get("ActionFlushingDns"));
                bool ok = await Task.Run(() => NetworkCollector.FlushDnsCache());
                if (ok)
                {
                    ShowToastRequested?.Invoke(LocalizationManager.Get("ToastDnsFlushed"), NotificationType.Success);
                }
                else
                {
                    ShowToastRequested?.Invoke(LocalizationManager.Get("ToastDnsFailed"), NotificationType.Error);
                }
            });

            CopySnapshotCommand = new AsyncRelayCommand(async () =>
            {
                if (CopySnapshotRequested == null) return;
                bool ok = await CopySnapshotRequested.Invoke();
                ShowToastRequested?.Invoke(
                    LocalizationManager.Get(ok ? "ToastSnapshotCopied" : "SnapshotFailed"),
                    ok ? NotificationType.Success : NotificationType.Error);
            });

            SwitchPowerPlanCommand = new RelayCommand(param =>
            {
                if (param is string planName)
                {
                    if (planName == "Saver") PowerPlanManager.SetScheme(PowerSchemeMode.PowerSaver);
                    else if (planName == "HighPerf") PowerPlanManager.SetScheme(PowerSchemeMode.HighPerformance);
                    else PowerPlanManager.SetScheme(PowerSchemeMode.Balanced);

                    CurrentPowerPlan = planName;
                    ShowToastRequested?.Invoke(string.Format(LocalizationManager.Get("ToastPlanChanged"), planName), NotificationType.Success);
                }
            });

            LaunchTaskMgrCommand = new RelayCommand(() => ToolLauncher.StartTaskManager());
            LaunchResMonCommand = new RelayCommand(() => ToolLauncher.StartResourceMonitor());
        }

        public void Update(CpuMetric cpu, MemoryMetric mem, List<DiskMetric> drives, NetworkMetric net, GpuMetric gpu, NpuMetric npu)
        {
            Cpu = cpu;
            Memory = mem;
            Drives = drives;
            Network = net;
            Gpu = gpu;
            Npu = npu;

            PowerPlanManager.GetActiveScheme(out string activeScheme);
            string mapped = MapSchemeToKey(activeScheme);
            if (CurrentPowerPlan != mapped) CurrentPowerPlan = mapped;
        }

        private static string MapSchemeToKey(string scheme) => scheme switch
        {
            "Power Saver" => "Saver",
            "High Performance" => "HighPerf",
            _ => "Balanced"
        };
    }
}