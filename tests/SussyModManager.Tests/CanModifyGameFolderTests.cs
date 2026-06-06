using SussyModManager.Core.Services;

namespace SussyModManager.Tests;

public class CanModifyGameFolderTests
{
    [Fact]
    public void CanModifyGameFolder_WritableInstall_ReturnsTrue()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smm-write-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Among Us.exe"), "");

        try
        {
            Assert.True(AmongUsLocator.CanModifyGameFolder(dir));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void CanModifyGameFolder_InvalidPath_ReturnsFalse()
    {
        Assert.False(AmongUsLocator.CanModifyGameFolder(null));
        Assert.False(AmongUsLocator.CanModifyGameFolder(""));
        Assert.False(AmongUsLocator.CanModifyGameFolder(Path.Combine(Path.GetTempPath(), "nope-" + Guid.NewGuid().ToString("N"))));
    }
}
