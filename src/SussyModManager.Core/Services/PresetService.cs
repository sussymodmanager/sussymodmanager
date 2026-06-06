using System;
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

        /// <summary>
        /// Adds or replaces a user preset by id. Never touches built-in presets.
        /// </summary>
        public Preset UpsertUserPreset(Config config, Preset preset)
        {
            config.UserPresets ??= new List<Preset>();
            preset.Builtin = false;

            var builtinIds = LoadBuiltinPresets()
                .Select(p => p.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (builtinIds.Contains(preset.Id))
                preset.Id = Guid.NewGuid().ToString("N");

            var existingByName = config.UserPresets.FirstOrDefault(p =>
                string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase));
            if (existingByName != null)
            {
                preset.Id = existingByName.Id;
                preset.CreatedUtcTicks = existingByName.CreatedUtcTicks;
            }

            config.UserPresets.RemoveAll(p =>
                string.Equals(p.Id, preset.Id, StringComparison.OrdinalIgnoreCase));
            preset.UpdatedUtcTicks = DateTime.UtcNow.Ticks;
            config.UserPresets.Add(preset);
            return preset;
        }
    }
}
