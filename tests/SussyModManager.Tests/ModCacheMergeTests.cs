using SussyModManager.Core.Helpers;

namespace SussyModManager.Tests;

public class ModCacheMergeTests
{
    [Fact]
    public void MergeModCacheJson_PrefersNewerReleaseTag()
    {
        var bundled = """
            {
              "version": "1.0",
              "mods": {
                "TownOfUs": {
                  "cachedReleaseData": "{\"tag_name\":\"1.4.0\",\"assets\":[]}"
                }
              }
            }
            """;

        var store = """
            {
              "version": "1.0",
              "mods": {
                "TownOfUs": {
                  "cachedReleaseData": "{\"tag_name\":\"1.6.2\",\"assets\":[]}"
                }
              }
            }
            """;

        var merged = DataStore.MergeModCacheJson(bundled, store);
        Assert.Contains("1.6.2", merged);
        Assert.DoesNotContain("1.4.0", merged);
    }

    [Fact]
    public void MergeModCacheJson_BundledNewerWinsOverStaleStore()
    {
        var bundled = """
            {
              "version": "1.0",
              "mods": {
                "MiraAPI": {
                  "cachedReleaseData": "{\"tag_name\":\"0.4.0\",\"assets\":[]}"
                }
              }
            }
            """;

        var store = """
            {
              "version": "1.0",
              "mods": {
                "MiraAPI": {
                  "cachedReleaseData": "{\"tag_name\":\"0.3.3\",\"assets\":[]}"
                }
              }
            }
            """;

        var merged = DataStore.MergeModCacheJson(bundled, store);
        Assert.Contains("0.4.0", merged);
        Assert.DoesNotContain("0.3.3", merged);
    }

    [Fact]
    public void PreferNewerModCacheEntry_UsesReleaseTagComparison()
    {
        var older = new SussyModManager.Core.Models.ModCacheEntry
        {
            cachedReleaseData = "{\"tag_name\":\"v1.2.0\"}"
        };
        var newer = new SussyModManager.Core.Models.ModCacheEntry
        {
            cachedReleaseData = "{\"tag_name\":\"v1.3.0\"}"
        };

        var winner = DataStore.PreferNewerModCacheEntry(older, newer);
        Assert.Equal("v1.3.0", System.Text.Json.JsonDocument.Parse(winner.cachedReleaseData).RootElement.GetProperty("tag_name").GetString());
    }
}
