using System.Collections.Generic;
using System.IO;
using System.Threading;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Platform;
using SussyModManager.Core.Services;

namespace SussyModManager.Core.Models
{
    public class Config
    {
        public string AmongUsPath { get; set; }
        public string DataPath { get; set; }
        public List<InstalledMod> InstalledMods { get; set; } = new List<InstalledMod>();
        public List<string> SelectedMods { get; set; } = new List<string>();
        public List<Preset> UserPresets { get; set; } = new List<Preset>();

        public bool AutoUpdateMods { get; set; }
        public bool ShowBetaVersions { get; set; }

        // Automatically download + apply SUSSYMODMANAGER app updates from GitHub. On by default.
        public bool AutoUpdateApp { get; set; } = true;

        public string ActiveColorProfileId { get; set; } = "sus-default";

        public bool FirstLaunchWizardCompleted { get; set; }
        public string GameChannel { get; set; } = GameChannels.Steam;

        // True once we've attempted importing legacy BeanModManager data.
        public bool LegacyImportAttempted { get; set; }

        // True while the background BeanModManager mod-file copy is still running.
        public bool LegacyModsCopyPending { get; set; }

        /// <summary>
        /// Optional path to a working Among Us install whose BepInEx/interop folder is copied into
        /// the live game before launch (fixes Reactor after Steam updates regenerate interop).
        /// </summary>
        public string InteropReferencePath { get; set; }

        private static readonly SemaphoreSlim SaveLock = new SemaphoreSlim(1, 1);

        private static string ConfigPath => Path.Combine(PlatformInfo.DataRoot, "config.json");

        public Config()
        {
            DataPath = PlatformInfo.DataRoot;
        }

        public string ModsFolder
        {
            get
            {
                var folder = Path.Combine(
                    string.IsNullOrEmpty(DataPath) ? PlatformInfo.DataRoot : DataPath, "Mods");
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        public static Config Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var config = Json.Deserialize<Config>(File.ReadAllText(ConfigPath));
                    if (config != null)
                    {
                        config.InstalledMods ??= new List<InstalledMod>();
                        config.SelectedMods ??= new List<string>();
                        config.UserPresets ??= new List<Preset>();
                        if (string.IsNullOrWhiteSpace(config.DataPath))
                            config.DataPath = PlatformInfo.DataRoot;
                        if (string.IsNullOrWhiteSpace(config.ActiveColorProfileId))
                            config.ActiveColorProfileId = "sus-default";
                        return config;
                    }
                }
            }
            catch
            {
            }

            return new Config();
        }

        public void Save()
        {
            SaveLock.Wait();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                var json = Json.Serialize(this);
                var tempPath = ConfigPath + ".tmp";
                File.WriteAllText(tempPath, json);
                if (File.Exists(ConfigPath))
                    File.Delete(ConfigPath);
                File.Move(tempPath, ConfigPath);
            }
            catch
            {
            }
            finally
            {
                SaveLock.Release();
            }
        }
    }
}
