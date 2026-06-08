using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Models;
using SussyModManager.Core.Services;
using SussyModManager.Services;

namespace SussyModManager.ViewModels
{
    /// <summary>
    /// Drives the first-launch onboarding flow: welcome -> game path -> theme -> optional SUS AF.
    /// </summary>
    public partial class WizardViewModel : ViewModelBase
    {
        private readonly AppEnvironment _env;

        public event EventHandler Completed;

        public ObservableCollection<ColorProfileViewModel> Profiles { get; } = new ObservableCollection<ColorProfileViewModel>();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsWelcome), nameof(IsPathStep), nameof(IsThemeStep), nameof(IsPackStep))]
        [NotifyPropertyChangedFor(nameof(BackVisible), nameof(IsLastStep), nameof(NextLabel))]
        private int _stepIndex;

        [ObservableProperty] private string _amongUsPath;
        [ObservableProperty] private string _pathStatus;
        [ObservableProperty] private bool _installPack = true;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _busyStatus;

        public bool IsWelcome => StepIndex == 0;
        public bool IsPathStep => StepIndex == 1;
        public bool IsThemeStep => StepIndex == 2;
        public bool IsPackStep => StepIndex == 3;
        public bool BackVisible => StepIndex > 0;
        public bool IsLastStep => StepIndex == 3;
        public string NextLabel => IsLastStep ? "Finish" : "Next";

        public WizardViewModel(AppEnvironment env)
        {
            _env = env;
            AmongUsPath = env.Config.AmongUsPath;

            foreach (var profile in env.Profiles.GetAllProfiles())
            {
                Profiles.Add(new ColorProfileViewModel(profile)
                {
                    IsActive = string.Equals(profile.Id, env.Config.ActiveColorProfileId, StringComparison.OrdinalIgnoreCase)
                });
            }

            if (string.IsNullOrWhiteSpace(AmongUsPath))
            {
                PathStatus = "Looking for Among Us...";
                _ = AutoDetectPathAsync();
            }
            else
            {
                PathStatus = "Using your saved Among Us folder.";
            }
        }

        private async Task AutoDetectPathAsync()
        {
            try
            {
                var detected = await AppEnvironment.DetectGameAsync(includeHeavyProbes: true).ConfigureAwait(true);
                if (detected != null && !string.IsNullOrEmpty(detected.Path))
                {
                    AmongUsPath = detected.Path;
                    if (!string.IsNullOrEmpty(detected.Channel))
                        _env.Config.GameChannel = detected.Channel;
                    PathStatus = _env.ApplyAutoDetectedGame(detected);
                }
                else
                {
                    PathStatus = "Couldn't auto-detect. Use Browse to pick the folder.";
                }
            }
            catch
            {
                PathStatus = "Couldn't auto-detect. Use Browse to pick the folder.";
            }
        }

        [RelayCommand]
        private async Task Detect()
        {
            if (IsBusy)
                return;
            IsBusy = true;
            try
            {
                PathStatus = "Looking for Among Us...";
                var found = await AppEnvironment.DetectGameAsync(includeHeavyProbes: true).ConfigureAwait(true);
                if (found != null && !string.IsNullOrEmpty(found.Path))
                {
                    AmongUsPath = found.Path;
                    if (!string.IsNullOrEmpty(found.Channel))
                        _env.Config.GameChannel = found.Channel;
                    PathStatus = _env.ApplyAutoDetectedGame(found);
                }
                else
                {
                    PathStatus = "Couldn't auto-detect. Use Browse to pick the folder.";
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void SetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            AmongUsPath = path;
            _env.Config.GameChannel = AmongUsLocator.GuessChannel(path);
            PathStatus = AmongUsLocator.IsValidGamePath(path)
                ? $"Looks good: {path}"
                : "That folder doesn't look like an Among Us install - you can still continue.";
        }

        [RelayCommand]
        private void ApplyProfile(ColorProfileViewModel vm)
        {
            if (vm == null)
                return;
            _env.Config.ActiveColorProfileId = vm.Id;
            ThemeService.Apply(vm.Profile);
            foreach (var p in Profiles)
                p.IsActive = string.Equals(p.Id, vm.Id, StringComparison.OrdinalIgnoreCase);
        }

        [RelayCommand]
        private void Back()
        {
            if (StepIndex > 0)
                StepIndex--;
        }

        [RelayCommand]
        private async Task NextAsync()
        {
            if (IsPathStep && !string.IsNullOrWhiteSpace(AmongUsPath))
            {
                _env.Config.AmongUsPath = AmongUsPath;
                _env.Save();
            }

            if (!IsLastStep)
            {
                StepIndex++;
                return;
            }

            await FinishAsync().ConfigureAwait(true);
        }

        [RelayCommand]
        private void Skip() => CompleteWizard();

        private async Task FinishAsync()
        {
            if (!string.IsNullOrWhiteSpace(AmongUsPath))
            {
                _env.Config.AmongUsPath = AmongUsPath;
                _env.Save();
            }

            if (InstallPack && !string.IsNullOrWhiteSpace(AmongUsPath))
            {
                IsBusy = true;
                try
                {
                    // Fresh installs start with an empty store cache and fall back to the data files
                    // bundled in the release, which can lag behind the live pack definition. Pull the
                    // latest from GitHub first so onboarding always installs the current SUS AF pack
                    // (and not mods we've since removed) rather than racing the background refresh.
                    var pack = _env.Presets.GetAllPresets(_env.Config)
                        .FirstOrDefault(p => string.Equals(p.Id, "sus-af-pack", StringComparison.OrdinalIgnoreCase))
                        ?? _env.Presets.GetAllPresets(_env.Config).FirstOrDefault(p => p.Builtin);
                    if (pack != null)
                    {
                        BusyStatus = $"Installing and selecting {pack.Name}...";
                        _env.Manager.Progress += OnPackProgress;
                        try
                        {
                            var result = await _env.Manager.SelectPresetAsync(pack).ConfigureAwait(true);
                            _env.SetStatus(result.Message);
                            _env.NotifyPackInstalled();
                            await DialogService.ShowResultAsync($"Install {pack.Name}", result).ConfigureAwait(true);
                        }
                        finally
                        {
                            _env.Manager.Progress -= OnPackProgress;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _env.SetStatus($"Pack install failed: {ex.Message}");
                    await DialogService.ShowErrorAsync("Pack install failed", ex.Message).ConfigureAwait(true);
                }
                finally
                {
                    IsBusy = false;
                }
            }

            CompleteWizard();
        }

        private void OnPackProgress(object sender, string message) => BusyStatus = message;

        private void CompleteWizard()
        {
            _env.Config.FirstLaunchWizardCompleted = true;
            _env.Save();
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }
}
