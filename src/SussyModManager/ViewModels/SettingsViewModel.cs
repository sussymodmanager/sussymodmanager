using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SussyModManager.Core;
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
        public ObservableCollection<string> Channels { get; } = new ObservableCollection<string>
        {
            "Steam/Itch.io",
            "Epic/MS Store"
        };

        [ObservableProperty] private string _amongUsPath;
        [ObservableProperty] private string _selectedChannel;
        [ObservableProperty] private bool _showBetaVersions;
        [ObservableProperty] private string _bepInExStatus;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _customAccent = "#8B5CF6";
        [ObservableProperty] private string _platformLabel;
        [ObservableProperty] private bool _armDangerZone;
        [ObservableProperty] private bool _autoUpdateApp;
        [ObservableProperty] private string _appVersionLabel;
        [ObservableProperty] private string _updateStatus;

        private readonly AppUpdateService _appUpdates = new AppUpdateService();

        public string Title => "Settings";
        public string Subtitle => "Game location, channel and the all-important looks.";

        public SettingsViewModel(AppEnvironment env)
        {
            _env = env;
            AmongUsPath = env.Config.AmongUsPath;
            SelectedChannel = env.Config.GameChannel ?? "Steam/Itch.io";
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
            if (string.IsNullOrEmpty(AmongUsPath) || !BepInExInstaller.IsBepInExInstalled(AmongUsPath))
            {
                BepInExStatus = "Not installed";
                return;
            }

            var build = BepInExInstaller.GetInstalledBuild(AmongUsPath);
            if (build == null)
                BepInExStatus = $"Installed (unknown build - update to be.{BepInExInstaller.BuildNumber})";
            else if (build.Value < BepInExInstaller.BuildNumber)
                BepInExStatus = $"be.{build.Value} (update available: be.{BepInExInstaller.BuildNumber})";
            else
                BepInExStatus = $"be.{build.Value} (up to date)";
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
        private void Detect()
        {
            var path = AmongUsLocator.Detect();
            if (!string.IsNullOrEmpty(path))
            {
                AmongUsPath = path;
                SavePath();
                _env.SetStatus($"Found Among Us at {path}");
            }
            else
            {
                _env.SetStatus("Could not auto-detect Among Us. Enter the path manually.");
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
            _env.SetStatus("Saved Among Us path.");
        }

        public void SetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            AmongUsPath = path;
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
            foreach (var p in Profiles)
                p.IsActive = string.Equals(p.Id, vm.Id, StringComparison.OrdinalIgnoreCase);
            _env.SetStatus($"Applied profile: {vm.Name}");
        }

        /// <summary>Creates (or updates) a user profile that recolors the active profile's accent.</summary>
        [RelayCommand]
        private void ApplyCustomAccent()
        {
            var baseProfile = _env.Profiles.GetProfileOrDefault(_env.Config.ActiveColorProfileId);
            var custom = new ColorProfile
            {
                Id = "custom-accent",
                Name = "Custom Accent",
                IsBuiltin = false,
                Variant = baseProfile.Variant,
                Accent = NormalizeHex(CustomAccent) ?? baseProfile.Accent,
                AccentSecondary = NormalizeHex(CustomAccent) ?? baseProfile.AccentSecondary,
                Background = baseProfile.Background,
                Surface = baseProfile.Surface,
                SurfaceElevated = baseProfile.SurfaceElevated,
                CardBorder = baseProfile.CardBorder,
                TextPrimary = baseProfile.TextPrimary,
                TextMuted = baseProfile.TextMuted,
                Success = baseProfile.Success,
                Warning = baseProfile.Warning,
                Danger = baseProfile.Danger,
                Glow = baseProfile.Glow
            };

            _env.Profiles.UpsertUserProfile(custom);
            _env.Config.ActiveColorProfileId = custom.Id;
            _env.Save();
            ThemeService.Apply(custom);
            ReloadProfiles();
            _env.SetStatus("Applied your custom accent.");
        }

        private static string NormalizeHex(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            value = value.Trim();
            if (!value.StartsWith("#"))
                value = "#" + value;
            // Expand #RRGGBB to #FFRRGGBB so it always carries an alpha channel.
            if (value.Length == 7)
                value = "#FF" + value.Substring(1);
            return value.Length == 9 ? value : value;
        }
    }
}
