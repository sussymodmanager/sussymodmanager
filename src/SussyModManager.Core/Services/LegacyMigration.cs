using System;
using System.IO;
using System.Threading.Tasks;
using SussyModManager.Core.Models;
using SussyModManager.Core.Platform;

namespace SussyModManager.Core.Services
{
    /// <summary>
    /// One-time best-effort import of an existing BeanModManager installation (Windows only).
    /// Metadata imports synchronously; mod files copy in the background so startup stays responsive.
    /// </summary>
    public static class LegacyMigration
    {
        private static Task _copyTask = Task.CompletedTask;

        /// <summary>True while a legacy mod-file copy is in flight.</summary>
        public static bool IsCopyInProgress => !_copyTask.IsCompleted;

        /// <summary>Await this before install/play when a legacy import may still be copying files.</summary>
        public static Task CopyTask => _copyTask;

        /// <summary>
        /// Imports legacy metadata synchronously. Returns the legacy Mods directory to copy, or null.
        /// </summary>
        public static string TryImport(Config config)
        {
            if (config.LegacyImportAttempted)
                return null;

            config.LegacyImportAttempted = true;

            try
            {
                if (!PlatformInfo.IsWindows)
                    return null;

                var legacyRoot = PlatformInfo.LegacyBeanDataRoot;
                var legacyConfigPath = Path.Combine(legacyRoot, "config.json");
                if (!File.Exists(legacyConfigPath))
                    return null;

                var legacy = Helpers.Json.Deserialize<LegacyConfig>(File.ReadAllText(legacyConfigPath));
                if (legacy == null)
                    return null;

                if (string.IsNullOrEmpty(config.AmongUsPath) && !string.IsNullOrEmpty(legacy.AmongUsPath))
                    config.AmongUsPath = legacy.AmongUsPath;
                if (!string.IsNullOrEmpty(legacy.GameChannel))
                    config.GameChannel = legacy.GameChannel;

                if (legacy.InstalledMods != null)
                    config.InstalledMods.AddRange(legacy.InstalledMods);
                if (legacy.SelectedMods != null)
                    config.SelectedMods.AddRange(legacy.SelectedMods);

                config.Save();

                var legacyMods = Path.Combine(legacyRoot, "Mods");
                return Directory.Exists(legacyMods) ? legacyMods : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Starts copying legacy mod files off the UI thread and tracks completion.</summary>
        public static void StartCopyLegacyMods(string legacyModsDir, Config config)
        {
            if (string.IsNullOrEmpty(legacyModsDir) || !Directory.Exists(legacyModsDir))
                return;

            config.LegacyModsCopyPending = true;
            config.Save();

            _copyTask = Task.Run(() =>
            {
                try
                {
                    CopyLegacyMods(legacyModsDir, config);
                }
                finally
                {
                    config.LegacyModsCopyPending = false;
                    config.Save();
                }
            });
        }

        /// <summary>Copies the legacy downloaded mods into the new data root. Best-effort.</summary>
        public static void CopyLegacyMods(string legacyModsDir, Config config)
        {
            try
            {
                if (!string.IsNullOrEmpty(legacyModsDir) && Directory.Exists(legacyModsDir))
                    ModInstaller.CopyDirectoryContents(legacyModsDir, config.ModsFolder, true);
            }
            catch
            {
            }
        }

        private class LegacyConfig
        {
            public string AmongUsPath { get; set; }
            public string GameChannel { get; set; }
            public System.Collections.Generic.List<InstalledMod> InstalledMods { get; set; }
            public System.Collections.Generic.List<string> SelectedMods { get; set; }
        }
    }
}
