using SussyModManager.Core.Models;
using SussyModManager.Core.Services;

namespace SussyModManager.Tests;

public class ResyncActivePluginsTests
{
    private const string Registry = @"{
        ""version"": ""test"",
        ""mods"": [
            { ""id"": ""AleLuduMod"", ""name"": ""Ale Ludu"", ""category"": ""Mod"" },
            { ""id"": ""TownOfUs"", ""name"": ""TOU"", ""category"": ""Mod"" }
        ]
    }";

    [Fact]
    public void ResyncActivePlugins_CopiesOnlySelectedMods()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm-test-" + Guid.NewGuid().ToString("N"));
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
            var aleDir = Path.Combine(data, "Mods", "AleLuduMod");
            var touDir = Path.Combine(data, "Mods", "TownOfUs");
            Directory.CreateDirectory(aleDir);
            Directory.CreateDirectory(touDir);
            File.WriteAllText(Path.Combine(aleDir, "Ale.dll"), "ale");
            File.WriteAllText(Path.Combine(touDir, "Tou.dll"), "tou");
            File.WriteAllText(Path.Combine(plugins, "Stale.dll"), "stale");

            var config = new Config { AmongUsPath = game, DataPath = data };
            config.InstalledMods.Add(new InstalledMod { Id = "AleLuduMod", Name = "Ale" });
            config.InstalledMods.Add(new InstalledMod { Id = "TownOfUs", Name = "TOU" });
            config.SelectedMods.Add("AleLuduMod");

            var store = new ModStore();
            store.LoadRegistryFromJson(Registry);
            var manager = new ModManager(config, store);

            manager.ResyncActivePlugins();

            Assert.True(File.Exists(Path.Combine(plugins, "Ale.dll")));
            Assert.False(File.Exists(Path.Combine(plugins, "Tou.dll")));
            Assert.False(File.Exists(Path.Combine(plugins, "Stale.dll")));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
