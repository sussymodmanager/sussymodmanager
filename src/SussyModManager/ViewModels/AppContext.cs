using System;
using System.Threading;
using System.Threading.Tasks;
using SussyModManager.Core.Helpers;
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
        private readonly SynchronizationContext _uiContext;
        private readonly object _idleLock = new object();
        private CancellationTokenSource _idleCts;

        public Config Config { get; }
        public ModManager Manager { get; }
        public ModStore Store => Manager.Store;
        public PresetService Presets { get; }
        public ColorProfileService Profiles { get; }

        public event EventHandler<string> StatusChanged;

        /// <summary>Raised when a view model wants the shell to switch tabs (e.g. open Settings).</summary>
        public event EventHandler<string> NavigationRequested;

        /// <summary>Raised when pack mode is turned on or off (Select / Deselect pack).</summary>
        public event EventHandler PackSelectionChanged;

        /// <summary>Raised after mod-registry / presets are refreshed from GitHub.</summary>
        public event EventHandler StoreCatalogRefreshed;

        public AppEnvironment(Config config, ColorProfileService profiles)
        {
            Config = config;
            Profiles = profiles;
            Presets = new PresetService();
            Manager = new ModManager(config, presets: Presets);
            _uiContext = SynchronizationContext.Current;
            Manager.Progress += (_, message) => SetProgress(message);
        }

        /// <summary>User-facing status that should stay until the next action.</summary>
        public void SetStatus(string message)
        {
            CancelIdleRestore();
            RaiseStatus(message);
        }

        /// <summary>Short-lived progress from background work; returns to idle shortly after it stops.</summary>
        public void SetProgress(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            RaiseStatus(message);
            ScheduleIdleRestore(delayMs: 2000);
        }

        public string GetIdleStatus()
        {
            if (Manager.IsPackModeActive)
            {
                var name = Manager.GetActivePackName();
                if (!string.IsNullOrWhiteSpace(name))
                    return $"Ready — hit Play {name} when you want.";
            }

            return "Ready to go.";
        }

        public void RestoreIdleStatus() => SetStatus(GetIdleStatus());

        public void RequestNavigation(string tab) => NavigationRequested?.Invoke(this, tab);

        public void NotifyPackSelectionChanged() => PackSelectionChanged?.Invoke(this, EventArgs.Empty);

        /// <summary>After install or explicit select — refresh pack UI and installed mod cards.</summary>
        public void NotifyPackInstalled() => NotifyModLibraryChanged();

        /// <summary>Raised when the mod library changed (install, uninstall, update, pack sync).</summary>
        public void NotifyModLibraryChanged() => ModLibraryChanged?.Invoke(this, EventArgs.Empty);

        public event EventHandler ModLibraryChanged;

        /// <summary>Obsolete alias — use <see cref="ModLibraryChanged"/>.</summary>
        public event EventHandler InstalledNeedsRefresh
        {
            add => ModLibraryChanged += value;
            remove => ModLibraryChanged -= value;
        }

        public void Save() => Config.Save();

        public async Task<bool> RefreshStoreCatalogAsync()
        {
            var changed = await DataStore.RefreshAsync().ConfigureAwait(false);
            Manager.Store.Reload();
            StoreCatalogRefreshed?.Invoke(this, EventArgs.Empty);
            return changed;
        }

        /// <summary>Checks remote mod versions when <see cref="Config.AutoUpdateMods"/> is enabled.</summary>
        public async Task<System.Collections.Generic.List<ModUpdateInfo>> CheckModUpdatesIfEnabledAsync()
        {
            if (!Config.AutoUpdateMods)
                return null;

            return await Manager.CheckForUpdatesAsync().ConfigureAwait(false);
        }

        private void RaiseStatus(string message) => StatusChanged?.Invoke(this, message);

        private void CancelIdleRestore()
        {
            lock (_idleLock)
            {
                _idleCts?.Cancel();
                _idleCts?.Dispose();
                _idleCts = null;
            }
        }

        private void ScheduleIdleRestore(int delayMs)
        {
            CancellationToken token;
            lock (_idleLock)
            {
                _idleCts?.Cancel();
                _idleCts?.Dispose();
                _idleCts = new CancellationTokenSource();
                token = _idleCts.Token;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delayMs, token).ConfigureAwait(false);
                    PostToUi(() => RaiseStatus(GetIdleStatus()));
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        private void PostToUi(Action action)
        {
            if (_uiContext != null)
                _uiContext.Post(_ => action(), null);
            else
                action();
        }

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
