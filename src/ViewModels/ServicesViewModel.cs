using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SystemCoreMonitor.Core;
using SystemCoreMonitor.Core.Mvvm;
using SystemCoreMonitor.Models;
using SystemCoreMonitor.Modules;

namespace SystemCoreMonitor.ViewModels
{
    public class ServicesViewModel : ViewModelBase
    {
        private ServiceMetric _metric = new();
        private ObservableCollection<ServiceItem> _services = new();
        private List<ServiceItem> _allServices = new();
        private string _searchFilter = string.Empty;

        public ServiceMetric Metric
        {
            get => _metric;
            set => SetProperty(ref _metric, value);
        }

        public ObservableCollection<ServiceItem> Services
        {
            get => _services;
            set => SetProperty(ref _services, value);
        }

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (SetProperty(ref _searchFilter, value))
                {
                    OnPropertyChanged(nameof(HasSearchFilter));
                    ApplyFilter();
                }
            }
        }

        public bool HasSearchFilter => !string.IsNullOrEmpty(_searchFilter);
        public string HeaderTitleText => LocalizationManager.Get("SvcHeaderTitle");
        public string HeaderDescText => LocalizationManager.Get("SvcHeaderDesc");
        public string RunningDisplay => string.Format(LocalizationManager.Get("SvcRunningLabel"), Metric.RunningCount);
        public string StoppedDisplay => string.Format(LocalizationManager.Get("SvcStoppedLabel"), Metric.StoppedCount);
        public string OpenConsoleText => LocalizationManager.Get("SvcOpenConsole");
        public string SearchPlaceholderText => LocalizationManager.Get("SvcSearchPlaceholder");
        public string StartBtnText => LocalizationManager.Get("SvcStartBtn");
        public string StopBtnText => LocalizationManager.Get("SvcStopBtn");
        public string RestartBtnText => LocalizationManager.Get("SvcRestartBtn");

        public AsyncRelayCommand<ServiceItem> StartServiceCommand { get; }
        public AsyncRelayCommand<ServiceItem> StopServiceCommand { get; }
        public AsyncRelayCommand<ServiceItem> RestartServiceCommand { get; }
        public RelayCommand OpenServicesConsoleCommand { get; }
        public RelayCommand ClearSearchCommand { get; }

        public event Action<string, NotificationType>? ShowToastRequested;

        public ServicesViewModel()
        {
            LocalizationManager.LanguageChanged += () => OnPropertyChanged(string.Empty);
            ClearSearchCommand = new RelayCommand(() => SearchFilter = string.Empty);

            OpenServicesConsoleCommand = new RelayCommand(() =>
            {
                ToolLauncher.StartServicesConsole();
                ShowToastRequested?.Invoke(LocalizationManager.CurrentLanguage == "es"
                    ? "Abriendo Consola de Servicios de Windows..."
                    : "Opening Windows Services Console...", NotificationType.Info);
            });

            StartServiceCommand = new AsyncRelayCommand<ServiceItem>(async svc =>
            {
                if (svc == null) return;
                bool ok = await Task.Run(() => ServiceCollector.StartService(svc.ServiceName));
                if (ok)
                {
                    await RefreshServicesNowAsync();
                    ShowToastRequested?.Invoke(
                        string.Format(LocalizationManager.Get("ToastServiceStarted"), svc.DisplayName),
                        NotificationType.Success);
                }
                else
                {
                    ShowToastRequested?.Invoke(
                        string.Format(LocalizationManager.Get("ToastServiceStartFailed"), svc.DisplayName),
                        NotificationType.Error);
                }
            });

            StopServiceCommand = new AsyncRelayCommand<ServiceItem>(async svc =>
            {
                if (svc == null) return;
                string prompt = string.Format(LocalizationManager.Get("ConfirmStopSvcBody"), svc.DisplayName, svc.ServiceName);
                var answer = MessageBox.Show(prompt, LocalizationManager.Get("ConfirmStopSvcTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (answer != MessageBoxResult.Yes) return;

                bool ok = await Task.Run(() => ServiceCollector.StopService(svc.ServiceName));
                if (ok)
                {
                    await RefreshServicesNowAsync();
                    ShowToastRequested?.Invoke(
                        string.Format(LocalizationManager.Get("ToastServiceStopped"), svc.DisplayName),
                        NotificationType.Success);
                }
                else
                {
                    ShowToastRequested?.Invoke(
                        string.Format(LocalizationManager.Get("ToastServiceStopFailed"), svc.DisplayName),
                        NotificationType.Error);
                }
            });

            RestartServiceCommand = new AsyncRelayCommand<ServiceItem>(async svc =>
            {
                if (svc == null) return;
                bool ok = await Task.Run(() => ServiceCollector.RestartService(svc.ServiceName));
                if (ok)
                {
                    await RefreshServicesNowAsync();
                    ShowToastRequested?.Invoke(
                        string.Format(LocalizationManager.Get("ToastServiceStarted"), svc.DisplayName),
                        NotificationType.Success);
                }
                else
                {
                    ShowToastRequested?.Invoke(
                        string.Format(LocalizationManager.Get("ToastServiceRestartFailed"), svc.DisplayName),
                        NotificationType.Error);
                }
            });
        }

        private async Task RefreshServicesNowAsync()
        {
            var collector = new ServiceCollector();
            var fresh = await Task.Run(() => collector.Sample(forceRefresh: true));
            Update(fresh);
        }

        public void Update(ServiceMetric metric)
        {
            Metric = metric;
            _allServices = metric.CriticalServices ?? new();
            OnPropertyChanged(nameof(RunningDisplay));
            OnPropertyChanged(nameof(StoppedDisplay));
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var filtered = string.IsNullOrWhiteSpace(_searchFilter)
                ? _allServices
                : _allServices.Where(s =>
                    (s.DisplayName?.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (s.ServiceName?.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

            _services.SyncInPlace(filtered);
        }
    }
}