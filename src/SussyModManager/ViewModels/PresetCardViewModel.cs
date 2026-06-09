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

        public bool IsPackSelected =>
            string.Equals(_env.Config.ActivePackId, Preset.Id, StringComparison.OrdinalIgnoreCase);

        public bool IsPinned => Preset.Pinned;

        /// <summary>Same featured treatment as store mod cards (pinned built-in packs).</summary>
        public bool IsFeatured => Preset.Pinned;

        public string PlayButtonLabel => IsPackSelected ? $"▶  Play {Name}" : "▶  Play Pack";

        [ObservableProperty] private string _installCountLabel;

        public PresetCardViewModel(AppEnvironment env, Preset preset)
        {
            _env = env;
            Preset = preset;
            ModNames = preset.ModIds
                .Select(id => _env.Store.GetEntry(id)?.name ?? id)
                .ToList();
            _env.PackSelectionChanged += (_, _) => RefreshPackState();
            RefreshInstallCount();
        }

        public void RefreshPackState()
        {
            OnPropertyChanged(nameof(IsPackSelected));
            OnPropertyChanged(nameof(PlayButtonLabel));
        }

        public void RefreshInstallCount()
        {
            var fresh = _env.Presets.ResolveFreshPreset(Preset, _env.Config) ?? Preset;
            var ids = fresh.ModIds ?? new List<string>();
            var total = ids.Count;
            var ready = ids.Count(_env.Manager.IsModReady);
            InstallCountLabel = $"{ready}/{total} installed";
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
                var wasPackSelected = IsPackSelected;
                var result = await _env.Manager.InstallPresetAsync(Preset).ConfigureAwait(true);
                _env.SetStatus(result.Message);
                if (!wasPackSelected && IsPackSelected)
                    _env.NotifyPackInstalled();
                else
                    _env.NotifyModLibraryChanged();
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
        private async Task SelectPackAsync()
        {
            if (IsBusy || IsPackSelected)
                return;
            if (!await _env.EnsureModOperationsReadyAsync($"select {Name}").ConfigureAwait(true))
                return;

            IsBusy = true;
            try
            {
                _env.SetStatus($"Selecting {Name}...");
                var result = await _env.Manager.SelectPresetAsync(Preset).ConfigureAwait(true);
                _env.NotifyPackInstalled();
                _env.NotifyPackSelectionChanged();
                _env.SetStatus(result.Success
                    ? $"Selected {Name}. Hit Play {Name} when you're ready."
                    : $"Selected {Name} with some install warnings.");
                await DialogService.ShowResultAsync($"Select {Name}", result).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _env.SetStatus($"Error selecting {Name}: {ex.Message}");
                await DialogService.ShowErrorAsync($"Couldn't select {Name}", ex.Message).ConfigureAwait(true);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void DeselectPack()
        {
            if (!IsPackSelected)
                return;

            _env.Manager.DeselectPack();
            _env.NotifyPackSelectionChanged();
            _env.SetStatus("Pack deselected. Pick your own launch mods on Installed, then hit Play.");
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
            OnPropertyChanged(nameof(PlayButtonLabel));
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

            if (IsPackSelected)
                _env.Manager.DeselectPack();

            _env.Config.UserPresets.RemoveAll(p => string.Equals(p.Id, Preset.Id, StringComparison.OrdinalIgnoreCase));
            _env.Save();
            _env.NotifyPackSelectionChanged();
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
                var preset = _env.Presets.ResolveFreshPreset(Preset, _env.Config);
                _env.SetStatus($"Launching {preset.Name}...");
                var result = await _env.Manager.PlayPresetAsync(preset).ConfigureAwait(true);
                _env.NotifyPackSelectionChanged();
                _env.NotifyModLibraryChanged();
                _env.SetStatus(result.Message);
                await DialogService.ShowResultAsync($"Play {preset.Name}", result).ConfigureAwait(true);
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
