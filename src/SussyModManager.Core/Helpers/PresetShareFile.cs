using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SussyModManager.Core.Models;

namespace SussyModManager.Core.Helpers
{
    /// <summary>
    /// Serialize/deserialize a single shareable preset definition (name, mods, order).
    /// Omits id and builtin so imports always land as user presets.
    /// </summary>
    public static class PresetShareFile
    {
        public static string ToJson(Preset preset) => Json.Serialize(ToShareData(preset));

        public static PresetShareData ToShareData(Preset preset)
        {
            if (preset == null)
                return null;

            return new PresetShareData
            {
                Name = preset.Name,
                Description = preset.Description,
                ModIds = preset.ModIds?.ToList() ?? new List<string>(),
                InstallOrder = preset.InstallOrder != null && preset.InstallOrder.Count > 0
                    ? preset.InstallOrder.ToList()
                    : null
            };
        }

        public static Preset FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var share = Json.Deserialize<PresetShareData>(json);
            if (IsValidShare(share))
                return FromShareData(share);

            var preset = Json.Deserialize<Preset>(json);
            if (IsValidPreset(preset))
                return SanitizeImported(preset);

            var file = Json.Deserialize<PresetFile>(json);
            preset = file?.presets?.FirstOrDefault(IsValidPreset);
            return preset != null ? SanitizeImported(preset) : null;
        }

        public static void Write(Preset preset, string path) =>
            File.WriteAllText(path, ToJson(preset));

        public static Preset Read(string path) =>
            FromJson(File.ReadAllText(path));

        public static Preset FromShareData(PresetShareData share)
        {
            var now = DateTime.UtcNow.Ticks;
            return new Preset
            {
                Name = share.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(share.Description)
                    ? "Imported preset."
                    : share.Description.Trim(),
                ModIds = share.ModIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToList(),
                InstallOrder = share.InstallOrder?.Count > 0
                    ? share.InstallOrder.Where(id => !string.IsNullOrWhiteSpace(id)).ToList()
                    : null,
                Builtin = false,
                CreatedUtcTicks = now,
                UpdatedUtcTicks = now
            };
        }

        private static Preset SanitizeImported(Preset preset)
        {
            preset.Builtin = false;
            preset.Id = Guid.NewGuid().ToString("N");
            var now = DateTime.UtcNow.Ticks;
            preset.CreatedUtcTicks = now;
            preset.UpdatedUtcTicks = now;
            preset.ModIds = preset.ModIds?.Where(id => !string.IsNullOrWhiteSpace(id)).ToList()
                ?? new List<string>();
            if (preset.InstallOrder != null && preset.InstallOrder.Count == 0)
                preset.InstallOrder = null;
            if (string.IsNullOrWhiteSpace(preset.Description))
                preset.Description = "Imported preset.";
            preset.Name = preset.Name.Trim();
            return preset;
        }

        private static bool IsValidShare(PresetShareData share) =>
            share != null
            && !string.IsNullOrWhiteSpace(share.Name)
            && share.ModIds != null
            && share.ModIds.Any(id => !string.IsNullOrWhiteSpace(id));

        private static bool IsValidPreset(Preset preset) =>
            preset != null
            && !string.IsNullOrWhiteSpace(preset.Name)
            && preset.ModIds != null
            && preset.ModIds.Any(id => !string.IsNullOrWhiteSpace(id));
    }

    public class PresetShareData
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> ModIds { get; set; }
        public List<string> InstallOrder { get; set; }
    }
}
