using System.Collections.Generic;
using System.Linq;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Models;
using SussyModManager.Core.Services;

namespace SussyModManager.Tests;

public class GitHubReleaseAssetGuesserTests
{
    private static ModRegistryEntry Entry(string owner, string repo) => new()
    {
        githubOwner = owner,
        githubRepo = repo
    };

    [Theory]
    [InlineData("v1.6.2", "1.6.2")]
    [InlineData("1.6.2", "1.6.2")]
    [InlineData("V1.2.9Hot3", "1.2.9Hot3")]
    public void NormalizeReleasePathTag_StripsLeadingV(string tag, string expected)
    {
        Assert.Equal(expected, GitHubReleaseAssetGuesser.NormalizeReleasePathTag(tag));
    }

    [Theory]
    [InlineData("1.6.2", "v1.6.2")]
    [InlineData("v1.6.2", "v1.6.2")]
    public void NormalizeReleaseFileTag_AddsLeadingV(string tag, string expected)
    {
        Assert.Equal(expected, GitHubReleaseAssetGuesser.NormalizeReleaseFileTag(tag));
    }

    [Fact]
    public void BuildCandidateUrls_TouMira_IncludesSteamEpicAndDll()
    {
        var entry = Entry("AU-Avengers", "TOU-Mira");
        entry.assetFilters = new AssetFilters
        {
            steam = new AssetFilter { patterns = new List<string> { "steam-itch" } },
            epic = new AssetFilter { patterns = new List<string> { "epic-msstore" } },
            dll = new AssetFilter { patterns = new List<string> { "TownOfUsMira.dll" }, exactMatch = true }
        };

        var urls = GitHubReleaseAssetGuesser.BuildCandidateUrls(entry, "1.6.2").ToList();

        Assert.Contains(urls, u => u.DownloadUrl ==
            "https://github.com/AU-Avengers/TOU-Mira/releases/download/1.6.2/TouMira-v1.6.2-x86-steam-itch.zip");
        Assert.Contains(urls, u => u.Name == "TownOfUsMira.dll");
    }

    [Fact]
    public void BuildCandidateUrls_AllTheRoles_UsesChannelZipNames()
    {
        var entry = Entry("Zeo666", "AllTheRoles");
        entry.assetFilters = new AssetFilters
        {
            steam = new AssetFilter { patterns = new List<string> { "x86-steam-itch", "steam-itch" } },
            epic = new AssetFilter { patterns = new List<string> { "x64-epic-msstore", "epic-msstore" } }
        };

        var urls = GitHubReleaseAssetGuesser.BuildCandidateUrls(entry, "0.14.0").ToList();

        Assert.Contains(urls, u => u.Name == "AllTheRoles-0.14.0-x86-steam-itch.zip");
        Assert.Contains(urls, u => u.Name == "AllTheRoles-0.14.0-x64-epic-msstore.zip");
    }

    [Fact]
    public void BuildCandidateUrls_LaunchpadReloaded_InfersRepoDll()
    {
        var entry = Entry("All-Of-Us-Mods", "LaunchpadReloaded");
        entry.assetFilters = new AssetFilters
        {
            dll = new AssetFilter { patterns = new List<string> { ".dll" } }
        };

        var urls = GitHubReleaseAssetGuesser.BuildCandidateUrls(entry, "0.3.8").ToList();

        Assert.Contains(urls, u => u.Name == "LaunchpadReloaded.dll");
        Assert.Contains(urls, u => u.DownloadUrl.Contains("/releases/latest/download/LaunchpadReloaded.dll"));
    }

    [Fact]
    public void BuildCandidateUrls_EndlessHostRoles_UsesEhrNaming()
    {
        var entry = Entry("Gurge44", "EndlessHostRoles");
        var urls = GitHubReleaseAssetGuesser.BuildCandidateUrls(entry, "v7.5.1").ToList();

        Assert.Contains(urls, u => u.Name == "EHR.v7.5.1_Steam.zip");
        Assert.Contains(urls, u => u.Name == "EHR.v7.5.1_Epic-Games_Microsoft-Store.zip");
    }

    [Fact]
    public void DirectDownloadResolver_InfersRepoDllWhenPatternIsGeneric()
    {
        var entry = new ModRegistryEntry
        {
            githubOwner = "All-Of-Us-Mods",
            githubRepo = "LaunchpadReloaded",
            assetFilters = new AssetFilters
            {
                dll = new AssetFilter { patterns = new List<string> { ".dll" } }
            }
        };

        var version = DirectDownloadResolver.TryResolve(entry);

        Assert.NotNull(version);
        Assert.Equal(
            "https://github.com/All-Of-Us-Mods/LaunchpadReloaded/releases/latest/download/LaunchpadReloaded.dll",
            version!.DownloadUrl);
    }
}
