using SussyModManager.Core.Platform;

namespace SussyModManager.Tests;

public class PlatformInfoTests
{
    [Fact]
    public void RuntimeIdentifier_HasOsDashArchShape()
    {
        var rid = PlatformInfo.RuntimeIdentifier;

        Assert.False(string.IsNullOrWhiteSpace(rid));
        var parts = rid.Split('-');
        Assert.Equal(2, parts.Length);
        Assert.Contains(parts[0], new[] { "win", "osx", "linux" });
        Assert.Contains(parts[1], new[] { "x64", "x86", "arm64" });
    }

    [Fact]
    public void DataRoot_IsRootedAndNamed()
    {
        var root = PlatformInfo.DataRoot;
        Assert.True(System.IO.Path.IsPathRooted(root));
        Assert.EndsWith(PlatformInfo.AppName, root);
    }
}
