using SussyModManager.Core.Models;

namespace SussyModManager.Tests;

public class ConfigDefaultsTests
{
    [Fact]
    public void NewConfig_AutoUpdateModsEnabledByDefault()
    {
        var config = new Config();
        Assert.True(config.AutoUpdateMods);
        Assert.False(config.AutoUpdateModsOptOut);
    }

    [Fact]
    public void LoadMigration_EnablesAutoUpdateMods_WhenNotExplicitlyOptedOut()
    {
        var config = new Config { AutoUpdateMods = false, AutoUpdateModsOptOut = false };
        config.AutoUpdateMods = !config.AutoUpdateModsOptOut;
        Assert.True(config.AutoUpdateMods);
    }

    [Fact]
    public void LoadMigration_RespectsAutoUpdateModsOptOut()
    {
        var config = new Config { AutoUpdateModsOptOut = true };
        config.AutoUpdateMods = !config.AutoUpdateModsOptOut;
        Assert.False(config.AutoUpdateMods);
    }
}
