using SussyModManager.Core.Services;

namespace SussyModManager.Tests;

public class LaunchServiceTests
{
    [Theory]
    [InlineData(@"C:\Steam\steamapps\common\Among Us", true)]
    [InlineData(@"C:\XboxGames\Among Us\Content", false)]
    [InlineData(@"C:\Program Files\Epic Games\AmongUs", false)]
    public void ShouldUseSteamHandoff_OnlyForSteamPaths(string path, bool expected)
    {
        Assert.Equal(expected, LaunchService.ShouldUseSteamHandoff(path));
    }
}
