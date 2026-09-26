using Aion.Components.Shared;
using AngleSharp.Dom;
using Bunit;
using MudBlazor;
using MudBlazor.Services;
using Shouldly;

namespace Aion.Test.Components.Shared;

public class AionSelectTests : TestContext
{
    private readonly IRenderedComponent<MudPopoverProvider> _popovers;

    public AionSelectTests()
    {
        // Aion turns popover flipping off app-wide, which is what left selects running off the window.
        Services.AddMudServices(options => options.PopoverOptions.OverflowBehavior = OverflowBehavior.FlipNever);
        JSInterop.Mode = JSRuntimeMode.Loose;
        _popovers = RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public async Task OpenSelect_FlipsItsListWhenThereIsNoRoomBelow()
    {
        await OpenAsync(RenderSelect());

        Popover().ClassList.ShouldContain(AionSelect<string>.FlipWhenOutOfRoomClass);
    }

    [Fact]
    public async Task PopoverClass_KeepsTheListFlipping()
    {
        await OpenAsync(RenderSelect(p => p.Add(x => x.PopoverClass, "generator-list")));

        Popover().ClassList.ShouldContain("generator-list");
        Popover().ClassList.ShouldContain(AionSelect<string>.FlipWhenOutOfRoomClass);
    }

    private IRenderedComponent<AionSelect<string>> RenderSelect(Action<ComponentParameterCollectionBuilder<AionSelect<string>>>? configure = null) =>
        RenderComponent<AionSelect<string>>(p =>
        {
            p.Add(x => x.Label, "Data Type");
            p.Add<MudSelectItem<string>>(x => x.ChildContent, item => item.Add(i => i.Value, "INTEGER"));
            configure?.Invoke(p);
        });

    private static async Task OpenAsync(IRenderedComponent<AionSelect<string>> select) =>
        await select.InvokeAsync(select.Instance.OpenMenu);

    private IElement Popover() => _popovers.Find(".mud-popover");
}
