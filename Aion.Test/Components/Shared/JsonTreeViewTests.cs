using System.Text.RegularExpressions;
using Aion.Components.Shared.JsonTreeView;
using Bunit;
using MudBlazor.Services;
using Shouldly;

namespace Aion.Test.Components.Shared;

public class JsonTreeViewTests : TestContext
{
    public JsonTreeViewTests()
    {
        Services.AddMudServices(x => x.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<JsonTreeView> Render(string json) =>
        RenderComponent<JsonTreeView>(p => p.Add(x => x.Json, json));

    // Each row's key and value text, leaving out the icon, whose text is a Material Symbols ligature.
    private static List<string> Rows(IRenderedFragment cut) =>
        cut.FindAll(".mud-treeview-item-content")
            .Select(row => string.Join(" ", row.QuerySelectorAll(".mud-typography, .mud-chip")
                .Select(part => Regex.Replace(part.TextContent, @"\s+", " ").Trim())))
            .ToList();

    [Fact]
    public void AnArray_IsOneItemWithAChildPerElement()
    {
        var cut = Render("""{"tags": ["a", "b", "c"]}""");

        Rows(cut).ShouldBe(["tags Array 3", "0 a", "1 b", "2 c"]);
    }

    [Fact]
    public void ArrayElements_OfEveryKind_AreShown()
    {
        var cut = Render("""{"values": [42, true, null, {"name": "x"}, [7]]}""");

        Rows(cut).ShouldBe(["values Array 5", "0 42", "1 true", "2 Null", "3 Object 1", "name x", "4 Array 1", "0 7"]);
    }
}
