using SussyModManager.Core.Helpers;
using SussyModManager.Core.Models;
using SussyModManager.Core.Services;

namespace SussyModManager.Tests;

public class PresetPlaySyncTests
{
    private const string Registry = @"{
        ""version"": ""test"",
        ""mods"": [
            { ""id"": ""TownOfUs"", ""name"": ""TOU"", ""category"": ""Mod"" },
            { ""id"": ""PackModA"", ""name"": ""Pack Mod A"", ""category"": ""Mod"" },
            { ""id"": ""PackModB"", ""name"": ""Pack Mod B"", ""category"": ""Mod"" }
        ]
    }";

    [Fact]
    public void ResolveFreshPreset_ReturnsUpdatedBuiltinModList()
    {
        var storePath = Path.Combine(DataStore.StoreDir, "builtin-presets.json");
        string previous = null;
        if (File.Exists(storePath))
            previous = File.ReadAllText(storePath);

        try
        {
            var json = """
                {
                  "presets": [
                    {
                      "id": "test-fresh-pack",
                      "name": "Fresh test pack",
                      "description": "test",
                      "modIds": ["TownOfUs", "PackModA", "PackModB"],
                      "installOrder": ["TownOfUs", "PackModA", "PackModB"]
                    }
                  ]
                }
                """;
            File.WriteAllText(storePath, json);

            var service = new PresetService();
            var stale = new Preset
            {
                Id = "test-fresh-pack",
                Name = "Fresh test pack",
                Builtin = true,
                ModIds = new List<string> { "TownOfUs" }
            };

            var fresh = service.ResolveFreshPreset(stale, new Config());

            Assert.NotNull(fresh);
            Assert.Equal(new[] { "TownOfUs", "PackModA", "PackModB" }, fresh.ModIds);
        }
        finally
        {
            if (previous == null)
            {
                try { File.Delete(storePath); } catch { }
            }
            else
            {
                File.WriteAllText(storePath, previous);
            }
        }
    }

    [Fact]
    public void GetPresetById_LoadsUserPresetFromConfig()
    {
        var service = new PresetService();
        var config = new Config
        {
            UserPresets = new List<Preset>
            {
                new Preset
                {
                    Id = "user-pack",
                    Name = "My Pack",
                    ModIds = new List<string> { "PackModA" }
                }
            }
        };

        var found = service.GetPresetById("user-pack", config);

        Assert.NotNull(found);
        Assert.Equal("My Pack", found.Name);
        Assert.Single(found.ModIds);
    }

    [Fact]
    public async Task SyncPresetModsForPlayAsync_SkipsInstalledMods()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm-play-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "data");

        try
        {
            SeedMod(data, "TownOfUs", "TownOfUs.dll");
            SeedMod(data, "PackModA", "PackModA.dll");

            var config = new Config { DataPath = data };
            config.InstalledMods.Add(new InstalledMod { Id = "TownOfUs", Name = "TOU", ReleaseTag = "v1.0.0" });
            config.InstalledMods.Add(new InstalledMod { Id = "PackModA", Name = "Pack Mod A", ReleaseTag = "v1.0.0" });

            var store = new ModStore();
            store.LoadRegistryFromJson(Registry);
            var manager = new ModManager(config, store);

            var preset = new Preset
            {
                Name = "Test Pack",
                ModIds = new List<string> { "TownOfUs", "PackModA" },
                InstallOrder = new List<string> { "TownOfUs", "PackModA" }
            };

            var result = await manager.SyncPresetModsForPlayAsync(preset);

            Assert.True(result.Success);
            Assert.Equal(2, config.InstalledMods.Count);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task InstallPresetAsync_DoesNotForceUpdateWhenAlreadyInstalled()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm-install-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "data");

        try
        {
            SeedMod(data, "TownOfUs", "TownOfUs.dll");

            var config = new Config { DataPath = data };
            config.InstalledMods.Add(new InstalledMod
            {
                Id = "TownOfUs",
                Name = "TOU",
                Version = "1.0.0",
                ReleaseTag = "v1.0.0"
            });

            var store = new ModStore();
            store.LoadRegistryFromJson(Registry);
            var manager = new ModManager(config, store);

            var preset = new Preset
            {
                Name = "Test Pack",
                ModIds = new List<string> { "TownOfUs" }
            };

            var result = await manager.InstallPresetAsync(preset);

            Assert.True(result.Success);
            Assert.Equal("v1.0.0", config.InstalledMods[0].ReleaseTag);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void PlayPresetAsync_SelectsFreshModIdsOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm-select-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "data");

        try
        {
            SeedMod(data, "TownOfUs", "TownOfUs.dll");
            SeedMod(data, "MiraAPI", "MiraAPI.dll");
            SeedMod(data, "PackModA", "PackModA.dll");

            var config = new Config { DataPath = data };
            foreach (var id in new[] { "TownOfUs", "MiraAPI", "PackModA" })
                config.InstalledMods.Add(new InstalledMod { Id = id, Name = id });

            var store = new ModStore();
            store.LoadRegistryFromJson(@"{
                ""version"": ""test"",
                ""mods"": [
                    { ""id"": ""TownOfUs"", ""name"": ""TOU"", ""category"": ""Mod"", ""dependencies"": [ { ""modId"": ""MiraAPI"" } ] },
                    { ""id"": ""MiraAPI"", ""name"": ""Mira API"", ""category"": ""Dependency"" },
                    { ""id"": ""PackModA"", ""name"": ""Pack Mod A"", ""category"": ""Mod"" }
                ]
            }");
            var manager = new ModManager(config, store);

            var freshPreset = new Preset
            {
                Name = "Fresh Pack",
                ModIds = new List<string> { "TownOfUs" }
            };

            manager.SetLaunchSelection(freshPreset.ModIds, syncPlugins: false);

            Assert.Equal(new[] { "TownOfUs" }, config.SelectedMods);
            Assert.Contains("MiraAPI", manager.GetLaunchModIds());
            Assert.DoesNotContain("PackModA", manager.GetLaunchModIds());
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void FindInstalledPackMatch_ReturnsExactBuiltinMatch()
    {
        var service = new PresetService();
        var config = new Config
        {
            InstalledMods = new List<InstalledMod>
            {
                new InstalledMod { Id = "TownOfUs", Name = "TOU" },
                new InstalledMod { Id = "BetterCrewLink", Name = "BCL" }
            }
        };

        var storePath = Path.Combine(DataStore.StoreDir, "builtin-presets.json");
        string previous = null;
        if (File.Exists(storePath))
            previous = File.ReadAllText(storePath);

        try
        {
            File.WriteAllText(storePath, """
                {
                  "presets": [
                    {
                      "id": "sus-af-pack",
                      "name": "SUS AF",
                      "modIds": ["TownOfUs", "BetterCrewLink", "ChaosTokens"],
                      "installOrder": ["TownOfUs", "BetterCrewLink", "ChaosTokens"]
                    },
                    {
                      "id": "other-pack",
                      "name": "Other",
                      "modIds": ["TownOfUs", "BetterCrewLink"],
                      "installOrder": ["TownOfUs", "BetterCrewLink"]
                    }
                  ]
                }
                """);

            var match = service.FindInstalledPackMatch(config);

            Assert.NotNull(match);
            Assert.Equal("other-pack", match.Id);
        }
        finally
        {
            if (previous == null)
            {
                try { File.Delete(storePath); } catch { }
            }
            else
            {
                File.WriteAllText(storePath, previous);
            }
        }
    }

    [Fact]
    public void SelectPack_EnforcesPackLaunchSelectionOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm-select-pack-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "data");

        try
        {
            SeedMod(data, "TownOfUs", "TownOfUs.dll");
            SeedMod(data, "PackModA", "PackModA.dll");
            SeedMod(data, "ExtraMod", "ExtraMod.dll");

            var config = new Config { DataPath = data };
            config.InstalledMods.Add(new InstalledMod { Id = "TownOfUs", Name = "TOU" });
            config.InstalledMods.Add(new InstalledMod { Id = "PackModA", Name = "Pack Mod A" });
            config.InstalledMods.Add(new InstalledMod { Id = "ExtraMod", Name = "Extra" });
            config.SelectedMods.AddRange(new[] { "TownOfUs", "PackModA", "ExtraMod" });

            var presets = new PresetService();
            var store = new ModStore();
            store.LoadRegistryFromJson(Registry);
            var manager = new ModManager(config, store, presets);

            var preset = new Preset
            {
                Id = "test-pack",
                Name = "Test Pack",
                ModIds = new List<string> { "TownOfUs", "PackModA" }
            };

            manager.SelectPack(preset);

            Assert.Equal("test-pack", config.ActivePackId);
            Assert.Equal(new[] { "TownOfUs", "PackModA" }, config.SelectedMods);
            Assert.DoesNotContain("ExtraMod", config.SelectedMods);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void DeselectPack_KeepsLaunchSelection()
    {
        var config = new Config { ActivePackId = "test-pack" };
        config.SelectedMods.AddRange(new[] { "TownOfUs", "PackModA" });

        var manager = new ModManager(config, new ModStore(), new PresetService());
        manager.DeselectPack();

        Assert.Null(config.ActivePackId);
        Assert.Equal(new[] { "TownOfUs", "PackModA" }, config.SelectedMods);
    }

    [Fact]
    public async Task InstallPresetAsync_DoesNotActivatePack()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm-pack-play-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "data");

        try
        {
            SeedMod(data, "TownOfUs", "TownOfUs.dll");
            SeedMod(data, "PackModA", "PackModA.dll");

            var config = new Config { DataPath = data };
            config.InstalledMods.Add(new InstalledMod { Id = "TownOfUs", Name = "TOU" });
            config.InstalledMods.Add(new InstalledMod { Id = "PackModA", Name = "Pack Mod A" });

            var presets = new PresetService();
            var store = new ModStore();
            store.LoadRegistryFromJson(Registry);
            var manager = new ModManager(config, store, presets);

            var preset = new Preset
            {
                Id = "test-pack",
                Name = "Test Pack",
                ModIds = new List<string> { "TownOfUs", "PackModA" },
                InstallOrder = new List<string> { "TownOfUs", "PackModA" }
            };

            var result = await manager.InstallPresetAsync(preset);

            Assert.True(result.Success);
            Assert.Null(config.ActivePackId);
            Assert.Empty(config.SelectedMods);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task InstallPresetAsync_AutoSelectsSusAfOnFirstInstall()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm-susaf-first-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "data");

        try
        {
            var config = new Config { DataPath = data };
            var store = new ModStore();
            store.LoadRegistryFromJson(Registry);
            var manager = new ModManager(config, store, new PresetService());

            var preset = new Preset
            {
                Id = "sus-af-pack",
                Name = "SUS AF",
                ModIds = new List<string> { "TownOfUs" },
                InstallOrder = new List<string> { "TownOfUs" }
            };

            SeedMod(data, "TownOfUs", "TownOfUsMira.dll");
            config.InstalledMods.Add(new InstalledMod { Id = "TownOfUs", Name = "TOU" });

            var result = await manager.InstallPresetAsync(preset);

            Assert.True(result.Success);
            Assert.Equal("sus-af-pack", config.ActivePackId);
            Assert.Contains("TownOfUs", config.SelectedMods);
            Assert.Contains("selected", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task InstallPresetAsync_DoesNotAutoSelectSusAfAfterFirstTime()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm-susaf-repeat-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "data");

        try
        {
            SeedMod(data, "TownOfUs", "TownOfUsMira.dll");

            var config = new Config
            {
                DataPath = data,
                SusAfInstallPackAutoSelectDone = true
            };
            config.InstalledMods.Add(new InstalledMod { Id = "TownOfUs", Name = "TOU" });

            var store = new ModStore();
            store.LoadRegistryFromJson(Registry);
            var manager = new ModManager(config, store, new PresetService());

            var preset = new Preset
            {
                Id = "sus-af-pack",
                Name = "SUS AF",
                ModIds = new List<string> { "TownOfUs" }
            };

            var result = await manager.InstallPresetAsync(preset);

            Assert.True(result.Success);
            Assert.Null(config.ActivePackId);
            Assert.DoesNotContain("selected", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task SelectPresetAsync_ActivatesPackAndSelection()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm-select-pack-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "data");

        try
        {
            SeedMod(data, "TownOfUs", "TownOfUs.dll");
            SeedMod(data, "PackModA", "PackModA.dll");

            var config = new Config { DataPath = data };
            config.InstalledMods.Add(new InstalledMod { Id = "TownOfUs", Name = "TOU" });
            config.InstalledMods.Add(new InstalledMod { Id = "PackModA", Name = "Pack Mod A" });

            var presets = new PresetService();
            var store = new ModStore();
            store.LoadRegistryFromJson(Registry);
            var manager = new ModManager(config, store, presets);

            var preset = new Preset
            {
                Id = "test-pack",
                Name = "Test Pack",
                ModIds = new List<string> { "TownOfUs", "PackModA" },
                InstallOrder = new List<string> { "TownOfUs", "PackModA" }
            };

            var result = await manager.SelectPresetAsync(preset);

            Assert.True(result.Success);
            Assert.Equal("test-pack", config.ActivePackId);
            Assert.Equal(new[] { "TownOfUs", "PackModA" }, config.SelectedMods);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void HasLaunchableFiles_DetectsUtilityExecutable()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm-util-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "data");

        try
        {
            var modDir = Path.Combine(data, "Mods", "BetterCrewLink");
            Directory.CreateDirectory(modDir);
            File.WriteAllText(Path.Combine(modDir, "Better-CrewLink.exe"), "stub");

            var config = new Config { DataPath = data };
            var store = new ModStore();
            store.LoadRegistryFromJson(@"{
                ""version"": ""test"",
                ""mods"": [
                    {
                        ""id"": ""BetterCrewLink"",
                        ""name"": ""Better CrewLink"",
                        ""category"": ""Utility"",
                        ""executableName"": ""Better-CrewLink.exe""
                    }
                ]
            }");
            var manager = new ModManager(config, store);

            Assert.True(manager.HasLaunchableFiles("BetterCrewLink"));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void GetAllPresets_PinnedPresetSortsFirst()
    {
        var storePath = Path.Combine(DataStore.StoreDir, "builtin-presets.json");
        string previous = null;
        if (File.Exists(storePath))
            previous = File.ReadAllText(storePath);

        try
        {
            File.WriteAllText(storePath, """
                {
                  "presets": [
                    {
                      "id": "vanilla-plus",
                      "name": "Vanilla+",
                      "builtin": true,
                      "modIds": ["AUnlocker"]
                    },
                    {
                      "id": "sus-af-pack",
                      "name": "SUS AF",
                      "builtin": true,
                      "pinned": true,
                      "modIds": ["TownOfUs"]
                    }
                  ]
                }
                """);

            var service = new PresetService();
            var config = new Config
            {
                UserPresets = new List<Preset>
                {
                    new Preset { Id = "aaa", Name = "AAA User", ModIds = new List<string>() },
                    new Preset { Id = "zzz", Name = "ZZZ User", ModIds = new List<string>() }
                }
            };

            var ordered = service.GetAllPresets(config);
            Assert.NotEmpty(ordered);
            Assert.True(ordered[0].Pinned, "SUS AF should stay pinned at the top.");
            Assert.Equal("sus-af-pack", ordered[0].Id);
        }
        finally
        {
            if (previous == null)
            {
                try { File.Delete(storePath); } catch { }
            }
            else
            {
                File.WriteAllText(storePath, previous);
            }
        }
    }

    [Fact]
    public void LoadBuiltinPresets_ContainsTohePack()
    {
        var storePath = Path.Combine(DataStore.StoreDir, "builtin-presets.json");
        string previous = null;
        if (File.Exists(storePath))
            previous = File.ReadAllText(storePath);

        try
        {
            File.WriteAllText(storePath, """
                {
                  "presets": [
                    {
                      "id": "tohe-pack",
                      "name": "TOHE",
                      "builtin": true,
                      "modIds": ["BetterCrewLink", "TOHE", "AUnlocker", "VanillaEnhancements"],
                      "installOrder": ["TOHE", "AUnlocker", "VanillaEnhancements", "BetterCrewLink"]
                    }
                  ]
                }
                """);

            var service = new PresetService();
            var tohe = service.LoadBuiltinPresets().FirstOrDefault(p =>
                string.Equals(p.Id, "tohe-pack", StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(tohe);
            Assert.Equal(new[] { "BetterCrewLink", "TOHE", "AUnlocker", "VanillaEnhancements" }, tohe.ModIds);
            Assert.Equal(new[] { "TOHE", "AUnlocker", "VanillaEnhancements", "BetterCrewLink" }, tohe.GetOrderedModIds());
        }
        finally
        {
            if (previous == null)
            {
                try { File.Delete(storePath); } catch { }
            }
            else
            {
                File.WriteAllText(storePath, previous);
            }
        }
    }

    [Fact]
    public void IsModReady_RequiresFilesOnDisk()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm-ready-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "data");

        try
        {
            var config = new Config { DataPath = data };
            config.InstalledMods.Add(new InstalledMod { Id = "GhostMod", Name = "Ghost" });
            config.InstalledMods.Add(new InstalledMod { Id = "RealMod", Name = "Real" });
            SeedMod(data, "RealMod", "RealMod.dll");

            var manager = new ModManager(config, new ModStore());

            Assert.False(manager.IsModReady("GhostMod"));
            Assert.True(manager.IsModReady("RealMod"));
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
