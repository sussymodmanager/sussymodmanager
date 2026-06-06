using SussyModManager.Core.Models;
using SussyModManager.Core.Services;

namespace SussyModManager.Tests;

public class ModInstallCacheTests
{
    private const string Registry = @"{
        ""version"": ""test"",
        ""mods"": [
            {
                ""id"": ""TestMod"",
                ""name"": ""Test Mod"",
                ""category"": ""Mod"",
                ""githubOwner"": ""x"",
                ""githubRepo"": ""y""
            }
        ]
    }";

    [Fact]
    public async Task InstallModAsync_SkipsWhenSameVersionAlreadyOnDisk()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm-cache-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "data");
        var modDir = Path.Combine(data, "Mods", "TestMod");
        Directory.CreateDirectory(modDir);
        File.WriteAllText(Path.Combine(modDir, "TestMod.dll"), "ok");

        try
        {
            var config = new Config { DataPath = data };
            config.InstalledMods.Add(new InstalledMod
            {
                Id = "TestMod",
                Name = "Test Mod",
                Version = "1.0.0",
                ReleaseTag = "v1.0.0"
            });

            var store = new ModStore();
            store.LoadRegistryFromJson(Registry);
            var manager = new ModManager(config, store);

            var result = await manager.InstallModAsync("TestMod", new ModVersion
            {
                Version = "1.0.0",
                ReleaseTag = "v1.0.0",
                DownloadUrl = "https://example.com/TestMod.dll"
            });

            Assert.True(result.Success);
            Assert.Contains("already installed", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
