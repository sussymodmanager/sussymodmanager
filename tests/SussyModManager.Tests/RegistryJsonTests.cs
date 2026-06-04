using System.Linq;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Models;

namespace SussyModManager.Tests;

public class RegistryJsonTests
{
    private const string Sample = @"{
        ""version"": ""1"",
        ""mods"": [
            {
                ""id"": ""TownOfUs"",
                ""name"": ""Town of Us Mira"",
                ""category"": ""Mod"",
                ""featured"": true,
                ""dependencies"": [ { ""modId"": ""MiraAPI"" }, { ""modId"": ""Reactor"" } ],
                ""incompatibilities"": [ ""TOHE"" ]
            },
            {
                ""id"": ""MiraAPI"",
                ""name"": ""Mira API"",
                ""category"": ""Dependency""
            }
        ]
    }";

    [Fact]
    public void Deserialize_ReadsEntriesCaseInsensitively()
    {
        var registry = Json.Deserialize<ModRegistry>(Sample);

        Assert.NotNull(registry);
        Assert.Equal(2, registry!.mods.Count);

        var tou = registry.mods.First(m => m.id == "TownOfUs");
        Assert.True(tou.featured);
        Assert.Equal(2, tou.dependencies.Count);
        Assert.Contains("TOHE", tou.incompatibilities);
    }

    [Fact]
    public void Deserialize_ToleratesNullAndEmpty()
    {
        Assert.Null(Json.Deserialize<ModRegistry>(null));
        Assert.Null(Json.Deserialize<ModRegistry>(""));
        Assert.Null(Json.Deserialize<ModRegistry>("   "));
    }

    [Fact]
    public void Deserialize_SkipsCommentsAndTrailingCommas()
    {
        const string json = @"{
            // a comment
            ""version"": ""2"",
            ""mods"": [ { ""id"": ""A"", ""name"": ""Alpha"" }, ]
        }";

        var registry = Json.Deserialize<ModRegistry>(json);
        Assert.NotNull(registry);
        Assert.Single(registry!.mods);
        Assert.Equal("Alpha", registry.mods[0].name);
    }
}
