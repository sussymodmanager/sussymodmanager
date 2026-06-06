using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SussyModManager.Core;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Models;
using SussyModManager.Core.Platform;
using SussyModManager.Core.Services;
using SussyModManager.Services;

namespace SussyModManager.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly AppEnvironment _env;

        public ObservableCollection<ColorProfileViewModel> Profiles { get; } = new ObservableCollection<ColorProfileViewModel>();
        public ObservableCollection<string> Channels { get; } =
            new ObservableCollection<string>(GameChannels.All);

        [ObservableProperty] private string _amongUsPath;
        [ObservableProperty] private string _selectedChannel;
        [ObservableProperty] private bool _showBetaVersions;
        [ObservableProperty] private string _bepInExStatus;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _platformLabel;
        [ObservableProperty] private bool _armDangerZone;
        [ObservableProperty] private bool _autoUpdateApp;
        [ObservableProperty] private string _appVersionLabel;
        [ObservableProperty] private string _updateStatus;

        private readonly AppUpdateService _appUpdates = new AppUpdateService();

        public ThemeEditorViewModel Editor { get; } = new ThemeEditorViewModel();

        public string Title => "Settings";
        public string Subtitle => "Game location, channel and the all-important looks.";

        public SettingsViewModel(AppEnvironment env)
        {
            _env = env;
            Editor.LoadFrom(env.Profiles.GetProfileOrDefault(env.Config.ActiveColorProfileId));
            AmongUsPath = env.Config.AmongUsPath;
            SelectedChannel = env.Config.GameChannel ?? GameChannels.Steam;
            ShowBetaVersions = env.Config.ShowBetaVersions;
            AutoUpdateApp = env.Config.AutoUpdateApp;
            AppVersionLabel = $"SUSSYMODMANAGER v{AppInfo.Version}";
            UpdateStatus = AppInfo.RepoConfigured ? "" : "Set your GitHub repo in AppInfo.cs to enable updates.";
            PlatformLabel = $"{PlatformInfo.Os} ({PlatformInfo.ProcessArchitecture})";
            RefreshBepInEx();
            ReloadProfiles();
        }

        partial void OnAutoUpdateAppChanged(bool value)
        {
            _env.Config.AutoUpdateApp = value;
            _env.Save();
        }

        [RelayCommand]
        private async Task CheckForUpdatesAsync()
        {
            UpdateStatus = "Checking for updates...";
            try
            {
                var info = await _appUpdates.CheckAsync().ConfigureAwait(true);
                if (info == null || !AppInfo.RepoConfigured)
                {
                    UpdateStatus = "Updates not configured.";
                    return;
                }
                if (info.UpdateAvailable)
                {
                    UpdateStatus = $"Update available: v{info.LatestVersion}. It will download and apply automatically.";
                    if (AutoUpdateApp && info.CanAutoApply)
                        await _appUpdates.DownloadAndStageAsync(info).ConfigureAwait(true);
                    else
                        _appUpdates.OpenDownload(info);
                }
                else
                {
                    UpdateStatus = $"You're on the latest version (v{AppInfo.Version}).";
                }
            }
            catch (Exception ex)
            {
                UpdateStatus = $"Update check failed: {ex.Message}";
            }
        }

        public void ReloadProfiles()
        {
            Profiles.Clear();
            foreach (var profile in _env.Profiles.GetAllProfiles())
            {
                Profiles.Add(new ColorProfileViewModel(profile)
                {
                    IsActive = string.Equals(profile.Id, _env.Config.ActiveColorProfileId, StringComparison.OrdinalIgnoreCase)
                });
            }
        }

        private void RefreshBepInEx()
        {
            if (string.IsNullOrEmpty(AmongUsPath))
            {
                BepInExStatus = "Not installed";
                return;
            }

            var issue = BepInExInstaller.GetReadinessIssue(AmongUsPath, SelectedChannel ?? _env.Config.GameChannel);
            if (issue != null)
            {
                BepInExStatus = BepInExInstaller.IsBepInExInstalled(AmongUsPath)
                    ? "Needs repair"
                    : "Not installed";
                return;
            }

            var build = BepInExInstaller.GetInstalledBuild(AmongUsPath);
            var target = BepInExInstaller.ResolveTarget(AmongUsPath, SelectedChannel ?? _env.Config.GameChannel);
            BepInExStatus = build != null
                ? $"be.{build.Value} ({target}) — ready"
                : $"Installed ({target}) — ready";
        }

        partial void OnSelectedChannelChanged(string value)
        {
            if (string.IsNullOrEmpty(value))
                return;
            _env.Config.GameChannel = value;
            _env.Save();
        }

        partial void OnShowBetaVersionsChanged(bool value)
        {
            _env.Config.ShowBetaVersions = value;
            _env.Save();
        }

        [RelayCommand]
        private async Task Detect()
        {
            if (IsBusy)
                return;
            IsBusy = true;
            try
            {
                _env.SetStatus("Looking for Among Us...");
                var found = await AppEnvironment.DetectGameAsync(includeHeavyProbes: true).ConfigureAwait(true);
                if (found != null && !string.IsNullOrEmpty(found.Path))
                {
                    AmongUsPath = found.Path;
                    if (!string.IsNullOrEmpty(found.Channel))
                        SelectedChannel = found.Channel;
                    SavePath();
                    _env.SetStatus(_env.ApplyAutoDetectedGame(found));
                }
                else
                {
                    _env.SetStatus("Could not auto-detect Among Us. Enter the path manually.");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void SavePath()
        {
            if (!string.IsNullOrWhiteSpace(AmongUsPath) && !AmongUsLocator.IsValidGamePath(AmongUsPath))
            {
                _env.SetStatus("That folder does not look like an Among Us install.");
                return;
            }
            _env.Config.AmongUsPath = AmongUsPath;
            _env.Save();
            RefreshBepInEx();

            if (!string.IsNullOrWhiteSpace(AmongUsPath) && !AmongUsLocator.CanModifyGameFolder(AmongUsPath))
            {
                _env.SetStatus(
                    "Saved path, but this folder may be read-only (common for some Microsoft Store installs). " +
                    "Use the Xbox App / Game Pass copy under XboxGames, or Epic/Steam, for modding.");
                return;
            }

            _env.SetStatus("Saved Among Us path.");
        }

        public void SetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            AmongUsPath = path;
            SelectedChannel = AmongUsLocator.GuessChannel(path);
            SavePath();
        }

        [RelayCommand]
        private async Task InstallBepInExAsync()
        {
            if (IsBusy || string.IsNullOrEmpty(AmongUsPath))
            {
                _env.SetStatus("Set a valid Among Us path first.");
                return;
            }
            IsBusy = true;
            try
            {
                _env.SetStatus("Installing/updating BepInEx...");
                await _env.Manager.ReinstallBepInExAsync().ConfigureAwait(true);
                RefreshBepInEx();
                _env.SetStatus($"BepInEx ready: {BepInExStatus}");
            }
            catch (Exception ex)
            {
                _env.SetStatus($"BepInEx error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ConvertToVanillaAsync()
        {
            if (IsBusy || string.IsNullOrEmpty(AmongUsPath))
            {
                _env.SetStatus("Set a valid Among Us path first.");
                return;
            }
            if (!await DialogService.ConfirmAsync(
                    "Convert game to vanilla",
                    "This removes BepInEx and all active mods from your Among Us install, but keeps your downloaded mods so you can re-enable them later.\n\nContinue?",
                    yes: "Convert to vanilla", no: "Cancel", danger: true).ConfigureAwait(true))
                return;

            IsBusy = true;
            try
            {
                await Task.Run(() => _env.Manager.ConvertToVanilla()).ConfigureAwait(true);
                RefreshBepInEx();
                ArmDangerZone = false;
                _env.SetStatus("Game converted to vanilla (BepInEx removed). Your downloaded mods are kept.");
            }
            catch (Exception ex)
            {
                _env.SetStatus($"Error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RestoreVanillaAsync()
        {
            if (IsBusy || string.IsNullOrEmpty(AmongUsPath))
            {
                _env.SetStatus("Set a valid Among Us path first.");
                return;
            }
            if (!await DialogService.ConfirmAsync(
                    "Remove ALL mods",
                    "This deletes BepInEx AND every mod you've downloaded, returning Among Us to a clean vanilla install. This cannot be undone (your game files are untouched).\n\nRemove everything?",
                    yes: "Remove everything", no: "Cancel", danger: true).ConfigureAwait(true))
                return;

            IsBusy = true;
            try
            {
                await Task.Run(() => _env.Manager.RestoreVanilla()).ConfigureAwait(true);
                RefreshBepInEx();
                ArmDangerZone = false;
                _env.SetStatus("Removed BepInEx and ALL mods. Game is fully vanilla.");
            }
            catch (Exception ex)
            {
                _env.SetStatus($"Error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ApplyProfile(ColorProfileViewModel vm)
        {
            if (vm == null)
                return;
            _env.Config.ActiveColorProfileId = vm.Id;
            _env.Save();
            ThemeService.Apply(vm.Profile);
            Editor.LoadFrom(vm.Profile);
            foreach (var p in Profiles)
                p.IsActive = string.Equals(p.Id, vm.Id, StringComparison.OrdinalIgnoreCase);
            _env.SetStatus($"Applied profile: {vm.Name}");
        }

        /// <summary>Pulls the editor's fields back to the currently active profile, discarding edits.</summary>
        [RelayCommand]
        private void ResetEditor()
        {
            var active = _env.Profiles.GetProfileOrDefault(_env.Config.ActiveColorProfileId);
            Editor.LoadFrom(active);
            ThemeService.Apply(active);
            _env.SetStatus("Reverted to the saved profile colors.");
        }

        [RelayCommand]
        private async Task ExportThemeAsync()
        {
            var active = _env.Profiles.GetProfileOrDefault(_env.Config.ActiveColorProfileId);
            var profile = Editor.BuildProfile(active.Id, active.Name, active.IsBuiltin);
            profile.Name = active.Name;

            var path = await DialogService.SaveThemeFileAsync(active.Name).ConfigureAwait(true);
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                ThemeFile.Write(profile, path);
                _env.SetStatus($"Exported theme to {path}");
            }
            catch (Exception ex)
            {
                await DialogService.ShowErrorAsync("Export failed", ex.Message).ConfigureAwait(true);
            }
        }

        [RelayCommand]
        private async Task ImportThemeAsync()
        {
            var path = await DialogService.PickThemeFileAsync().ConfigureAwait(true);
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                var profile = ThemeFile.Read(path);
                if (profile == null)
                {
                    await DialogService.ShowErrorAsync("Import failed", "That file doesn't look like a theme.").ConfigureAwait(true);
                    return;
                }

                profile.Id = "custom-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                profile.IsBuiltin = false;
                if (string.IsNullOrWhiteSpace(profile.Name))
                    profile.Name = System.IO.Path.GetFileNameWithoutExtension(path);

                _env.Profiles.UpsertUserProfile(profile);
                _env.Config.ActiveColorProfileId = profile.Id;
                _env.Save();
                ThemeService.Apply(profile);
                Editor.LoadFrom(profile);
                ReloadProfiles();
                _env.SetStatus($"Imported theme \"{profile.Name}\".");
            }
            catch (Exception ex)
            {
                await DialogService.ShowErrorAsync("Import failed", ex.Message).ConfigureAwait(true);
            }
        }

        [RelayCommand]
        private async Task SaveCustomThemeAsync()
        {
            var name = await DialogService.PromptAsync("Save theme",
                "Name your custom color theme.", "My Theme").ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(name))
                return;

            var id = "custom-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var profile = Editor.BuildProfile(id, name);
            _env.Profiles.UpsertUserProfile(profile);
            _env.Config.ActiveColorProfileId = profile.Id;
            _env.Save();
            ThemeService.Apply(profile);
            ReloadProfiles();
            _env.SetStatus($"Saved and applied theme \"{name}\".");
        }

        [RelayCommand]
        private async Task DeleteProfileAsync(ColorProfileViewModel vm)
        {
            if (vm == null || vm.IsBuiltin)
                return;
            if (!await DialogService.ConfirmAsync("Delete theme",
                    $"Delete the custom theme \"{vm.Name}\"?",
                    yes: "Delete", no: "Cancel", danger: true).ConfigureAwait(true))
                return;

            _env.Profiles.DeleteUserProfile(vm.Id);

            // If we deleted the active profile, fall back to the default.
            if (string.Equals(_env.Config.ActiveColorProfileId, vm.Id, StringComparison.OrdinalIgnoreCase))
            {
                _env.Config.ActiveColorProfileId = "sus-default";
                var fallback = _env.Profiles.GetProfileOrDefault(_env.Config.ActiveColorProfileId);
                ThemeService.Apply(fallback);
                Editor.LoadFrom(fallback);
            }
            _env.Save();
            ReloadProfiles();
            _env.SetStatus($"Deleted theme \"{vm.Name}\".");
        }
    }
}
