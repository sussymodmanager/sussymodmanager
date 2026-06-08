using SussyModManager.Core.Helpers;
using SussyModManager.Core.Models;
using SussyModManager.Core.Services;

namespace SussyModManager.Tests;

public class PresetShareFileTests
{
    [Fact]
    public void PresetShareFile_RoundTripsPreset()
    {
        var original = new Preset
        {
            Id = "user-abc",
            Name = "My Pack",
            Description = "Test preset for sharing.",
            Builtin = false,
            ModIds = new List<string> { "TownOfUs", "AUnlocker" },
            InstallOrder = new List<string> { "AUnlocker", "TownOfUs" }
        };

        var json = PresetShareFile.ToJson(original);
        var loaded = PresetShareFile.FromJson(json);

        Assert.NotNull(loaded);
        Assert.Equal(original.Name, loaded.Name);
        Assert.Equal(original.Description, loaded.Description);
        Assert.Equal(original.ModIds, loaded.ModIds);
        Assert.Equal(original.InstallOrder, loaded.InstallOrder);
        Assert.False(loaded.Builtin);
        Assert.NotEqual(original.Id, loaded.Id);
    }

    [Fact]
    public void PresetShareFile_OmitsIdAndBuiltinFromExport()
    {
        var preset = new Preset
        {
            Id = "sus-af-pack",
            Name = "SUS AF",
            Builtin = true,
            ModIds = new List<string> { "TownOfUs" }
        };

        var json = PresetShareFile.ToJson(preset);

        Assert.DoesNotContain("sus-af-pack", json, StringComparison.Ordinal);
        Assert.DoesNotContain("builtin", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PresetShareFile_ReadsBundledPresetFileFormat()
    {
        var json = """
            {
              "presets": [
                {
                  "name": "Shared Pack",
                  "modIds": ["DraftModeTOUM"]
                }
              ]
            }
            """;

        var loaded = PresetShareFile.FromJson(json);

        Assert.NotNull(loaded);
        Assert.Equal("Shared Pack", loaded.Name);
        Assert.Single(loaded.ModIds);
        Assert.False(loaded.Builtin);
    }

    [Fact]
    public void PresetService_UpsertUserPreset_DoesNotClobberBuiltin()
    {
        var service = new PresetService();
        var config = new Config();
        var builtin = service.LoadBuiltinPresets().First();
        Assert.NotNull(builtin);

        var imported = new Preset
        {
            Id = builtin.Id,
            Name = "Imported copy",
            ModIds = new List<string> { "TownOfUs" }
        };

        service.UpsertUserPreset(config, imported);

        Assert.All(config.UserPresets, p => Assert.False(p.Builtin));
        Assert.DoesNotContain(config.UserPresets, p =>
            string.Equals(p.Id, builtin.Id, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(config.UserPresets, p =>
            string.Equals(p.Name, "Imported copy", StringComparison.Ordinal));
    }

    [Fact]
    public void PresetService_UpsertUserPreset_UpdatesExistingByName()
    {
        var service = new PresetService();
        var config = new Config
        {
            UserPresets = new List<Preset>
            {
                new Preset
                {
                    Id = "keep-me",
                    Name = "My Pack",
                    ModIds = new List<string> { "AUnlocker" }
                }
            }
        };
        var originalCreated = config.UserPresets[0].CreatedUtcTicks;

        var updated = new Preset
        {
            Name = "My Pack",
            Description = "Updated description",
            ModIds = new List<string> { "TownOfUs", "AUnlocker" }
        };

        service.UpsertUserPreset(config, updated);

        Assert.Single(config.UserPresets);
        Assert.Equal("keep-me", config.UserPresets[0].Id);
        Assert.Equal(originalCreated, config.UserPresets[0].CreatedUtcTicks);
        Assert.Equal(2, config.UserPresets[0].ModIds.Count);
    }
}
