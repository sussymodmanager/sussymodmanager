using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;

namespace SussyModManager.ViewModels
{
    public partial class PresetsViewModel : ViewModelBase
    {
        private readonly AppEnvironment _env;

        public ObservableCollection<PresetCardViewModel> Presets { get; } = new ObservableCollection<PresetCardViewModel>();

        public string Title => "Presets";
        public string Subtitle => "Curated mod packs. Install everything in one click.";

        public PresetsViewModel(AppEnvironment env)
        {
            _env = env;
            Reload();
        }

        public void Reload()
        {
            Presets.Clear();
            foreach (var preset in _env.Presets.GetAllPresets(_env.Config))
                Presets.Add(new PresetCardViewModel(_env, preset));
        }

        [RelayCommand]
        private void SaveCurrentSelection()
        {
            var preset = new Core.Models.Preset
            {
                Name = "My Pack",
                Description = "Saved from your current selection.",
                ModIds = new System.Collections.Generic.List<string>(_env.Config.SelectedMods)
            };
            _env.Config.UserPresets.Add(preset);
            _env.Save();
            Reload();
            _env.SetStatus("Saved current selection as a preset.");
        }
    }
}
