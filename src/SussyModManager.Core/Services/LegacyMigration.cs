using System;
using System.IO;
using SussyModManager.Core.Models;
using SussyModManager.Core.Platform;

namespace SussyModManager.Core.Services
{
    /// <summary>
    /// One-time best-effort import of an existing BeanModManager installation (Windows only,
    /// since that is the only platform BeanModManager ran on). Copies the downloaded Mods folder
    /// and the detected game path so existing users keep their library.
    /// </summary>
    public static class LegacyMigration
    {
        public static bool TryImport(Config config)
        {
            if (config.LegacyImportAttempted)
                return false;

            config.LegacyImportAttempted = true;

            try
            {
                if (!PlatformInfo.IsWindows)
                    return false;

                var legacyRoot = PlatformInfo.LegacyBeanDataRoot;
                var legacyConfigPath = Path.Combine(legacyRoot, "config.json");
                if (!File.Exists(legacyConfigPath))
                    return false;

                var legacy = Helpers.Json.Deserialize<LegacyConfig>(File.ReadAllText(legacyConfigPath));
                if (legacy == null)
                    return false;

                if (string.IsNullOrEmpty(config.AmongUsPath) && !string.IsNullOrEmpty(legacy.AmongUsPath))
                    config.AmongUsPath = legacy.AmongUsPath;
                if (!string.IsNullOrEmpty(legacy.GameChannel))
                    config.GameChannel = legacy.GameChannel;

                var legacyMods = Path.Combine(legacyRoot, "Mods");
                if (Directory.Exists(legacyMods))
                {
                    ModInstaller.CopyDirectoryContents(legacyMods, config.ModsFolder, true);
                }

                if (legacy.InstalledMods != null)
                    config.InstalledMods.AddRange(legacy.InstalledMods);
                if (legacy.SelectedMods != null)
                    config.SelectedMods.AddRange(legacy.SelectedMods);

                config.Save();
                return true;
            }
            catch
            {
                return false;
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
