using SussyModManager.Core.Helpers;
using SussyModManager.Core.Models;

namespace SussyModManager.Tests;

public class ThemeEditorTests
{
    [Theory]
    [InlineData("#FF8B5CF6", "#FF8B5CF6")]
    [InlineData("#8B5CF6", "#FF8B5CF6")]
    [InlineData("8B5CF6", "#FF8B5CF6")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void Normalize_ExpandsRgbToArgb(string input, string expected)
    {
        Assert.Equal(expected, ColorHex.Normalize(input));
    }

    [Fact]
    public void FromBaseColors_FillsDerivedTokens()
    {
        var profile = ThemePalette.FromBaseColors(
            "#FF8B5CF6", "#FFEC4899", "#FF1A1825", "#FF0F0E17",
            isLight: false, "test", "Test");

        Assert.Equal("#FF8B5CF6", profile.Accent);
        Assert.Equal("#FF1A1825", profile.Surface);
        Assert.False(string.IsNullOrEmpty(profile.SurfaceElevated));
        Assert.False(string.IsNullOrEmpty(profile.TextPrimary));
        Assert.Equal("#FF34D399", profile.Success);
        Assert.StartsWith("#66", profile.Glow);
    }

    [Fact]
    public void UsesCustomTokens_DetectsManualOverrides()
    {
        var derived = ThemePalette.FromBaseColors(
            "#FF8B5CF6", "#FFEC4899", "#FF1A1825", "#FF0F0E17",
            false, "x", "X");
        Assert.False(ThemePalette.UsesCustomTokens(derived));

        derived.Danger = "#FFFF0000";
        Assert.True(ThemePalette.UsesCustomTokens(derived));
    }

    [Fact]
    public void ThemeFile_RoundTripsProfile()
    {
        var original = ThemePalette.FromBaseColors(
            "#FF00FF00", "#FF0000FF", "#FF222222", "#FF111111",
            false, "custom-abc", "Green");

        var json = ThemeFile.ToJson(original);
        var loaded = ThemeFile.FromJson(json);

        Assert.NotNull(loaded);
        Assert.Equal(original.Accent, loaded.Accent);
        Assert.Equal(original.Surface, loaded.Surface);
    }
}
