using System.Net.Http;
using SussyModManager.Core.Helpers;
using Xunit;

namespace SussyModManager.Tests;

public class VanillaEnhancementsDllPatcherTests
{
    [Fact]
    public async Task TryPatch_FixesRateLimitCrashArraySize()
    {
        var source = await EnsureUnpatchedDllAsync();
        var copy = Path.Combine(Path.GetTempPath(), "ve-patch-test-" + Guid.NewGuid().ToString("N") + ".dll");
        File.Copy(source, copy, true);
        try
        {
            Assert.True(VanillaEnhancementsDllPatcher.NeedsPatch(copy));
            Assert.True(VanillaEnhancementsDllPatcher.TryPatch(copy, out var patchError), patchError);
            Assert.False(VanillaEnhancementsDllPatcher.NeedsPatch(copy));
            Assert.True(VanillaEnhancementsDllPatcher.TryPatch(copy, out var secondError), secondError);
        }
        finally
        {
            try { File.Delete(copy); } catch { }
        }
    }

    [Fact]
    public void LaunchSetIncludesVanillaEnhancements_DetectsPackMember()
    {
        Assert.True(VanillaEnhancementsLaunchGuard.LaunchSetIncludesVanillaEnhancements(
            new[] { "TownOfUs", "VanillaEnhancements" }));
        Assert.False(VanillaEnhancementsLaunchGuard.LaunchSetIncludesVanillaEnhancements(
            new[] { "TownOfUs", "AUnlocker" }));
    }

    private static async Task<string> EnsureUnpatchedDllAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), "VanillaEnhancements-unpatched-test.dll");
        if (File.Exists(path) && VanillaEnhancementsDllPatcher.NeedsPatch(path))
            return path;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SussyModManager.Tests");
        var bytes = await client.GetByteArrayAsync(
            "https://github.com/xChipseq/VanillaEnhancements/releases/latest/download/VanillaEnhancements.dll");
        await File.WriteAllBytesAsync(path, bytes);
        Assert.True(VanillaEnhancementsDllPatcher.NeedsPatch(path));
        return path;
    }
}
