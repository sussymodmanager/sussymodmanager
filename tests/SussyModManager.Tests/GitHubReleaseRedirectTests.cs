using System.Linq;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Services;

namespace SussyModManager.Tests;

public class GitHubReleaseRedirectTests
{
    [Theory]
    [InlineData("https://github.com/AU-Avengers/TOU-Mira/releases/tag/1.6.2", "1.6.2")]
    [InlineData("https://github.com/AU-Avengers/TOU-Mira/releases/tag/v1.6.2", "v1.6.2")]
    [InlineData("https://github.com/AU-Avengers/TOU-Mira/releases/tag/v1.4.0?foo=bar", "v1.4.0")]
    public void ParseTagFromReleaseUrl_ExtractsTag(string url, string expected)
    {
        Assert.Equal(expected, GitHubReleaseRedirect.ParseTagFromReleaseUrl(url));
    }

    [Fact]
    public void GuessDirectAssetUrls_TouMira_UsesPathAndFileTagFormats()
    {
        var entry = new SussyModManager.Core.Models.ModRegistryEntry
        {
            githubOwner = "AU-Avengers",
            githubRepo = "TOU-Mira"
        };

        var urls = GitHubReleaseRedirect.GuessDirectAssetUrls(entry, "1.6.2").ToList();

        Assert.Contains(urls, u => u.DownloadUrl ==
            "https://github.com/AU-Avengers/TOU-Mira/releases/download/1.6.2/TouMira-v1.6.2-x86-steam-itch.zip");
        Assert.Contains(urls, u => u.Name == "TouMira-v1.6.2-x86-steam-itch.zip");
    }

    [Theory]
    [InlineData("1.6.2", "v1.4.0", true)]
    [InlineData("1.6.2", "v1.6.2", false)]
    public void IsNewer_HandlesMixedTagPrefixes(string latest, string current, bool expected)
    {
        Assert.Equal(expected, AppUpdateService.IsNewer(latest, current));
    }

    [Theory]
    [InlineData("v1.6.2", "v1.4.0", true)]
    [InlineData("v1.4.0", "v1.6.2", false)]
    [InlineData("v1.6.2", "v1.6.2", false)]
    public void IsNewer_ComparesModReleaseTags(string latest, string current, bool expected)
    {
        Assert.Equal(expected, AppUpdateService.IsNewer(latest, current));
    }

    [Fact]
    public void GetLastLogPluginLoadFailures_ParsesFailedPlugins()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smm-log-" + Guid.NewGuid().ToString("N"));
        var logDir = Path.Combine(dir, "BepInEx");
        Directory.CreateDirectory(logDir);
        File.WriteAllText(Path.Combine(logDir, "LogOutput.log"),
            """
            [Info   :   BepInEx] Loading [Town of Us Mira 1.4.0]
            [Error  :   BepInEx] Error loading [Tou Mira Roles Extension 1.1.9]: boom
            [Error  :   BepInEx] Error loading [DraftModeTOUM 1.1.0]: boom
            """);

        try
        {
            var failures = BepInExInteropDiagnostics.GetLastLogPluginLoadFailures(dir);
            Assert.Equal(2, failures.Count);
            Assert.Contains(failures, f => f.StartsWith("Tou Mira Roles Extension", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(failures, f => f.StartsWith("DraftModeTOUM", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
