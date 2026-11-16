using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SystemCoreMonitor.Core;
using SystemCoreMonitor.Core.Mvvm;
using SystemCoreMonitor.Models;
using SystemCoreMonitor.Modules;

namespace SystemCoreMonitor.ViewModels
{
    public class StartupViewModel : ViewModelBase
    {
        private ObservableCollection<StartupItem> _startupItems = new();
        private List<StartupItem> _allStartupItems = new();
        private string _searchFilter = string.Empty;

        public event Action<string, NotificationType>? ShowToastRequested;

        public ObservableCollection<StartupItem> StartupItems
        {
            get => _startupItems;
            set => SetProperty(ref _startupItems, value);
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
        public string HeaderTitleText => LocalizationManager.Get("StartupHeaderTitle");
        public string HeaderDescText => LocalizationManager.Get("StartupHeaderDesc");
        public string OpenSettingsText => LocalizationManager.Get("StartupOpenSettings");
        public string SearchPlaceholderText => LocalizationManager.Get("StartupSearchPlaceholder");
        public string DisableBtnText => LocalizationManager.Get("StartupDisableBtn");
        public string EnableBtnText => LocalizationManager.Get("StartupEnableBtn");
        public string InvestigateBtnText => LocalizationManager.Get("StartupInvestigateBtn");

        public RelayCommand<StartupItem> SearchOnlineCommand { get; }
        public RelayCommand<StartupItem> ToggleStartupCommand { get; }
        public RelayCommand OpenStartupSettingsCommand { get; }
        public RelayCommand ClearSearchCommand { get; }

        public StartupViewModel()
        {
            LocalizationManager.LanguageChanged += () => OnPropertyChanged(string.Empty);
            ClearSearchCommand = new RelayCommand(() => SearchFilter = string.Empty);

            SearchOnlineCommand = new RelayCommand<StartupItem>(item =>
            {
                if (item != null)
                {
                    ProcessManager.SearchProcessOnline(item.Name, "windows startup program");
                }
            });

            ToggleStartupCommand = new RelayCommand<StartupItem>(item =>
            {
                if (item == null) return;
                bool newState = !item.IsEnabled;
                bool success = StartupCollector.SetStartupItemEnabled(item.Name, item.LocationType, newState);
                bool isEs = LocalizationManager.CurrentLanguage == "es";
                if (success)
                {
                    item.IsEnabled = newState;
                    item.Status = newState
                        ? (isEs ? "✓ Habilitado" : "✓ Enabled")
                        : (isEs ? "✕ Deshabilitado" : "✕ Disabled");

                    string appName = !string.IsNullOrWhiteSpace(item.DisplayName) ? item.DisplayName : item.Name;
                    string msg = isEs
                        ? $"{(newState ? "Habilitado" : "Deshabilitado")}: {appName}"
                        : $"{(newState ? "Enabled" : "Disabled")}: {appName}";
                    ShowToastRequested?.Invoke(msg, NotificationType.Success);
                }
                else
                {
                    string errMsg = isEs
                        ? $"No se pudo modificar {item.Name} (posiblemente requiere elevación)"
                        : $"Could not modify {item.Name} (may require elevation)";
                    ShowToastRequested?.Invoke(errMsg, NotificationType.Error);
                }
            });

            OpenStartupSettingsCommand = new RelayCommand(() =>
            {
                ToolLauncher.StartStartupSettings();
                bool isEs = LocalizationManager.CurrentLanguage == "es";
                ShowToastRequested?.Invoke(isEs ? "Abriendo Configuración de Inicio de Windows..." : "Opening Windows Startup Settings...", NotificationType.Info);
            });
        }

        public void Update(List<StartupItem> items)
        {
            _allStartupItems = items ?? new();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var filtered = string.IsNullOrWhiteSpace(_searchFilter)
                ? _allStartupItems
                : _allStartupItems.Where(i =>
                    (i.DisplayName?.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (i.Name?.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (i.Publisher?.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (i.Command?.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

            _startupItems.SyncInPlace(filtered);
        }
    }
}