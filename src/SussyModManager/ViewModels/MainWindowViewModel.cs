using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SussyModManager.Core.Models;
using SussyModManager.Core.Services;

namespace SussyModManager.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly AppEnvironment _env;

        public StoreViewModel Store { get; }
        public InstalledViewModel Installed { get; }
        public PresetsViewModel Presets { get; }
        public SettingsViewModel Settings { get; }

        [ObservableProperty] private ViewModelBase _currentPage;
        [ObservableProperty] private string _statusText = "Ready.";
        [ObservableProperty] private bool _statusIsError;
        [ObservableProperty] private string _activeTab = "store";

        [ObservableProperty] private bool _updateAvailable;
        [ObservableProperty] private string _updateText;
        [ObservableProperty] private bool _updateStaged;
        private AppUpdateInfo _appUpdate;
        private readonly AppUpdateService _updateService = new AppUpdateService();

        public string AppTitle => "SUSSYMODMANAGER";
        public string AppTagline => "the sussiest mod manager alive";
        public string AppVersion => $"v{SussyModManager.Core.AppInfo.Version}";

        public AppEnvironment Environment => _env;

        /// <summary>True on the very first run, until the onboarding wizard is finished/skipped.</summary>
        public bool NeedsWizard { get; }

        public WizardViewModel CreateWizard() => new WizardViewModel(_env);

        /// <summary>Refresh all pages after the onboarding wizard installs the pack / sets the path.</summary>
        public void OnWizardCompleted()
        {
            Settings.SetPath(_env.Config.AmongUsPath);
            Settings.ReloadProfiles();
            Store.RefreshStates();
            Installed.Reload();
            Presets.Reload();
            _env.SetStatus("All set. Welcome to SUSSYMODMANAGER!");
        }

        public MainWindowViewModel(Config config, ColorProfileService profiles)
        {
            NeedsWizard = !config.FirstLaunchWizardCompleted;
            _env = new AppEnvironment(config, profiles);
            _env.StatusChanged += (_, message) =>
            {
                StatusText = message;
                StatusIsError = LooksLikeError(message);
            };
            _env.NavigationRequested += (_, tab) => Navigate(tab);

            Store = new StoreViewModel(_env);
            Installed = new InstalledViewModel(_env);
            Presets = new PresetsViewModel(_env);
            Settings = new SettingsViewModel(_env);

            CurrentPage = Store;

            if (string.IsNullOrEmpty(config.AmongUsPath))
            {
                var detected = AmongUsLocator.Detect();
                if (!string.IsNullOrEmpty(detected))
                {
                    config.AmongUsPath = detected;
                    config.Save();
                    Settings.SetPath(detected);
                    StatusText = $"Detected Among Us at {detected}";
                }
                else
                {
                    StatusText = "Welcome! Set your Among Us path in Settings to get started.";
                }
            }

            _ = CheckForAppUpdateAsync();
        }

        private async System.Threading.Tasks.Task CheckForAppUpdateAsync()
        {
            try
            {
                _appUpdate = await _updateService.CheckAsync().ConfigureAwait(true);
                if (_appUpdate?.UpdateAvailable != true)
                    return;

                UpdateText = $"Update available: v{_appUpdate.LatestVersion} (you have {AppVersion})";
                UpdateAvailable = true;

                // Automatic path: silently download + stage, then offer a one-click restart.
                if (_env.Config.AutoUpdateApp && _appUpdate.CanAutoApply)
                {
                    UpdateText = $"Downloading update v{_appUpdate.LatestVersion}...";
                    var staged = await _updateService
                        .DownloadAndStageAsync(_appUpdate, new Progress<int>(p => UpdateText = $"Downloading update v{_appUpdate.LatestVersion}... {p}%"))
                        .ConfigureAwait(true);
                    if (staged)
                    {
                        UpdateStaged = true;
                        UpdateText = $"Update v{_appUpdate.LatestVersion} ready - restart to finish (or it installs next launch).";
                    }
                    else
                    {
                        UpdateText = $"Update available: v{_appUpdate.LatestVersion} (you have {AppVersion})";
                    }
                }
            }
            catch
            {
            }
        }

        private static bool LooksLikeError(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;
            return message.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("fail", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("couldn't", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("could not", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("warning", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void OnStoreDataRefreshed()
        {
            _env.Manager.Store.Reload();
            Store.Reload();
            Presets.Reload();
            _env.SetStatus("Mod store updated from GitHub.");
        }

        /// <summary>Either restart to apply a staged update, or open the download page.</summary>
        [RelayCommand]
        private void DownloadUpdate()
        {
            if (UpdateStaged && AppUpdateService.TryApplyPendingUpdate())
            {
                System.Environment.Exit(0);
                return;
            }
            _updateService.OpenDownload(_appUpdate);
        }

        [RelayCommand]
        private void DismissUpdate() => UpdateAvailable = false;

        [RelayCommand]
        private void Navigate(string tab)
        {
            ActiveTab = tab;
            switch (tab)
            {
                case "store":
                    Store.RefreshStates();
                    CurrentPage = Store;
                    break;
                case "installed":
                    Installed.Reload();
                    CurrentPage = Installed;
                    break;
                case "presets":
                    Presets.Reload();
                    CurrentPage = Presets;
                    break;
                case "settings":
                    Settings.ReloadProfiles();
                    CurrentPage = Settings;
                    break;
            }
        }
    }
}
