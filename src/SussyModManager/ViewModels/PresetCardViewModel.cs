using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Models;
using SussyModManager.Services;

namespace SussyModManager.ViewModels
{
    public partial class PresetCardViewModel : ViewModelBase
    {
        private readonly AppEnvironment _env;
        public Preset Preset { get; }

        /// <summary>Raised after a user preset is renamed or deleted so the list can refresh.</summary>
        public event EventHandler Changed;

        [ObservableProperty] private bool _isBusy;

        public string Name => Preset.Name;
        public string Description => Preset.Description;
        public bool IsBuiltin => Preset.Builtin;
        public bool IsUserPreset => !Preset.Builtin;
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
            if (!await _env.EnsureModOperationsReadyAsync($"install {Name}").ConfigureAwait(true))
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

        /// <summary>Loads this preset's mods as the active selection without launching.</summary>
        [RelayCommand]
        private async Task Apply()
        {
            var installedCount = Preset.ModIds.Count(_env.Manager.IsInstalled);
            _env.Manager.SetLaunchSelection(Preset.ModIds, syncPlugins: false);
            if (!string.IsNullOrEmpty(_env.Config.AmongUsPath))
                await Task.Run(() => _env.Manager.ResyncActivePlugins()).ConfigureAwait(true);

            var missing = Preset.ModIds.Count - installedCount;
            _env.SetStatus(missing > 0
                ? $"Loaded {Name}. {missing} mod(s) aren't installed yet - use Install Pack to get them."
                : $"Loaded {Name} as your active selection.");
            _env.RequestNavigation("installed");
        }

        [RelayCommand]
        private async Task RenameAsync()
        {
            if (IsBuiltin)
                return;
            var newName = await DialogService.PromptAsync("Rename preset",
                "Give this mod pack a new name.", Name).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, Name, StringComparison.Ordinal))
                return;

            Preset.Name = newName;
            Preset.UpdatedUtcTicks = DateTime.UtcNow.Ticks;
            _env.Save();
            OnPropertyChanged(nameof(Name));
            _env.SetStatus($"Renamed preset to {newName}.");
            Changed?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (IsBuiltin)
                return;
            if (!await DialogService.ConfirmAsync("Delete preset",
                    $"Delete the preset \"{Name}\"? Your downloaded mods are not affected.",
                    yes: "Delete", no: "Cancel", danger: true).ConfigureAwait(true))
                return;

            _env.Config.UserPresets.RemoveAll(p => string.Equals(p.Id, Preset.Id, StringComparison.OrdinalIgnoreCase));
            _env.Save();
            _env.SetStatus($"Deleted preset {Name}.");
            Changed?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private async Task ExportAsync()
        {
            if (IsBuiltin)
                return;

            var path = await DialogService.SavePresetFileAsync(Name).ConfigureAwait(true);
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                PresetShareFile.Write(Preset, path);
                _env.SetStatus($"Exported \"{Name}\" to {path}");
            }
            catch (Exception ex)
            {
                await DialogService.ShowErrorAsync("Export failed", ex.Message).ConfigureAwait(true);
            }
        }

        [RelayCommand]
        private async Task PlayAsync()
        {
            if (IsBusy)
                return;
            if (!await _env.EnsureModOperationsReadyAsync($"launch {Name}").ConfigureAwait(true))
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
