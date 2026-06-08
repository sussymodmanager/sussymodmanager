using SussyModManager.Core.Models;
using SussyModManager.Core.Services;

namespace SussyModManager.Tests;

public class VanillaPlusLaunchTests
{
    private const string Registry = @"{
        ""version"": ""test"",
        ""mods"": [
            { ""id"": ""TownOfUs"", ""name"": ""TOU"", ""category"": ""Mod"", ""packageType"": ""nested"" },
            { ""id"": ""MiraAPI"", ""name"": ""Mira API"", ""category"": ""Dependency"" },
            { ""id"": ""Reactor"", ""name"": ""Reactor"", ""category"": ""Dependency"" },
            { ""id"": ""AUnlocker"", ""name"": ""AUnlocker"", ""category"": ""Mod"" },
            {
                ""id"": ""VanillaEnhancements"",
                ""name"": ""Vanilla Enhancements"",
                ""category"": ""Mod"",
                ""dependencies"": [ { ""modId"": ""Reactor"" } ]
            }
        ]
    }";

    [Fact]
    public void CopySelectedModsIntoGame_VanillaPlus_DoesNotLeaveTouStackPlugins()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm-vanilla-" + Guid.NewGuid().ToString("N"));
        var game = Path.Combine(root, "game");
        var data = Path.Combine(root, "data");
        var plugins = Path.Combine(game, "BepInEx", "plugins");
        Directory.CreateDirectory(Path.Combine(game, "BepInEx", "core"));
        File.WriteAllText(Path.Combine(game, "BepInEx", "core", "BepInEx.Core.dll"), "bep");
        File.WriteAllText(Path.Combine(game, "winhttp.dll"), "loader");
        File.WriteAllText(Path.Combine(game, "Among Us.exe"), "");
        Directory.CreateDirectory(plugins);

        // Simulate a prior TOU session leaving the full stack in plugins.
        File.WriteAllText(Path.Combine(plugins, "TownOfUs.dll"), "tou");
        File.WriteAllText(Path.Combine(plugins, "MiraAPI.dll"), "mira");
        File.WriteAllText(Path.Combine(plugins, "Reactor.dll"), "old-reactor");

        try
        {
            SeedMod(data, "TownOfUs", "TownOfUs.dll");
            SeedMod(data, "MiraAPI", "MiraAPI.dll");
            SeedMod(data, "Reactor", "Reactor.dll");
            SeedMod(data, "AUnlocker", "AUnlocker.dll");
            SeedMod(data, "VanillaEnhancements", "VanillaEnhancements.dll");

            var config = new Config { AmongUsPath = game, DataPath = data };
            foreach (var id in new[] { "TownOfUs", "MiraAPI", "Reactor", "AUnlocker", "VanillaEnhancements" })
                config.InstalledMods.Add(new InstalledMod { Id = id, Name = id });

            var store = new ModStore();
            store.LoadRegistryFromJson(Registry);
            var manager = new ModManager(config, store);

            manager.SetLaunchSelection(new[] { "AUnlocker", "VanillaEnhancements" });
            manager.CopySelectedModsIntoGame(strict: true);

            Assert.False(File.Exists(Path.Combine(plugins, "TownOfUs.dll")));
            Assert.False(File.Exists(Path.Combine(plugins, "MiraAPI.dll")));
            Assert.True(File.Exists(Path.Combine(plugins, "AUnlocker.dll")));
            Assert.True(File.Exists(Path.Combine(plugins, "VanillaEnhancements.dll")));
            // Vanilla Enhancements depends on Reactor per the registry.
            Assert.True(File.Exists(Path.Combine(plugins, "Reactor.dll")));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static void SeedMod(string dataRoot, string modId, string dllName)
    {
        var dir = Path.Combine(dataRoot, "Mods", modId);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, dllName), modId);
    }
}
