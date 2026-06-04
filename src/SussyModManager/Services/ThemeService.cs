using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using SussyModManager.Core.Models;

namespace SussyModManager.Services
{
    /// <summary>
    /// Applies a <see cref="ColorProfile"/> to the live application resources so the whole UI
    /// recolors instantly when the user picks or tweaks a profile.
    /// </summary>
    public static class ThemeService
    {
        public static void Apply(ColorProfile profile)
        {
            if (profile == null || Application.Current == null)
                return;

            var res = Application.Current.Resources;

            Set(res, "AccentBrush", profile.Accent);
            Set(res, "AccentSecondaryBrush", profile.AccentSecondary);
            Set(res, "BackgroundBrush", profile.Background);
            Set(res, "SurfaceBrush", profile.Surface);
            Set(res, "SurfaceElevatedBrush", profile.SurfaceElevated);
            Set(res, "CardBorderBrush", profile.CardBorder);
            Set(res, "TextPrimaryBrush", profile.TextPrimary);
            Set(res, "TextMutedBrush", profile.TextMuted);
            Set(res, "SuccessBrush", profile.Success);
            Set(res, "WarningBrush", profile.Warning);
            Set(res, "DangerBrush", profile.Danger);
            Set(res, "GlowBrush", profile.Glow);

            Application.Current.RequestedThemeVariant =
                string.Equals(profile.Variant, "Light", StringComparison.OrdinalIgnoreCase)
                    ? ThemeVariant.Light
                    : ThemeVariant.Dark;
        }

        private static void Set(IResourceDictionary res, string key, string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return;
            try
            {
                res[key] = new SolidColorBrush(Color.Parse(hex));
            }
            catch
            {
            }
        }
    }
}
