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
    /// Loads built-in presets (bundled, includes SUS AF) and merges them with the user's
    /// own presets stored in config.
    /// </summary>
    public class PresetService
    {
        public List<Preset> LoadBuiltinPresets()
        {
            // Cached remote copy first (updated on launch from GitHub), then bundled next to the exe.
            var json = DataStore.Read("builtin-presets.json");
            var file = Json.Deserialize<PresetFile>(json);
            var presets = file?.presets ?? new List<Preset>();
            DataStore.ApplyBundledPresetDisplayOverrides(presets);
            foreach (var preset in presets)
                preset.Builtin = true;
            return presets;
        }

        public List<Preset> GetAllPresets(Config config)
        {
            var all = new List<Preset>(LoadBuiltinPresets());
            if (config?.UserPresets != null)
                all.AddRange(config.UserPresets);

            return all
                .OrderByDescending(p => p.Pinned)
                .ThenByDescending(p => p.Builtin)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Finds a preset by id in the latest built-in list or user presets.</summary>
        public Preset GetPresetById(string id, Config config)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            var builtin = LoadBuiltinPresets().FirstOrDefault(p =>
                string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
            if (builtin != null)
                return builtin;

            return config?.UserPresets?.FirstOrDefault(p =>
                string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Finds a built-in pack whose mod list exactly matches installed mods (prefers SUS AF).
        /// </summary>
        public Preset FindInstalledPackMatch(Config config)
        {
            if (config?.InstalledMods == null || config.InstalledMods.Count == 0)
                return null;

            var installed = config.InstalledMods
                .Select(m => m.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Preset fallback = null;
            foreach (var preset in LoadBuiltinPresets())
            {
                var packIds = (preset.ModIds ?? new List<string>())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (packIds.Count == 0 || !installed.SetEquals(packIds))
                    continue;

                if (string.Equals(preset.Id, "sus-af-pack", StringComparison.OrdinalIgnoreCase))
                    return preset;

                fallback ??= preset;
            }

            return fallback;
        }

        /// <summary>
        /// Returns the latest definition for a preset (built-ins reload from DataStore; user presets
        /// reload from config) so play/install uses current mod lists instead of a stale VM copy.
        /// </summary>
        public Preset ResolveFreshPreset(Preset preset, Config config)
        {
            if (preset == null)
                return null;

            if (!string.IsNullOrEmpty(preset.Id))
            {
                var fresh = GetPresetById(preset.Id, config);
                if (fresh != null)
                    return fresh;
            }

            return preset;
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
