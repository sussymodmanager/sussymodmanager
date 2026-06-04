using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SussyModManager.Core.Models;
using SussyModManager.Core.Services;
using SussyModManager.Services;

namespace SussyModManager.ViewModels
{
    /// <summary>
    /// Drives the first-launch onboarding flow: welcome -> game path -> theme -> optional SUS AF PACK.
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
                var detected = AmongUsLocator.Detect();
                if (!string.IsNullOrEmpty(detected))
                {
                    AmongUsPath = detected;
                    PathStatus = $"Auto-detected: {detected}";
                }
            }
            else
            {
                PathStatus = "Using your saved Among Us folder.";
            }
        }

        [RelayCommand]
        private void Detect()
        {
            var path = AmongUsLocator.Detect();
            if (!string.IsNullOrEmpty(path))
            {
                AmongUsPath = path;
                PathStatus = $"Found Among Us at {path}";
            }
            else
            {
                PathStatus = "Couldn't auto-detect. Use Browse to pick the folder.";
            }
        }

        public void SetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            AmongUsPath = path;
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
        private void Skip() => Finalize();

        private async Task FinishAsync()
        {
            if (!string.IsNullOrWhiteSpace(AmongUsPath))
            {
                _env.Config.AmongUsPath = AmongUsPath;
                _env.Save();
            }

            if (InstallPack && !string.IsNullOrWhiteSpace(AmongUsPath))
            {
                var pack = _env.Presets.GetAllPresets(_env.Config)
                    .FirstOrDefault(p => p.Builtin);
                if (pack != null)
                {
                    IsBusy = true;
                    try
                    {
                        BusyStatus = $"Installing {pack.Name}...";
                        _env.Manager.Progress += OnPackProgress;
                        var result = await _env.Manager.InstallPresetAsync(pack).ConfigureAwait(true);
                        _env.SetStatus(result.Message);
                        await DialogService.ShowResultAsync($"Install {pack.Name}", result).ConfigureAwait(true);
                    }
                    catch (Exception ex)
                    {
                        _env.SetStatus($"Pack install failed: {ex.Message}");
                        await DialogService.ShowErrorAsync("Pack install failed", ex.Message).ConfigureAwait(true);
                    }
                    finally
                    {
                        _env.Manager.Progress -= OnPackProgress;
                        IsBusy = false;
                    }
                }
            }

            Finalize();
        }

        private void OnPackProgress(object sender, string message) => BusyStatus = message;

        private void Finalize()
        {
            _env.Config.FirstLaunchWizardCompleted = true;
            _env.Save();
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }
}
