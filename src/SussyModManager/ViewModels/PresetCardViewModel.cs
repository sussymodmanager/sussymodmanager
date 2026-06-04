using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SussyModManager.Core.Models;
using SussyModManager.Services;

namespace SussyModManager.ViewModels
{
    public partial class PresetCardViewModel : ViewModelBase
    {
        private readonly AppEnvironment _env;
        public Preset Preset { get; }

        [ObservableProperty] private bool _isBusy;

        public string Name => Preset.Name;
        public string Description => Preset.Description;
        public bool IsBuiltin => Preset.Builtin;
        public List<string> ModNames { get; }

        public PresetCardViewModel(AppEnvironment env, Preset preset)
        {
            _env = env;
            Preset = preset;
            ModNames = preset.ModIds
                .Select(id => _env.Store.GetEntry(id)?.name ?? id)
                .ToList();
        }

        [RelayCommand]
        private async Task InstallAsync()
        {
            if (IsBusy)
                return;
            if (!await _env.EnsureGamePathAsync($"install {Name}").ConfigureAwait(true))
                return;

            IsBusy = true;
            try
            {
                _env.SetStatus($"Installing {Name}...");
                var result = await _env.Manager.InstallPresetAsync(Preset).ConfigureAwait(true);
                _env.SetStatus(result.Message);
                await DialogService.ShowResultAsync($"Install {Name}", result).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _env.SetStatus($"Error installing {Name}: {ex.Message}");
                await DialogService.ShowErrorAsync($"Couldn't install {Name}", ex.Message).ConfigureAwait(true);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task PlayAsync()
        {
            if (IsBusy)
                return;
            if (!await _env.EnsureGamePathAsync($"launch {Name}").ConfigureAwait(true))
                return;

            IsBusy = true;
            try
            {
                _env.SetStatus($"Launching {Name}...");
                await _env.Manager.PlayPresetAsync(Preset).ConfigureAwait(true);
                _env.SetStatus($"Launched {Name}. Have fun!");
            }
            catch (Exception ex)
            {
                _env.SetStatus($"Error launching {Name}: {ex.Message}");
                await DialogService.ShowErrorAsync($"Couldn't launch {Name}", ex.Message).ConfigureAwait(true);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
