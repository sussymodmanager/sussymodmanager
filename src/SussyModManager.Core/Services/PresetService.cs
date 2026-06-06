using System.Collections.Generic;
using System.IO;
using System.Linq;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Models;
using SussyModManager.Core.Platform;

namespace SussyModManager.Core.Services
{
    /// <summary>
    /// Loads built-in presets (bundled, includes SUS AF PACK) and merges them with the user's
    /// own presets stored in config.
    /// </summary>
    public class PresetService
    {
        public List<Preset> LoadBuiltinPresets()
        {
            var json = DataStore.ReadBundled("builtin-presets.json") ?? DataStore.Read("builtin-presets.json");
            var file = Json.Deserialize<PresetFile>(json);
            var presets = file?.presets ?? new List<Preset>();
            foreach (var preset in presets)
                preset.Builtin = true;
            return presets;
        }

        public List<Preset> GetAllPresets(Config config)
        {
            var all = new List<Preset>(LoadBuiltinPresets());
            if (config?.UserPresets != null)
                all.AddRange(config.UserPresets);
            return all;
        }
    }
}
