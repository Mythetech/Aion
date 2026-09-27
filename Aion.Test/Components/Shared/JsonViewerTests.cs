using Aion.Components.Shared;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Mythetech.Framework.Components.Buttons;
using Mythetech.Framework.Infrastructure.Guards;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.Shared;

public class JsonViewerTests : TestContext
{
    private const string Json = "{\"id\":1,\"tags\":[\"a\"]}";

    public JsonViewerTests()
    {
        Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(Substitute.For<IJsGuardService>());
    }

    [Theory]
    [InlineData("pretty")]
    [InlineData("raw")]
    public void TreeAndRawViews_CopyTheJsonAsItCame(string viewMode)
    {
        // Arrange & Act
        var cut = RenderComponent<JsonViewer>(p => p
            .Add(x => x.Json, Json)
            .Add(x => x.ViewMode, viewMode));

        // Assert
        cut.FindComponent<MtCopyButton>().Instance.Text.ShouldBe(Json);
    }

    [Fact]
    public void FormattedView_CopiesTheIndentedJsonItShows()
    {
        // Arrange & Act
        var cut = RenderComponent<JsonViewer>(p => p
            .Add(x => x.Json, Json)
            .Add(x => x.ViewMode, "formatted"));

        // Assert
        cut.FindComponent<MtCopyButton>().Instance.Text.ShouldBe(
            "{\n  \"id\": 1,\n  \"tags\": [\n    \"a\"\n  ]\n}".ReplaceLineEndings());
    }

    [Fact]
    public void Body_WrapsTheViewSoItScrollsUnderTheToolbar()
    {
        // Arrange & Act
        var cut = RenderComponent<JsonViewer>(p => p
            .Add(x => x.Json, Json)
            .Add(x => x.ViewMode, "raw"));

        // Assert
        cut.Find(".json-viewer > .json-viewer-body pre").ShouldNotBeNull();
    }
}
