using SussyModManager.Core.Models;
using SussyModManager.Core.Services;

namespace SussyModManager.Tests;

public class VanillaEnhancementsReactorTests
{
    private const string Registry = """
        {
          "version": "test",
          "mods": [
            { "id": "TownOfUs", "name": "TOU", "category": "Mod", "packageType": "nested" },
            { "id": "Reactor", "name": "Reactor", "category": "Dependency",
              "githubOwner": "NuclearPowered", "githubRepo": "Reactor",
              "assetFilters": { "dll": { "patterns": ["Reactor.dll"], "exactMatch": true } } },
            {
              "id": "VanillaEnhancements", "name": "Vanilla Enhancements", "category": "Mod",
              "githubOwner": "xChipseq", "githubRepo": "VanillaEnhancements",
              "assetFilters": { "dll": { "patterns": ["VanillaEnhancements.dll"], "exactMatch": true } },
              "dependencies": [
                { "modId": "Reactor", "fileName": "Reactor.dll", "githubOwner": "NuclearPowered", "githubRepo": "Reactor", "requiredVersion": ">=2.5.0" }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void ExpandWithDependencies_IncludesReactorForVanillaEnhancements()
    {
        var config = new Config { DataPath = Path.GetTempPath() };
        var store = new ModStore();
        store.LoadRegistryFromJson(Registry);
        var manager = new ModManager(config, store);

        var expanded = manager.ExpandWithDependencies(new[] { "VanillaEnhancements" });

        Assert.Contains(expanded, id => string.Equals(id, "Reactor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HasReactorAvailableForLaunch_TrueWhenBundledInTownOfUs()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm-reactor-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "data");
        var touPlugins = Path.Combine(data, "Mods", "TownOfUs", "BepInEx", "plugins");
        Directory.CreateDirectory(touPlugins);
        File.WriteAllText(Path.Combine(touPlugins, "Reactor.dll"), "reactor");

        try
        {
            var config = new Config { DataPath = data };
            config.InstalledMods.Add(new InstalledMod { Id = "TownOfUs", Name = "TOU" });

            var store = new ModStore();
            store.LoadRegistryFromJson(Registry);
            var manager = new ModManager(config, store);

            Assert.True(manager.HasReactorAvailableForLaunch());
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void CopySelectedModsIntoGame_SusAfSelection_DeploysReactorWithVanillaEnhancements()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm-susaf-" + Guid.NewGuid().ToString("N"));
        var game = Path.Combine(root, "game");
        var data = Path.Combine(root, "data");
        var plugins = Path.Combine(game, "BepInEx", "plugins");
        Directory.CreateDirectory(Path.Combine(game, "BepInEx", "core"));
        File.WriteAllText(Path.Combine(game, "BepInEx", "core", "BepInEx.Core.dll"), "bep");
        File.WriteAllText(Path.Combine(game, "winhttp.dll"), "loader");
        File.WriteAllText(Path.Combine(game, "Among Us.exe"), "");
        Directory.CreateDirectory(plugins);

        try
        {
            SeedMod(data, "Reactor", "Reactor.dll");
            SeedMod(data, "VanillaEnhancements", "VanillaEnhancements.dll");

            var config = new Config { AmongUsPath = game, DataPath = data };
            foreach (var id in new[] { "Reactor", "VanillaEnhancements" })
                config.InstalledMods.Add(new InstalledMod { Id = id, Name = id });

            var store = new ModStore();
            store.LoadRegistryFromJson(Registry);
            var manager = new ModManager(config, store);

            manager.SetLaunchSelection(new[] { "VanillaEnhancements" });
            manager.CopySelectedModsIntoGame(strict: true);

            Assert.True(File.Exists(Path.Combine(plugins, "Reactor.dll")));
            Assert.True(File.Exists(Path.Combine(plugins, "VanillaEnhancements.dll")));
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
