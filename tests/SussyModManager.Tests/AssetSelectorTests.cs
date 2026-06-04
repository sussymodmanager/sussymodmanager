using System.Collections.Generic;
using SussyModManager.Core.Models;
using SussyModManager.Core.Services;

namespace SussyModManager.Tests;

public class AssetSelectorTests
{
    private static List<GitHubAsset> Assets(params string[] names)
    {
        var list = new List<GitHubAsset>();
        foreach (var n in names)
            list.Add(new GitHubAsset { name = n, browser_download_url = "https://x/" + n });
        return list;
    }

    [Fact]
    public void FindAssetByFilter_MatchesPatternAndRespectsExclude()
    {
        var assets = Assets("Mod-Steam.zip", "Mod-Epic.zip", "Mod-Steam-debug.zip");
        var filter = new AssetFilter
        {
            patterns = new List<string> { "steam" },
            exclude = new List<string> { "debug" }
        };

        var match = AssetSelector.FindAssetByFilter(assets, filter);

        Assert.NotNull(match);
        Assert.Equal("Mod-Steam.zip", match!.name);
    }

    [Fact]
    public void FindDependencyDll_PrefersExactNameThenAnyDll()
    {
        var assets = Assets("Reactor.dll", "Other.dll");
        Assert.Equal("Reactor.dll", AssetSelector.FindDependencyDll(assets, "Reactor.dll")!.name);

        var onlyOther = Assets("Something.dll");
        Assert.Equal("Something.dll", AssetSelector.FindDependencyDll(onlyOther, "Missing.dll")!.name);

        Assert.Null(AssetSelector.FindDependencyDll(Assets("readme.txt"), "x.dll"));
    }
}
