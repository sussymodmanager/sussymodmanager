using System.IO;
using SussyModManager.Core.Helpers;

namespace SussyModManager.Tests;

public class AmongUsProcessGuardTests
{
    [Theory]
    [InlineData("Among Us", true)]
    [InlineData("among us", true)]
    [InlineData("Among Us.x86_64", true)]
    [InlineData("AmongUs", false)]
    [InlineData("notepad", false)]
    [InlineData("", false)]
    public void IsAmongUsProcessName_MatchesExpectedProcesses(string processName, bool expected)
    {
        Assert.Equal(expected, AmongUsProcessGuard.IsAmongUsProcessName(processName));
    }

    [Fact]
    public void PathUnderAmongUsInstall_MatchesExeAndNestedPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm_guard_paths_" + Guid.NewGuid().ToString("N"));
        var game = Path.Combine(root, "Among Us");
        var sibling = Path.Combine(root, "Among Us 2");
        var other = Path.Combine(root, "Other");
        Directory.CreateDirectory(Path.Combine(game, "BepInEx", "plugins"));
        Directory.CreateDirectory(sibling);
        Directory.CreateDirectory(other);

        var exe = Path.Combine(game, "Among Us.exe");
        var plugin = Path.Combine(game, "BepInEx", "plugins", "Reactor.dll");
        var siblingExe = Path.Combine(sibling, "Among Us.exe");
        var otherExe = Path.Combine(other, "Among Us.exe");
        File.WriteAllText(exe, "");
        File.WriteAllText(plugin, "");
        File.WriteAllText(siblingExe, "");
        File.WriteAllText(otherExe, "");

        try
        {
            Assert.True(AmongUsProcessGuard.PathUnderAmongUsInstall(exe, game));
            Assert.True(AmongUsProcessGuard.PathUnderAmongUsInstall(plugin, game));
            Assert.False(AmongUsProcessGuard.PathUnderAmongUsInstall(siblingExe, game));
            Assert.False(AmongUsProcessGuard.PathUnderAmongUsInstall(otherExe, game));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void PathUnderAmongUsInstall_NormalizesTrailingSeparators()
    {
        var game = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "smm_guard_" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(game);
        try
        {
            var nested = Path.Combine(game, "BepInEx", "plugins");
            Directory.CreateDirectory(nested);

            Assert.True(AmongUsProcessGuard.PathUnderAmongUsInstall(nested, game + Path.DirectorySeparatorChar));
            Assert.True(AmongUsProcessGuard.PathUnderAmongUsInstall(game, game));
        }
        finally
        {
            try { Directory.Delete(game, true); } catch { }
        }
    }

    [Fact]
    public void FormatGameRunningMessage_MentionsCloseAndTray()
    {
        var message = AmongUsProcessGuard.FormatGameRunningMessage();
        Assert.Contains("Among Us", message);
        Assert.Contains("taskbar", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tray", message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("The process cannot access the file because it is being used by another process.", true)]
    [InlineData("Access to the path 'Reactor.dll' is denied.", true)]
    [InlineData("Access denied", true)]
    [InlineData("Directory not found", false)]
    public void IsFileLockError_DetectsLockMessages(string message, bool expected)
    {
        var ex = new IOException(message);
        Assert.Equal(expected, AmongUsProcessGuard.IsFileLockError(ex));
        Assert.Equal(expected, AmongUsProcessGuard.LooksLikeFileLockFailure($"MiraAPI: Failed to copy MiraAPI.dll: {message}"));
    }

    [Fact]
    public async Task WaitForAmongUsToExit_ReturnsImmediatelyWhenNotRunning()
    {
        var game = Path.Combine(Path.GetTempPath(), "smm_guard_wait_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(game);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await AmongUsProcessGuard.WaitForAmongUsToExit(game, TimeSpan.FromSeconds(1), cts.Token);
        }
        finally
        {
            try { Directory.Delete(game, true); } catch { }
        }
    }
}
