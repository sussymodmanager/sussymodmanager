using SussyModManager.Core.Models;
using SussyModManager.Core.Services;

namespace SussyModManager.Tests;

public class LaunchSelectionTests
{
    private const string Registry = @"{
        ""version"": ""test"",
        ""mods"": [
            {
                ""id"": ""TownOfUs"",
                ""name"": ""Town of Us Mira"",
                ""category"": ""Mod"",
                ""dependencies"": [ { ""modId"": ""MiraAPI"" } ]
            },
            {
                ""id"": ""MiraAPI"",
                ""name"": ""Mira API"",
                ""category"": ""Dependency""
            },
            {
                ""id"": ""Reactor"",
                ""name"": ""Reactor"",
                ""category"": ""Dependency""
            },
            {
                ""id"": ""AleLuduMod"",
                ""name"": ""Ale Ludu Mod"",
                ""category"": ""Mod"",
                ""dependencies"": [ { ""modId"": ""Reactor"" } ]
            }
        ]
    }";

    private static (ModManager manager, Config config) CreateManager(params (string id, bool installed)[] mods)
    {
        var config = new Config();
        foreach (var (id, installed) in mods)
        {
            if (!installed)
                continue;
            config.InstalledMods.Add(new InstalledMod { Id = id, Name = id });
            SeedLaunchableFiles(config, id);
        }

        var store = new ModStore();
        store.LoadRegistryFromJson(Registry);
        return (new ModManager(config, store), config);
    }

    private static void SeedLaunchableFiles(Config config, string modId)
    {
        var dir = Path.Combine(config.ModsFolder, modId);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, modId + ".dll"), "stub");
    }

    [Fact]
    public void GetLaunchModIds_UncheckedMod_IsExcluded()
    {
        var (manager, config) = CreateManager(("TownOfUs", true), ("AleLuduMod", true), ("MiraAPI", true), ("Reactor", true));
        config.SelectedMods.Add("AleLuduMod");

        var launch = manager.GetLaunchModIds();

        Assert.Contains("AleLuduMod", launch);
        Assert.Contains("Reactor", launch);
        Assert.DoesNotContain("TownOfUs", launch);
        Assert.DoesNotContain("MiraAPI", launch);
    }

    [Fact]
    public void GetLaunchModIds_SelectedMod_PullsOnlyItsDependencies()
    {
        var (manager, config) = CreateManager(("TownOfUs", true), ("MiraAPI", true), ("Reactor", true));
        config.SelectedMods.Add("TownOfUs");

        var launch = manager.GetLaunchModIds();

        Assert.Contains("TownOfUs", launch);
        Assert.Contains("MiraAPI", launch);
        Assert.DoesNotContain("Reactor", launch);
    }

    [Fact]
    public void GetLaunchModIds_EmptySelection_IsEmpty()
    {
        var (manager, _) = CreateManager(("TownOfUs", true), ("MiraAPI", true));
        Assert.Empty(manager.GetLaunchModIds());
    }

    [Fact]
    public void PruneDependencySelections_RemovesDependencyCategoryFromSelectedMods()
    {
        var (manager, config) = CreateManager(("TownOfUs", true), ("MiraAPI", true), ("Reactor", true));
        config.SelectedMods.AddRange(new[] { "TownOfUs", "MiraAPI", "Reactor" });

        manager.PruneDependencySelections();

        Assert.Equal(new[] { "TownOfUs" }, config.SelectedMods);
        Assert.Equal(new[] { "TownOfUs", "MiraAPI" }, manager.GetLaunchModIds());
    }

    [Fact]
    public void SetLaunchSelection_PruneDependencies_AndSyncsLaunchIds()
    {
        var (manager, config) = CreateManager(("TownOfUs", true), ("MiraAPI", true), ("Reactor", true));
        manager.SetLaunchSelection(new[] { "TownOfUs", "MiraAPI", "Reactor" });

        Assert.Equal(new[] { "TownOfUs" }, config.SelectedMods);
        Assert.Equal(new[] { "TownOfUs", "MiraAPI" }, manager.GetLaunchModIds());
    }

    [Fact]
    public async Task InstallPresetAsync_DoesNotChangeLaunchSelection()
    {
        var config = new Config();
        config.InstalledMods.Add(new InstalledMod { Id = "TownOfUs", Name = "Town of Us Mira" });
        config.InstalledMods.Add(new InstalledMod { Id = "MiraAPI", Name = "Mira API" });
        SeedLaunchableFiles(config, "TownOfUs");
        SeedLaunchableFiles(config, "MiraAPI");

        var store = new ModStore();
        store.LoadRegistryFromJson(Registry);
        var manager = new ModManager(config, store);

        var preset = new Preset { ModIds = new List<string> { "TownOfUs" } };
        await manager.InstallPresetAsync(preset);

        Assert.Empty(config.SelectedMods);
        Assert.Null(config.ActivePackId);
    }

    [Fact]
    public async Task SelectPresetAsync_AddsPackModsWithoutDependencyCheckboxes()
    {
        var config = new Config();
        config.InstalledMods.Add(new InstalledMod { Id = "TownOfUs", Name = "Town of Us Mira" });
        config.InstalledMods.Add(new InstalledMod { Id = "MiraAPI", Name = "Mira API" });
        SeedLaunchableFiles(config, "TownOfUs");
        SeedLaunchableFiles(config, "MiraAPI");

        var store = new ModStore();
        store.LoadRegistryFromJson(Registry);
        var manager = new ModManager(config, store);

        var preset = new Preset { Id = "tou-pack", ModIds = new List<string> { "TownOfUs" } };
        await manager.SelectPresetAsync(preset);

        Assert.Single(config.SelectedMods);
        Assert.Equal("TownOfUs", config.SelectedMods[0]);
        Assert.Contains("MiraAPI", manager.GetLaunchModIds());
    }
}
