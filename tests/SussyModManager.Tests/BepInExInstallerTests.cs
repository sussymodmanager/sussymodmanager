using SussyModManager.Core.Services;

namespace SussyModManager.Tests;

public class BepInExInstallerTests
{
    [Fact]
    public void GetReadinessIssue_MissingCore_ReturnsMessage()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smm-bep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Among Us.exe"), "");
        File.WriteAllText(Path.Combine(dir, "winhttp.dll"), "");

        try
        {
            var issue = BepInExInstaller.GetReadinessIssue(dir, GameChannels.Steam);
            Assert.NotNull(issue);
            Assert.Contains("core", issue, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void ResolveTarget_EpicChannelWithoutExe_FallsBackToChannel()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smm-bep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Equal(BepInExTarget.WindowsX64,
                BepInExInstaller.ResolveTarget(dir, GameChannels.EpicMsStore));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
