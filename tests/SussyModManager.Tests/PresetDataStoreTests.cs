using System.Linq;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Models;
using Xunit;

namespace SussyModManager.Tests;

public class PresetDataStoreTests
{
    [Fact]
    public void MergeBuiltinPresetsJson_OverridesStaleRemoteDisplayName()
    {
        const string bundled = """
            {
              "presets": [
                {
                  "id": "sus-af-pack",
                  "name": "SUS AF",
                  "description": "Shipped description.",
                  "pinned": true,
                  "modIds": ["TownOfUs"]
                }
              ]
            }
            """;

        const string remote = """
            {
              "presets": [
                {
                  "id": "sus-af-pack",
                  "name": "SUS AF PACK",
                  "description": "Old remote description.",
                  "modIds": ["TownOfUs", "BetterCrewLink"]
                }
              ]
            }
            """;

        var merged = DataStore.MergeBuiltinPresetsJson(bundled, remote);
        var file = Json.Deserialize<PresetFile>(merged);
        var susAf = file.presets.Single(p => p.Id == "sus-af-pack");

        Assert.Equal("SUS AF", susAf.Name);
        Assert.Equal("Shipped description.", susAf.Description);
        Assert.True(susAf.Pinned);
        Assert.Contains("BetterCrewLink", susAf.ModIds);
    }

    [Fact]
    public void MergeBuiltinPresetLists_KeepsGitHubModListOverBundled()
    {
        var cached = new List<Preset>
        {
            new Preset
            {
                Id = "sus-af-pack",
                Name = "SUS AF PACK",
                ModIds = new List<string> { "TownOfUs", "AleLuduMod", "AUnlocker", "VanillaEnhancements", "BetterCrewLink", "DraftModeTOUM", "TownOfUsMiraRolesExtension" },
                InstallOrder = new List<string> { "TownOfUs", "AleLuduMod", "AUnlocker", "VanillaEnhancements", "BetterCrewLink", "DraftModeTOUM", "TownOfUsMiraRolesExtension" }
            }
        };

        var bundled = new List<Preset>
        {
            new Preset
            {
                Id = "sus-af-pack",
                Name = "SUS AF",
                Description = "Shipped",
                Pinned = true,
                ModIds = new List<string>
                {
                    "TownOfUs", "TownOfUsMiraRolesExtension", "DraftModeTOUM", "AleLuduMod",
                    "ChaosTokens", "TownOfUsMiraDivaniModsAddOn", "GameTweaks",
                    "AUnlocker", "VanillaEnhancements", "BetterCrewLink"
                }
            }
        };

        var merged = DataStore.MergeBuiltinPresetLists(cached, bundled);
        var susAf = merged.Single(p => p.Id == "sus-af-pack");

        Assert.Equal("SUS AF", susAf.Name);
        Assert.Equal(7, susAf.ModIds.Count);
        Assert.Contains("DraftModeTOUM", susAf.ModIds);
        Assert.DoesNotContain("ChaosTokens", susAf.ModIds);
        Assert.Equal(7, susAf.GetOrderedModIds().Count);
    }

    [Fact]
    public void MergeBuiltinPresetLists_KeepsGitHubModListWhenBundledIsShorter()
    {
        var cached = new List<Preset>
        {
            new Preset
            {
                Id = "sus-af-pack",
                ModIds = new List<string>
                {
                    "TownOfUs", "TownOfUsMiraRolesExtension", "DraftModeTOUM", "AleLuduMod",
                    "ChaosTokens", "TownOfUsMiraDivaniModsAddOn", "GameTweaks",
                    "AUnlocker", "VanillaEnhancements", "BetterCrewLink"
                }
            }
        };

        var bundled = new List<Preset>
        {
            new Preset
            {
                Id = "sus-af-pack",
                Name = "SUS AF",
                ModIds = new List<string> { "TownOfUs", "AleLuduMod", "BetterCrewLink" }
            }
        };

        var merged = DataStore.MergeBuiltinPresetLists(cached, bundled);
        var susAf = merged.Single(p => p.Id == "sus-af-pack");

        Assert.Equal(10, susAf.ModIds.Count);
        Assert.Contains("GameTweaks", susAf.ModIds);
    }

    [Fact]
    public void SyncInstallOrderWithModIds_AppendsMissingMods()
    {
        var preset = new Preset
        {
            Id = "sus-af-pack",
            ModIds = new List<string> { "TownOfUs", "ChaosTokens", "GameTweaks" },
            InstallOrder = new List<string> { "TownOfUs" }
        };

        DataStore.SyncInstallOrderWithModIds(preset);

        Assert.Equal(new[] { "TownOfUs", "ChaosTokens", "GameTweaks" }, preset.GetOrderedModIds());
    }

    [Fact]
    public void MergeBuiltinPresetLists_UsesBundledWhenNoCachedEntry()
    {
        var bundled = new List<Preset>
        {
            new Preset
            {
                Id = "sus-af-pack",
                Name = "SUS AF",
                ModIds = new List<string> { "TownOfUs", "BetterCrewLink" }
            }
        };

        var merged = DataStore.MergeBuiltinPresetLists(null, bundled);
        var susAf = merged.Single(p => p.Id == "sus-af-pack");

        Assert.Equal(2, susAf.ModIds.Count);
        Assert.Equal("SUS AF", susAf.Name);
    }
}
