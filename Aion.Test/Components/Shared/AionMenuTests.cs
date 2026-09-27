using Aion.Components.Shared;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MudBlazor.Services;
using Shouldly;

namespace Aion.Test.Components.Shared;

public class AionMenuTests : TestContext
{
    private readonly IRenderedComponent<MudPopoverProvider> _popovers;

    public AionMenuTests()
    {
        // Aion turns popover flipping off app-wide, which left menus near the bottom of the window running off it.
        Services.AddMudServices(options => options.PopoverOptions.OverflowBehavior = OverflowBehavior.FlipNever);
        JSInterop.Mode = JSRuntimeMode.Loose;
        _popovers = RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public async Task OpenMenu_InsetsItsItemsFromThePopoverEdge()
    {
        await OpenAsync(RenderMenu());

        MenuList().ClassList.ShouldContain("pa-1");
    }

    [Fact]
    public async Task OpenMenu_HasDenseItems()
    {
        await OpenAsync(RenderMenu());

        _popovers.Find(".mud-menu-item").ClassList.ShouldContain("mud-menu-item-dense");
    }

    [Fact]
    public async Task ListClass_ReplacesTheDefaultInset()
    {
        await OpenAsync(RenderMenu(p => p.Add(x => x.ListClass, "pa-3")));

        MenuList().ClassList.ShouldContain("pa-3");
        MenuList().ClassList.ShouldNotContain("pa-1");
    }

    [Fact]
    public async Task OpenMenu_FlipsWhenThereIsNoRoomBelow()
    {
        await OpenAsync(RenderMenu());

        Popover().ClassList.ShouldContain(AionMenu.FlipWhenOutOfRoomClass);
    }

    [Fact]
    public async Task PopoverClass_KeepsTheMenuFlipping()
    {
        await OpenAsync(RenderMenu(p => p.Add(x => x.PopoverClass, "row-menu")));

        Popover().ClassList.ShouldContain("row-menu");
        Popover().ClassList.ShouldContain(AionMenu.FlipWhenOutOfRoomClass);
    }

    private IElement Popover() => _popovers.Find(".mud-popover");

    private IRenderedComponent<AionMenu> RenderMenu(Action<ComponentParameterCollectionBuilder<AionMenu>>? configure = null) =>
        RenderComponent<AionMenu>(p =>
        {
            p.Add(x => x.Label, "Actions");
            p.Add<MudMenuItem>(x => x.ChildContent, item => item.AddChildContent("Refresh"));
            configure?.Invoke(p);
        });

    private static async Task OpenAsync(IRenderedComponent<AionMenu> menu) =>
        await menu.Find("button.mud-menu-button-activator").ClickAsync(new MouseEventArgs());

    private IElement MenuList() => _popovers.Find(".mud-menu-list");
}
