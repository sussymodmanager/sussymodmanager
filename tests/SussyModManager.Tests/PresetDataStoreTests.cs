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
}
