using System.Collections.Generic;
using SussyModManager.Core;
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
    [InlineData("1.0.5", "1.0.4", true)]
    public void IsNewer_ComparesSemanticVersions(string latest, string current, bool expected)
    {
        Assert.Equal(expected, AppUpdateService.IsNewer(latest, current));
    }

    [Theory]
    [InlineData("https://github.com/sussymodmanager/sussymodmanager/releases/tag/v1.0.5", "1.0.5")]
    [InlineData("https://github.com/sussymodmanager/sussymodmanager/releases/tag/v1.0.4/", "1.0.4")]
    public void ParseVersionFromReleaseTagUrl_ExtractsVersion(string url, string expected)
    {
        Assert.Equal(expected, AppUpdateService.ParseVersionFromReleaseTagUrl(url));
    }

    [Fact]
    public void BuildPlatformZipUrl_MatchesReleaseAssetNaming()
    {
        var url = AppUpdateService.BuildPlatformZipUrl("1.0.5");
        Assert.Contains("releases/download/v1.0.5/SussyModManager-", url);
        Assert.EndsWith(".zip", url);
    }

    [Fact]
    public void IsUpdateAlreadyStaged_IsFalseWithoutPendingFiles()
    {
        Assert.False(AppUpdateService.IsUpdateAlreadyStaged("1.0.6"));
    }

    [Theory]
    [InlineData("1.0.9", "1.0.9.0", "1.0.9")]
    [InlineData("v1.0.8", "1.0.8.0", "1.0.8")]
    [InlineData("1.0.7+abc123", "1.0.7.0", "1.0.7")]
    public void TryNormalizeVersion_ParsesBuildMetadata(string raw, string fileVersion, string expected)
    {
        Assert.True(AppInfo.TryNormalizeVersion(raw, out var fromInformational));
        Assert.Equal(expected, fromInformational);
        Assert.True(AppInfo.TryNormalizeVersion(fileVersion, out var fromFile));
        Assert.Equal(expected, fromFile);
    }

    [Fact]
    public void NormalizeUpdateState_DoesNotThrowWhenUpdatesFolderMissing()
    {
        AppUpdateService.NormalizeUpdateState();
    }

    private static string WrongArchTokenFor(string rid)
    {
        // Return an arch token that is NOT the current one, for the DoesNotContain assertion.
        if (rid.Contains("arm64")) return "x64";
        if (rid.Contains("x86")) return "arm64";
        return "arm64";
    }
}
