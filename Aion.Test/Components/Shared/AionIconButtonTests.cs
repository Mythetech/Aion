using Aion.Components.Shared;
using Aion.Components.Theme;
using Bunit;
using MudBlazor;
using MudBlazor.Services;
using Shouldly;

namespace Aion.Test.Components.Shared;

public class AionIconButtonTests : TestContext
{
    public AionIconButtonTests()
    {
        Services.AddMudServices(x => x.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void AriaLabel_DefaultsToTooltip()
    {
        var cut = RenderComponent<AionIconButton>(p => p
            .Add(x => x.Icon, AionIcons.Settings)
            .Add(x => x.Tooltip, "Settings"));

        cut.Find("button").GetAttribute("aria-label").ShouldBe("Settings");
    }

    [Fact]
    public void AriaLabel_OverridesTooltip()
    {
        var cut = RenderComponent<AionIconButton>(p => p
            .Add(x => x.Icon, AionIcons.Close)
            .Add(x => x.Tooltip, "Close")
            .Add(x => x.AriaLabel, "Close Query1"));

        cut.Find("button").GetAttribute("aria-label").ShouldBe("Close Query1");
    }

    [Fact]
    public void AriaLabel_WithoutTooltip_IsRendered()
    {
        var cut = RenderComponent<AionIconButton>(p => p
            .Add(x => x.Icon, AionIcons.Aion)
            .Add(x => x.AriaLabel, "About Aion"));

        cut.Find("button").GetAttribute("aria-label").ShouldBe("About Aion");
    }

    [Fact]
    public void TooltipPlacement_IsPassedToTheTooltip()
    {
        var cut = RenderComponent<AionIconButton>(p => p
            .Add(x => x.Icon, AionIcons.Close)
            .Add(x => x.Tooltip, "Close panel")
            .Add(x => x.TooltipPlacement, Placement.Left));

        cut.FindComponent<MudTooltip>().Instance.Placement.ShouldBe(Placement.Left);
    }

    [Fact]
    public void AriaLabel_IsOmittedWhenNothingDescribesTheButton()
    {
        var cut = RenderComponent<AionIconButton>(p => p.Add(x => x.Icon, AionIcons.Add));

        cut.Find("button").HasAttribute("aria-label").ShouldBeFalse();
    }
}
