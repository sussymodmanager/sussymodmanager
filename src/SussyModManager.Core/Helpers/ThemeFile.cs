using System;
using System.IO;
using System.Linq;
using SussyModManager.Core.Models;

namespace SussyModManager.Core.Helpers
{
    public static class ThemeFile
    {
        public static string ToJson(ColorProfile profile) => Json.Serialize(profile);

        public static ColorProfile FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var profile = Json.Deserialize<ColorProfile>(json);
            if (IsValid(profile))
                return profile;

            var file = Json.Deserialize<ColorProfileFile>(json);
            profile = file?.profiles?.FirstOrDefault(IsValid);
            return profile;
        }

        public static void Write(ColorProfile profile, string path) =>
            File.WriteAllText(path, ToJson(profile));

        public static ColorProfile Read(string path) =>
            FromJson(File.ReadAllText(path));

        private static bool IsValid(ColorProfile profile) =>
            profile != null
            && !string.IsNullOrWhiteSpace(profile.Accent)
            && !string.IsNullOrWhiteSpace(profile.Background)
            && !string.IsNullOrWhiteSpace(profile.Surface);
    }
}
