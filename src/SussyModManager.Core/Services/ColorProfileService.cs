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
    /// Loads built-in color profiles (bundled JSON) and user-defined profiles stored under the
    /// data root, and persists user edits.
    /// </summary>
    public class ColorProfileService
    {
        private string UserProfilePath => Path.Combine(PlatformInfo.DataRoot, "color-profiles.user.json");

        public List<ColorProfile> LoadBuiltinProfiles()
        {
            var json = DataStore.Read("color-profiles.json");
            var file = Json.Deserialize<ColorProfileFile>(json);
            var profiles = file?.profiles ?? new List<ColorProfile>();
            foreach (var p in profiles)
                p.IsBuiltin = true;
            return profiles;
        }

        public List<ColorProfile> LoadUserProfiles()
        {
            try
            {
                if (!File.Exists(UserProfilePath))
                    return new List<ColorProfile>();
                var file = Json.Deserialize<ColorProfileFile>(File.ReadAllText(UserProfilePath));
                var profiles = file?.profiles ?? new List<ColorProfile>();
                foreach (var p in profiles)
                    p.IsBuiltin = false;
                return profiles;
            }
            catch
            {
                return new List<ColorProfile>();
            }
        }

        public List<ColorProfile> GetAllProfiles()
        {
            var all = new List<ColorProfile>(LoadBuiltinProfiles());
            all.AddRange(LoadUserProfiles());
            return all;
        }

        public ColorProfile GetProfileOrDefault(string id)
        {
            var all = GetAllProfiles();
            return all.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
                ?? all.FirstOrDefault()
                ?? DefaultProfile();
        }

        public void SaveUserProfiles(IEnumerable<ColorProfile> profiles)
        {
            try
            {
                var file = new ColorProfileFile { profiles = profiles.ToList() };
                File.WriteAllText(UserProfilePath, Json.Serialize(file));
            }
            catch
            {
            }
        }

        public void UpsertUserProfile(ColorProfile profile)
        {
            var profiles = LoadUserProfiles();
            profiles.RemoveAll(p => string.Equals(p.Id, profile.Id, StringComparison.OrdinalIgnoreCase));
            profile.IsBuiltin = false;
            profiles.Add(profile);
            SaveUserProfiles(profiles);
        }

        public void DeleteUserProfile(string id)
        {
            var profiles = LoadUserProfiles();
            profiles.RemoveAll(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
            SaveUserProfiles(profiles);
        }

        private static ColorProfile DefaultProfile() => new ColorProfile
        {
            Id = "sus-default",
            Name = "Sus Default",
            IsBuiltin = true,
            Variant = "Dark",
            Accent = "#FF8B5CF6",
            AccentSecondary = "#FFEC4899",
            Background = "#FF0F0E17",
            Surface = "#FF1A1825",
            SurfaceElevated = "#FF252233",
            CardBorder = "#FF332F45",
            TextPrimary = "#FFF5F3FF",
            TextMuted = "#FF9A93B8",
            Success = "#FF34D399",
            Warning = "#FFFBBF24",
            Danger = "#FFF87171",
            Glow = "#668B5CF6"
        };
    }
}
