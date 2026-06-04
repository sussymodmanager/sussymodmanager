using System;
using System.Threading.Tasks;
using SussyModManager.Core.Models;
using SussyModManager.Core.Services;
using SussyModManager.Services;

namespace SussyModManager.ViewModels
{
    /// <summary>
    /// Shared state and services handed to every page view model so they all act on the same
    /// config, mod manager and status line.
    /// </summary>
    public class AppEnvironment
    {
        public Config Config { get; }
        public ModManager Manager { get; }
        public ModStore Store => Manager.Store;
        public PresetService Presets { get; }
        public ColorProfileService Profiles { get; }

        public event EventHandler<string> StatusChanged;

        /// <summary>Raised when a view model wants the shell to switch tabs (e.g. open Settings).</summary>
        public event EventHandler<string> NavigationRequested;

        public AppEnvironment(Config config, ColorProfileService profiles)
        {
            Config = config;
            Profiles = profiles;
            Presets = new PresetService();
            Manager = new ModManager(config);
            Manager.Progress += (_, message) => SetStatus(message);
        }

        public void SetStatus(string message) => StatusChanged?.Invoke(this, message);

        public void RequestNavigation(string tab) => NavigationRequested?.Invoke(this, tab);

        public void Save() => Config.Save();

        /// <summary>
        /// Ensures an Among Us folder is configured before an action that needs it. If not set,
        /// offers to open Settings and returns false so the caller can abort.
        /// </summary>
        public async Task<bool> EnsureGamePathAsync(string actionLabel)
        {
            if (!string.IsNullOrWhiteSpace(Config.AmongUsPath))
                return true;

            SetStatus("Set your Among Us folder in Settings first.");
            var open = await DialogService.ConfirmAsync(
                "Among Us folder not set",
                $"You need to point SUSSYMODMANAGER at your Among Us install before you can {actionLabel}.\n\nOpen Settings now?",
                yes: "Open Settings", no: "Not now").ConfigureAwait(true);
            if (open)
                RequestNavigation("settings");
            return false;
        }
    }
}
