using SussyModManager.Core.Models;
using SussyModManager.Core.Services;

namespace SussyModManager.Tests;

public class ModReconcileTests
{
    [Fact]
    public void ReconcileInstalledMods_RemovesGhostEntriesWithoutFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm-reconcile-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(data);

        try
        {
            var config = new Config { DataPath = data };
            config.InstalledMods.Add(new InstalledMod { Id = "GhostMod", Name = "Ghost" });
            config.SelectedMods.Add("GhostMod");

            var manager = new ModManager(config);
            var result = manager.ReconcileInstalledMods();

            Assert.True(result.Changed);
            Assert.Contains("Ghost", result.RemovedFromInstalled);
            Assert.Contains("GhostMod", result.RemovedFromSelection);
            Assert.Empty(config.InstalledMods);
            Assert.Empty(config.SelectedMods);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void ValidateBeforeLaunch_FailsWhenSelectedModMissingOnDisk()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm-validate-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(data);

        try
        {
            var config = new Config { DataPath = data };
            config.InstalledMods.Add(new InstalledMod { Id = "Broken", Name = "Broken Mod" });
            config.SelectedMods.Add("Broken");

            var manager = new ModManager(config);
            var validation = manager.ValidateBeforeLaunch();

            Assert.False(validation.Success);
            Assert.Empty(config.InstalledMods);
            Assert.Empty(config.SelectedMods);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void ImportCustomDll_AddsDllAndSelectsForLaunch()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm-custom-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "data");
        var dllSource = Path.Combine(root, "MyPlugin.dll");
        Directory.CreateDirectory(data);
        File.WriteAllText(dllSource, "fake");

        try
        {
            var config = new Config { DataPath = data };
            var manager = new ModManager(config);
            var result = manager.ImportCustomDll(dllSource);

            Assert.True(result.Success);
            Assert.Single(config.InstalledMods);
            Assert.True(config.InstalledMods[0].IsCustom);
            Assert.Contains(config.InstalledMods[0].Id, config.SelectedMods);
            Assert.True(manager.HasLaunchableFiles(config.InstalledMods[0].Id));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
