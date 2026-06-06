using System.IO;
using SussyModManager.Core.Models;
using SussyModManager.Core.Services;

namespace SussyModManager.Tests;

public class ModInstallerLaunchTests : IDisposable
{
    private readonly string _root;
    private readonly string _game;
    private readonly ModInstaller _installer;

    public ModInstallerLaunchTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "smm_launch_test_" + Guid.NewGuid().ToString("N"));
        _game = Path.Combine(_root, "game");
        Directory.CreateDirectory(Path.Combine(_game, "BepInEx", "plugins"));
        _installer = new ModInstaller(new ModStore());
    }

    [Fact]
    public void PrepareModForLaunch_CopiesTopLevelDllEvenWhenSubfolderExists()
    {
        var modPath = Path.Combine(_root, "DraftModeTOUM");
        Directory.CreateDirectory(modPath);
        Directory.CreateDirectory(Path.Combine(modPath, "meta"));
        File.WriteAllText(Path.Combine(modPath, "DraftModeTOUM.dll"), "dll");
        File.WriteAllText(Path.Combine(modPath, "meta", "readme.txt"), "x");

        _installer.PrepareModForLaunch(
            new Mod { Id = "DraftModeTOUM", Name = "Draft Mode" },
            modPath,
            _game);

        Assert.True(File.Exists(Path.Combine(_game, "BepInEx", "plugins", "DraftModeTOUM.dll")));
    }

    [Fact]
    public void DeployLaunchPackAssets_SkipsPluginsAndInterop()
    {
        var pack = Path.Combine(_root, "pack");
        Directory.CreateDirectory(Path.Combine(pack, "BepInEx", "plugins"));
        Directory.CreateDirectory(Path.Combine(pack, "BepInEx", "interop"));
        Directory.CreateDirectory(Path.Combine(pack, "BepInEx", "config"));
        File.WriteAllText(Path.Combine(pack, "BepInEx", "plugins", "OnlyInPack.dll"), "x");
        File.WriteAllText(Path.Combine(pack, "BepInEx", "interop", "Assembly-CSharp.dll"), "x");
        File.WriteAllText(Path.Combine(pack, "BepInEx", "config", "tou.cfg"), "x");

        ModInstaller.DeployLaunchPackAssets(pack, _game);

        Assert.False(File.Exists(Path.Combine(_game, "BepInEx", "plugins", "OnlyInPack.dll")));
        Assert.False(File.Exists(Path.Combine(_game, "BepInEx", "interop", "Assembly-CSharp.dll")));
        Assert.True(File.Exists(Path.Combine(_game, "BepInEx", "config", "tou.cfg")));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }
}
