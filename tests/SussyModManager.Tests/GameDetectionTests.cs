using SussyModManager.Core.Services;

namespace SussyModManager.Tests;

public class GameDetectionTests
{
    [Theory]
    [InlineData(@"C:\Program Files (x86)\Steam\steamapps\common\Among Us", AmongUsLocator.SteamChannel)]
    [InlineData(@"D:\SteamLibrary\steamapps\common\Among Us", AmongUsLocator.SteamChannel)]
    [InlineData(@"C:\Program Files\Epic Games\AmongUs", AmongUsLocator.EpicChannel)]
    [InlineData(@"C:\XboxGames\Among Us\Content", AmongUsLocator.EpicChannel)]
    [InlineData(@"C:\Program Files\WindowsApps\InnerSloth.AmongUs", AmongUsLocator.EpicChannel)]
    public void GuessChannel_MapsKnownStoreLayouts(string path, string expected)
    {
        Assert.Equal(expected, AmongUsLocator.GuessChannel(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\Games\Among Us")]
    public void GuessChannel_DefaultsToSteam(string? path)
    {
        Assert.Equal(AmongUsLocator.SteamChannel, AmongUsLocator.GuessChannel(path!));
    }
}
