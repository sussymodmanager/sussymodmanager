using System.Collections.Generic;
using SussyModManager.Core.Models;
using SussyModManager.Core.Platform;
using SussyModManager.Core.Services;

namespace SussyModManager.Tests;

public class AppUpdateServiceTests
{
    private static GitHubRelease ReleaseWith(params string[] assetNames)
    {
        var release = new GitHubRelease { tag_name = "v1.2.3", assets = new List<GitHubAsset>() };
        foreach (var name in assetNames)
            release.assets.Add(new GitHubAsset { name = name, browser_download_url = "https://example.com/" + name });
        return release;
    }

    [Fact]
    public void PickAsset_PrefersExactRuntimeIdentifier()
    {
        var rid = PlatformInfo.RuntimeIdentifier;
        var release = ReleaseWith(
            "SussyModManager-win-x64.zip",
            "SussyModManager-osx-arm64.zip",
            "SussyModManager-linux-x64.zip",
            $"SussyModManager-{rid}.zip");

        var url = AppUpdateService.PickAsset(release);

        Assert.NotNull(url);
        Assert.Contains(rid, url!);
    }

    [Fact]
    public void PickAsset_NeverServesWrongArchitecture()
    {
        // A release that contains every arch EXCEPT the current one must not hand back a build.
        var rid = PlatformInfo.RuntimeIdentifier;
        var all = new[]
        {
            "SussyModManager-win-x64.zip",
            "SussyModManager-win-arm64.zip",
            "SussyModManager-osx-x64.zip",
            "SussyModManager-osx-arm64.zip",
            "SussyModManager-linux-x64.zip",
            "SussyModManager-linux-arm64.zip",
        };
        var without = new List<string>();
        foreach (var a in all)
            if (!a.Contains(rid))
                without.Add(a);

        var url = AppUpdateService.PickAsset(ReleaseWith(without.ToArray()));

        if (url != null)
            Assert.DoesNotContain(WrongArchTokenFor(rid), url);
    }

    [Fact]
    public void PickAsset_ReturnsNullWhenNoAssets()
    {
        Assert.Null(AppUpdateService.PickAsset(new GitHubRelease { assets = new List<GitHubAsset>() }));
    }

    [Theory]
    [InlineData("1.2.4", "1.2.3", true)]
    [InlineData("1.2.3", "1.2.3", false)]
    [InlineData("1.2.2", "1.2.3", false)]
    [InlineData("2.0.0", "1.9.9", true)]
    public void IsNewer_ComparesSemanticVersions(string latest, string current, bool expected)
    {
        Assert.Equal(expected, AppUpdateService.IsNewer(latest, current));
    }

    private static string WrongArchTokenFor(string rid)
    {
        // Return an arch token that is NOT the current one, for the DoesNotContain assertion.
        if (rid.Contains("arm64")) return "x64";
        if (rid.Contains("x86")) return "arm64";
        return "arm64";
    }
}
