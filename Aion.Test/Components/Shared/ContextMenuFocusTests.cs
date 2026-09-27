using Aion.Components.Shared;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Shouldly;

namespace Aion.Test.Components.Shared;

public class ContextMenuFocusTests : TestContext
{
    public ContextMenuFocusTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<ContextMenuFocus> Render(Action? onEscape = null) =>
        RenderComponent<ContextMenuFocus>(p => p
            .AddChildContent("<div class=\"item\">Copy row</div>")
            .Add(x => x.OnEscape, () => onEscape?.Invoke()));

    [Fact]
    public void TakesFocusAsTheMenuOpens_WithoutScrolling()
    {
        Render();

        var focus = JSInterop.VerifyFocusAsyncInvoke();
        focus.Arguments[0].ShouldBeOfType<ElementReference>();
        focus.Arguments[1].ShouldBe(true);
    }

    [Fact]
    public void KeepsTheItemsItWraps()
    {
        var cut = Render();

        cut.Find(".context-menu-focus .item").TextContent.ShouldBe("Copy row");
    }

    [Fact]
    public async Task Escape_IsReported()
    {
        var escapes = 0;
        var cut = Render(() => escapes++);

        await cut.Find(".context-menu-focus").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        await cut.Find(".context-menu-focus").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        escapes.ShouldBe(1);
    }
}
