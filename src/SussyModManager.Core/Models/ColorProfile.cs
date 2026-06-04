using System.Collections.Generic;

namespace SussyModManager.Core.Models
{
    /// <summary>
    /// A token-based theme. Every value is a hex color string (e.g. "#FF8B5CF6") so profiles
    /// can be serialized, shared, and edited without any platform-specific color types.
    /// </summary>
    public class ColorProfile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsBuiltin { get; set; }

        // "Dark" or "Light" - drives the base Avalonia theme variant.
        public string Variant { get; set; } = "Dark";

        public string Accent { get; set; }
        public string AccentSecondary { get; set; }
        public string Background { get; set; }
        public string Surface { get; set; }
        public string SurfaceElevated { get; set; }
        public string CardBorder { get; set; }
        public string TextPrimary { get; set; }
        public string TextMuted { get; set; }
        public string Success { get; set; }
        public string Warning { get; set; }
        public string Danger { get; set; }
        public string Glow { get; set; }
    }

    public class ColorProfileFile
    {
        public List<ColorProfile> profiles { get; set; }
    }
}
