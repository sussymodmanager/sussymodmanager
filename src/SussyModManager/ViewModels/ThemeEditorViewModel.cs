using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Models;
using SussyModManager.Services;

namespace SussyModManager.ViewModels
{
    public partial class ThemeEditorViewModel : ViewModelBase
    {
        private bool _suppressLiveApply;

        [ObservableProperty] private bool _isLight;
        [ObservableProperty] private bool _showAdvanced;

        public ThemeColorTokenViewModel Accent { get; }
        public ThemeColorTokenViewModel AccentSecondary { get; }
        public ThemeColorTokenViewModel Surface { get; }
        public ThemeColorTokenViewModel Background { get; }

        public ObservableCollection<ThemeColorTokenViewModel> AdvancedTokens { get; } =
            new ObservableCollection<ThemeColorTokenViewModel>();

        public ThemeEditorViewModel()
        {
            Accent = new ThemeColorTokenViewModel("Accent", "#FF8B5CF6", null, ApplyLive);
            AccentSecondary = new ThemeColorTokenViewModel("Accent (secondary)", "#FFEC4899", null, ApplyLive);
            Surface = new ThemeColorTokenViewModel("Surface", "#FF1A1825", null, ApplyLive);
            Background = new ThemeColorTokenViewModel("Background", "#FF0F0E17", null, ApplyLive);

            AdvancedTokens.Add(new ThemeColorTokenViewModel("Surface (elevated)", "#FF252233", null, ApplyLive));
            AdvancedTokens.Add(new ThemeColorTokenViewModel("Card border", "#FF332F45", null, ApplyLive));
            AdvancedTokens.Add(new ThemeColorTokenViewModel("Text (primary)", "#FFF5F3FF", null, ApplyLive));
            AdvancedTokens.Add(new ThemeColorTokenViewModel("Text (muted)", "#FF9A93B8", null, ApplyLive));
            AdvancedTokens.Add(new ThemeColorTokenViewModel("Success", "#FF34D399", null, ApplyLive));
            AdvancedTokens.Add(new ThemeColorTokenViewModel("Warning", "#FFFBBF24", null, ApplyLive));
            AdvancedTokens.Add(new ThemeColorTokenViewModel("Danger", "#FFF87171", null, ApplyLive));
            AdvancedTokens.Add(new ThemeColorTokenViewModel("Glow", "#668B5CF6", null, ApplyLive));
        }

        public void LoadFrom(ColorProfile profile)
        {
            if (profile == null)
                return;

            _suppressLiveApply = true;
            IsLight = string.Equals(profile.Variant, "Light", System.StringComparison.OrdinalIgnoreCase);
            Accent.Value = profile.Accent;
            AccentSecondary.Value = profile.AccentSecondary;
            Surface.Value = profile.Surface;
            Background.Value = profile.Background;

            ShowAdvanced = !profile.IsBuiltin && ThemePalette.UsesCustomTokens(profile);
            if (ShowAdvanced)
            {
                AdvancedTokens[0].Value = profile.SurfaceElevated;
                AdvancedTokens[1].Value = profile.CardBorder;
                AdvancedTokens[2].Value = profile.TextPrimary;
                AdvancedTokens[3].Value = profile.TextMuted;
                AdvancedTokens[4].Value = profile.Success;
                AdvancedTokens[5].Value = profile.Warning;
                AdvancedTokens[6].Value = profile.Danger;
                AdvancedTokens[7].Value = profile.Glow;
            }

            _suppressLiveApply = false;
        }

        public ColorProfile BuildProfile(string id, string name, bool builtin = false)
        {
            if (!ShowAdvanced)
            {
                var profile = ThemePalette.FromBaseColors(
                    Accent.Value, AccentSecondary.Value, Surface.Value, Background.Value,
                    IsLight, id, name);
                profile.IsBuiltin = builtin;
                return profile;
            }

            return new ColorProfile
            {
                Id = id,
                Name = name,
                IsBuiltin = builtin,
                Variant = IsLight ? "Light" : "Dark",
                Accent = ColorHex.Normalize(Accent.Value),
                AccentSecondary = ColorHex.Normalize(AccentSecondary.Value),
                Background = ColorHex.Normalize(Background.Value),
                Surface = ColorHex.Normalize(Surface.Value),
                SurfaceElevated = ColorHex.Normalize(AdvancedTokens[0].Value),
                CardBorder = ColorHex.Normalize(AdvancedTokens[1].Value),
                TextPrimary = ColorHex.Normalize(AdvancedTokens[2].Value),
                TextMuted = ColorHex.Normalize(AdvancedTokens[3].Value),
                Success = ColorHex.Normalize(AdvancedTokens[4].Value),
                Warning = ColorHex.Normalize(AdvancedTokens[5].Value),
                Danger = ColorHex.Normalize(AdvancedTokens[6].Value),
                Glow = ColorHex.Normalize(AdvancedTokens[7].Value)
            };
        }

        private void ApplyLive()
        {
            if (_suppressLiveApply)
                return;
            ThemeService.Apply(BuildProfile("live-preview", "Live preview"));
        }

        partial void OnIsLightChanged(bool value) => ApplyLive();

        partial void OnShowAdvancedChanged(bool value)
        {
            if (_suppressLiveApply || !value)
                return;

            var derived = ThemePalette.FromBaseColors(
                Accent.Value, AccentSecondary.Value, Surface.Value, Background.Value,
                IsLight, "derived", "derived");
            AdvancedTokens[0].Value = derived.SurfaceElevated;
            AdvancedTokens[1].Value = derived.CardBorder;
            AdvancedTokens[2].Value = derived.TextPrimary;
            AdvancedTokens[3].Value = derived.TextMuted;
            AdvancedTokens[4].Value = derived.Success;
            AdvancedTokens[5].Value = derived.Warning;
            AdvancedTokens[6].Value = derived.Danger;
            AdvancedTokens[7].Value = derived.Glow;
            ApplyLive();
        }
    }
}
