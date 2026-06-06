using SussyModManager.Core.Services;

namespace SussyModManager.Tests;

public class GameDetectionPriorityTests
{
    [Fact]
    public void PickFirstValid_PrefersEarlierSteamCandidate()
    {
        var steam = CreateTempInstall(AmongUsLocator.SteamChannel);
        var epic = CreateTempInstall(AmongUsLocator.EpicChannel);

        try
        {
            var picked = AmongUsLocator.PickFirstValid(new[]
            {
                new GameLocation(steam, AmongUsLocator.SteamChannel),
                new GameLocation(epic, AmongUsLocator.EpicChannel)
            });

            Assert.NotNull(picked);
            Assert.Equal(steam, picked.Path);
            Assert.Equal(AmongUsLocator.SteamChannel, picked.Channel);
        }
        finally
        {
            Cleanup(steam);
            Cleanup(epic);
        }
    }

    [Fact]
    public void PickFirstValid_SkipsInvalidPaths()
    {
        var epic = CreateTempInstall(AmongUsLocator.EpicChannel);

        try
        {
            var picked = AmongUsLocator.PickFirstValid(new[]
            {
                new GameLocation(Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N")), AmongUsLocator.SteamChannel),
                new GameLocation(epic, AmongUsLocator.EpicChannel)
            });

            Assert.NotNull(picked);
            Assert.Equal(epic, picked.Path);
        }
        finally
        {
            Cleanup(epic);
        }
    }

    [Fact]
    public void BuildCandidateLocations_OrdersSteamBeforeEpic()
    {
        var candidates = AmongUsLocator.BuildCandidateLocations(includeHeavyProbes: false).Take(10).ToList();

        // Linux/macOS CI has no Steam installs or MS Store probes — only assert ordering when present.
        if (candidates.Count == 0)
            return;

        Assert.Equal(AmongUsLocator.SteamChannel, candidates[0].Channel);

        if (OperatingSystem.IsWindows())
        {
            var epicIndex = candidates.FindIndex(c =>
                string.Equals(c.Channel, AmongUsLocator.EpicChannel, StringComparison.Ordinal));
            if (epicIndex >= 0)
                Assert.True(epicIndex > 0, "Epic/MS Store candidates should follow Steam paths.");
        }
        else
        {
            Assert.All(candidates, c => Assert.Equal(AmongUsLocator.SteamChannel, c.Channel));
        }
    }

    private static string CreateTempInstall(string channel)
    {
        var dir = Path.Combine(Path.GetTempPath(), "smm-detect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Among Us.exe"), channel);
        return dir;
    }

    private static void Cleanup(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
