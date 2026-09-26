using Aion.Components.Shared;
using Bunit;
using MudBlazor.Services;
using Shouldly;

namespace Aion.Test.Components.Shared;

public class KeyValueListItemTests : TestContext
{
    public KeyValueListItemTests()
    {
        Services.AddMudServices(x => x.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Value_TruncatesOnOneLineWithTheFullTextOnHover()
    {
        var cut = RenderComponent<KeyValueListItem>(p => p
            .Add(x => x.Key, "Executed At")
            .Add(x => x.Value, "26/09/2026 14:05"));

        cut.Find(".kv-key").TextContent.Trim().ShouldBe("Executed At");
        var value = cut.Find(".kv-value");
        value.TextContent.Trim().ShouldBe("26/09/2026 14:05");
        value.GetAttribute("title").ShouldBe("26/09/2026 14:05");
    }
}
