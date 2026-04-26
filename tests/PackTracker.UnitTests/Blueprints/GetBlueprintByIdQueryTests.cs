using System.Reflection;
using System.Text.Json;
using PackTracker.Application.Blueprints.Queries.GetBlueprintById;
using PackTracker.Application.DTOs.Crafting;

namespace PackTracker.UnitTests.Blueprints;

public sealed class GetBlueprintByIdQueryTests
{
    [Fact]
    public void BuildComponent_UsesHumanizedKey_WhenResourceNameIsMissing()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "name": "Precision Parts",
              "children": [
                {
                  "kind": "resource",
                  "key": "hadanite",
                  "quantity_scu": 0.0
                }
              ]
            }
            """);

        var method = typeof(GetBlueprintByIdQueryHandler)
            .GetMethod("BuildComponent", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = method!.Invoke(null, new object[] { document.RootElement });

        var component = Assert.IsType<BlueprintComponentDto>(result);
        Assert.Equal("Hadanite", component.MaterialName);
    }
}
