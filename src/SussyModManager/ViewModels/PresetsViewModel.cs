using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using SussyModManager.Core.Helpers;
using SussyModManager.Services;

namespace SussyModManager.ViewModels
{
    public partial class PresetsViewModel : ViewModelBase
    {
        private readonly AppEnvironment _env;

        public ObservableCollection<PresetCardViewModel> Presets { get; } = new ObservableCollection<PresetCardViewModel>();

        public string Title => "Presets";
        public string Subtitle =>
            "Install Pack downloads missing mods. Select Pack refreshes from GitHub, installs missing, and locks play to that loadout (updates on Play).";

        public PresetsViewModel(AppEnvironment env)
        {
            _env = env;
            Reload();
        }

        public void Reload()
        {
            foreach (var existing in Presets)
                existing.Changed -= OnPresetChanged;

            Presets.Clear();
            foreach (var preset in _env.Presets.GetAllPresets(_env.Config))
            {
                var card = new PresetCardViewModel(_env, preset);
                card.Changed += OnPresetChanged;
                card.RefreshInstallCount();
                Presets.Add(card);
            }
        }

        public void RefreshInstallCounts()
        {
            foreach (var card in Presets)
                card.RefreshInstallCount();
        }

        private void OnPresetChanged(object sender, EventArgs e) => Reload();

        [RelayCommand]
        private async Task SaveCurrentSelectionAsync()
        {
            if (_env.Config.SelectedMods.Count == 0)
            {
                _env.SetStatus("Select some mods on the Installed page first, then save them as a preset.");
                await DialogService.ShowInfoAsync("Nothing selected",
                    "Tick the mods you want on the Installed page, then come back and save them as a preset.").ConfigureAwait(true);
                return;
            }

            var name = await DialogService.PromptAsync("Save preset",
                "Name this mod pack so you can switch to it later.", "My Pack").ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(name))
                return;

            var preset = new Core.Models.Preset
            {
                Name = name,
                Description = "Saved from your current selection.",
                ModIds = new System.Collections.Generic.List<string>(_env.Config.SelectedMods)
            };
            _env.Config.UserPresets.Add(preset);
            _env.Save();
            Reload();
            _env.SetStatus($"Saved \"{name}\" as a preset.");
        }

        [RelayCommand]
        private async Task ImportPresetAsync()
        {
            var path = await DialogService.PickPresetFileAsync().ConfigureAwait(true);
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                var preset = PresetShareFile.Read(path);
                if (preset == null)
                {
                    await DialogService.ShowErrorAsync("Import failed", "That file doesn't look like a preset.").ConfigureAwait(true);
                    return;
                }

                if (string.IsNullOrWhiteSpace(preset.Name))
                    preset.Name = System.IO.Path.GetFileNameWithoutExtension(path);

                _env.Presets.UpsertUserPreset(_env.Config, preset);
                _env.Save();
                Reload();
                _env.SetStatus($"Imported preset \"{preset.Name}\".");
            }
            catch (Exception ex)
            {
                await DialogService.ShowErrorAsync("Import failed", ex.Message).ConfigureAwait(true);
            }
        }
    }
}
