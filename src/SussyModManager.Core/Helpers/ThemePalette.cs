using System;
using SussyModManager.Core.Models;

namespace SussyModManager.Core.Helpers
{
    /// <summary>
    /// Builds a full <see cref="ColorProfile"/> from the four swatch colors shown in the UI.
    /// </summary>
    public static class ThemePalette
    {
        public static ColorProfile FromBaseColors(string accent, string accentSecondary, string surface,
            string background, bool isLight, string id, string name)
        {
            accent = ColorHex.Normalize(accent);
            accentSecondary = ColorHex.Normalize(accentSecondary);
            surface = ColorHex.Normalize(surface);
            background = ColorHex.Normalize(background);

            var (sa, sr, sg, sb) = ParseArgb(surface);
            var elevated = Shift(sa, sr, sg, sb, isLight ? -18 : 14);
            var border = Shift(sa, elevated.r, elevated.g, elevated.b, isLight ? -12 : 10);

            var textPrimary = isLight ? "#FF111827" : "#FFF5F3FF";
            var (ta, tr, tg, tb) = ParseArgb(textPrimary);
            var (ma, mr, mg, mb) = ParseArgb(surface);
            var textMuted = ToHex(ta,
                Blend(tr, mr, 0.45),
                Blend(tg, mg, 0.45),
                Blend(tb, mb, 0.45));

            var (aa, ar, ag, ab) = ParseArgb(accent);
            var glow = ToHex(0x66, ar, ag, ab);

            return new ColorProfile
            {
                Id = id,
                Name = name,
                IsBuiltin = false,
                Variant = isLight ? "Light" : "Dark",
                Accent = accent,
                AccentSecondary = accentSecondary,
                Background = background,
                Surface = surface,
                SurfaceElevated = ToHex(elevated.a, elevated.r, elevated.g, elevated.b),
                CardBorder = ToHex(border.a, border.r, border.g, border.b),
                TextPrimary = textPrimary,
                TextMuted = textMuted,
                Success = "#FF34D399",
                Warning = "#FFFBBF24",
                Danger = "#FFF87171",
                Glow = glow
            };
        }

        public static bool UsesCustomTokens(ColorProfile profile)
        {
            if (profile == null)
                return false;

            var derived = FromBaseColors(
                profile.Accent,
                profile.AccentSecondary,
                profile.Surface,
                profile.Background,
                string.Equals(profile.Variant, "Light", StringComparison.OrdinalIgnoreCase),
                profile.Id,
                profile.Name);

            return !Equals(profile.SurfaceElevated, derived.SurfaceElevated)
                || !Equals(profile.CardBorder, derived.CardBorder)
                || !Equals(profile.TextPrimary, derived.TextPrimary)
                || !Equals(profile.TextMuted, derived.TextMuted)
                || !Equals(profile.Success, derived.Success)
                || !Equals(profile.Warning, derived.Warning)
                || !Equals(profile.Danger, derived.Danger)
                || !Equals(profile.Glow, derived.Glow);
        }

        private static bool Equals(string a, string b) =>
            string.Equals(ColorHex.Normalize(a), ColorHex.Normalize(b), StringComparison.OrdinalIgnoreCase);

        private static (byte a, byte r, byte g, byte b) ParseArgb(string hex)
        {
            hex = ColorHex.Normalize(hex) ?? "#FF000000";
            if (hex.Length != 9)
                return (255, 0, 0, 0);

            var value = Convert.ToUInt32(hex.Substring(1), 16);
            return ((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);
        }

        private static string ToHex(byte a, byte r, byte g, byte b) =>
            $"#{a:X2}{r:X2}{g:X2}{b:X2}";

        private static (byte a, byte r, byte g, byte b) Shift(byte a, byte r, byte g, byte b, int amount)
        {
            return (a,
                Clamp(r + amount),
                Clamp(g + amount),
                Clamp(b + amount));
        }

        private static byte Blend(byte a, byte b, double weight) =>
            Clamp((int)(a + (b - a) * weight));

        private static byte Clamp(int value) =>
            value < 0 ? (byte)0 : value > 255 ? (byte)255 : (byte)value;
    }
}
