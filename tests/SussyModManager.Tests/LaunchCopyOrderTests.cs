using SussyModManager.Core.Models;
using SussyModManager.Core.Services;

namespace SussyModManager.Tests;

public class LaunchCopyOrderTests
{
    private const string Registry = @"{
        ""version"": ""1"",
        ""mods"": [
            { ""id"": ""TownOfUs"", ""name"": ""TOU"", ""category"": ""Mod"", ""packageType"": ""nested"" },
            { ""id"": ""MiraAPI"", ""name"": ""Mira"", ""category"": ""Dependency"" },
            { ""id"": ""VanillaEnhancements"", ""name"": ""VE"", ""category"": ""Mod"" }
        ]
    }";

    private static ModManager CreateManager()
    {
        var store = new ModStore();
        store.LoadRegistryFromJson(Registry);
        return new ModManager(new Config(), store);
    }

    [Fact]
    public void OrderForLaunchCopy_PutsNestedPackModsFirst()
    {
        var manager = CreateManager();
        var ordered = manager.OrderForLaunchCopy(new List<string>
        {
            "MiraAPI",
            "VanillaEnhancements",
            "TownOfUs"
        });

        Assert.Equal("TownOfUs", ordered[0]);
        Assert.Equal("MiraAPI", ordered[1]);
        Assert.Equal("VanillaEnhancements", ordered[2]);
    }

    [Fact]
    public void BepInExInstaller_UsesTouMiraBuild()
    {
        Assert.Equal(752, BepInExInstaller.BuildNumber);
    }
}
