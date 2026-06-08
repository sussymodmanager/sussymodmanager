using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Models;
using SussyModManager.Core.Platform;
using SussyModManager.Core.Services;
using SussyModManager.Services;

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
        [ObservableProperty] private string _statusText;
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

        public void SetStatus(string message)
        {
            StatusText = message;
            StatusIsError = LooksLikeError(message);
        }

        /// <summary>Refresh all pages after the onboarding wizard installs the pack / sets the path.</summary>
        public void OnWizardCompleted()
        {
            Settings.SetPath(_env.Config.AmongUsPath);
            Settings.ReloadProfiles();
            Store.RefreshStates();
            Installed.Reload();
            Presets.Reload();
            Installed.RefreshPackMode();
            _env.SetStatus("All set. Welcome to SUSSYMODMANAGER!");
        }

        public MainWindowViewModel(Config config, ColorProfileService profiles)
        {
            NeedsWizard = !config.FirstLaunchWizardCompleted;
            _env = new AppEnvironment(config, profiles);
            _env.Manager.ReconcileInstalledMods();
            _env.Manager.SetLaunchSelection(config.SelectedMods, syncPlugins: false);

            _env.StatusChanged += (_, message) => SetStatus(message);
            _env.NavigationRequested += (_, tab) => Navigate(tab);
            _env.PackSelectionChanged += (_, _) =>
            {
                Installed.RefreshPackMode();
                Installed.RefreshCardStates();
                Presets.RefreshInstallCounts();
                SetStatus(_env.GetIdleStatus());
            };
            _env.ModLibraryChanged += (_, _) => RefreshModLibraryUi();
            _env.StoreCatalogRefreshed += (_, _) => OnStoreCatalogRefreshed();

            Store = new StoreViewModel(_env);
            Installed = new InstalledViewModel(_env);
            Presets = new PresetsViewModel(_env);
            Settings = new SettingsViewModel(_env);

            CurrentPage = Store;
            SetStatus(_env.GetIdleStatus());

            if (string.IsNullOrEmpty(config.AmongUsPath))
                _ = AutoDetectGameAsync();
            else if (config.LegacyModsCopyPending)
                SetStatus("Importing mods from BeanModManager...");

            _ = CheckForAppUpdateAsync();
            _ = RunStartupTasksAsync();
        }

        private async Task RunStartupTasksAsync()
        {
            await ShowProtonWarningIfNeededAsync().ConfigureAwait(true);

            if (!_env.Config.AutoUpdateMods)
                return;

            try
            {
                var updates = await _env.CheckModUpdatesIfEnabledAsync().ConfigureAwait(true);
                ApplyModUpdateBadges(updates);
            }
            catch (Exception ex)
            {
                SetStatus($"Mod update check failed: {ex.Message}");
            }
        }

        private async Task ShowProtonWarningIfNeededAsync()
        {
            if (!ProtonReminder.ShouldShow(_env.Config))
                return;

            var dismiss = await DialogService.ConfirmAsync(
                "Steam / Proton launch options",
                "On Linux and macOS, Among Us must launch through Steam with compatible Proton or Wine settings, or BepInEx mods may fail to load.\n\n" +
                "If mods break after a game update, reinstall BepInEx from Settings and check the BepInEx log.\n\n" +
                "Don't show this reminder again?",
                yes: "Got it", no: "Remind me later").ConfigureAwait(true);

            if (dismiss)
            {
                ProtonReminder.DismissPermanently(_env.Config);
                Settings.RefreshProtonReminder();
                return;
            }

            ProtonReminder.Snooze(_env.Config);
            Settings.RefreshProtonReminder();
        }

        public void RefreshModLibraryUi()
        {
            Installed.Reload();
            Installed.RefreshPackMode();
            Presets.Reload();
            Store.RefreshStates();
        }

        private void ApplyModUpdateBadges(System.Collections.Generic.IReadOnlyList<ModUpdateInfo> updates)
        {
            if (updates == null)
                return;

            Store.ApplyUpdateInfo(updates);

            foreach (var card in Installed.Installed)
            {
                if (card.IsCustom)
                    continue;
                var info = updates.FirstOrDefault(u =>
                    string.Equals(u.ModId, card.Id, StringComparison.OrdinalIgnoreCase));
                if (info != null)
                {
                    card.HasUpdate = info.HasUpdate;
                    card.LatestVersion = info.LatestVersion;
                }
            }
        }

        private async System.Threading.Tasks.Task AutoDetectGameAsync()
        {
            SetStatus("Looking for Among Us...");
            try
            {
                var detected = await AppEnvironment.DetectGameAsync(includeHeavyProbes: false).ConfigureAwait(true);
                var status = _env.ApplyAutoDetectedGame(detected);
                if (detected != null && !string.IsNullOrEmpty(detected.Path))
                {
                    Settings.SetPath(detected.Path);
                    Settings.SelectedChannel = _env.Config.GameChannel;
                }
                SetStatus(status);
            }
            catch
            {
                SetStatus("Welcome! Set your Among Us path in Settings to get started.");
            }
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

        public void OnStoreDataRefreshed() => OnStoreCatalogRefreshed(showStatus: true);

        public void OnStoreCatalogRefreshed(bool showStatus = false)
        {
            _env.Manager.Store.Reload();
            Store.Reload();
            Presets.Reload();
            Settings.RefreshInteropStatus();
            if (showStatus)
                _env.SetStatus("Mod store updated from GitHub.");
        }

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
                    if (_env.Config.AutoUpdateMods)
                        _ = Store.RefreshUpdateBadgesAsync();
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
                    Settings.RefreshInteropStatus();
                    CurrentPage = Settings;
                    break;
            }
        }
    }
}
