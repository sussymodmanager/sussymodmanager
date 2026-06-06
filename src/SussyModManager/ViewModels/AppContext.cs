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
        /// Applies a detected game location to config and returns a user-facing status message.
        /// </summary>
        public string ApplyAutoDetectedGame(GameLocation detected)
        {
            if (detected == null || string.IsNullOrEmpty(detected.Path))
                return "Welcome! Set your Among Us path in Settings to get started.";

            Config.AmongUsPath = detected.Path;
            if (!string.IsNullOrEmpty(detected.Channel))
                Config.GameChannel = detected.Channel;
            Config.Save();

            if (!AmongUsLocator.CanModifyGameFolder(detected.Path))
            {
                return $"Detected Among Us ({detected.Channel}) at {detected.Path}, but this folder may be read-only. " +
                       "Use the Xbox App / Game Pass copy under XboxGames, or Epic/Steam, for modding.";
            }

            return $"Detected Among Us ({detected.Channel}) at {detected.Path}";
        }

        /// <summary>Runs auto-detect off the UI thread (optionally with heavy MS Store probes).</summary>
        public static Task<GameLocation> DetectGameAsync(bool includeHeavyProbes = false) =>
            Task.Run(() => AmongUsLocator.DetectGame(includeHeavyProbes));

        /// <summary>
        /// Waits for any in-flight BeanModManager mod import before install/play operations.
        /// </summary>
        public async Task<bool> EnsureModOperationsReadyAsync(string actionLabel)
        {
            if (Config.LegacyModsCopyPending || LegacyMigration.IsCopyInProgress)
            {
                SetStatus("Importing mods from BeanModManager...");
                try
                {
                    await LegacyMigration.CopyTask.ConfigureAwait(true);
                    SetStatus("BeanModManager import complete.");
                }
                catch
                {
                    SetStatus("BeanModManager import finished with errors - some mod files may be missing.");
                }
            }

            return await EnsureGamePathAsync(actionLabel).ConfigureAwait(true);
        }

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
