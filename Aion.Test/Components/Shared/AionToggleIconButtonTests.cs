using Aion.Components.Shared;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MudBlazor.Services;
using Shouldly;

namespace Aion.Test.Components.Shared;

public class AionToggleIconButtonTests : TestContext
{
    public AionToggleIconButtonTests()
    {
        Services.AddMudServices(x => x.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<AionToggleIconButton> Render(bool pressed, Action<ComponentParameterCollectionBuilder<AionToggleIconButton>>? configure = null) =>
        RenderComponent<AionToggleIconButton>(p =>
        {
            p.Add(x => x.Icon, "lock_open");
            p.Add(x => x.PressedIcon, "lock");
            p.Add(x => x.Label, "Transactions");
            p.Add(x => x.Pressed, pressed);
            configure?.Invoke(p);
        });

    [Fact]
    public void IsNamedByItsLabelWhateverItsState()
    {
        // Act
        var off = Render(pressed: false);
        var on = Render(pressed: true);

        // Assert
        off.Find("button").GetAttribute("aria-label").ShouldBe("Transactions");
        on.Find("button").GetAttribute("aria-label").ShouldBe("Transactions");
    }

    [Fact]
    public void TellsAssistiveTechnologyWhetherItIsOn()
    {
        // Act
        var off = Render(pressed: false);
        var on = Render(pressed: true);

        // Assert
        off.Find("button").GetAttribute("aria-pressed").ShouldBe("false");
        on.Find("button").GetAttribute("aria-pressed").ShouldBe("true");
    }

    [Fact]
    public void Tooltip_SaysWhatItDoesAndWhetherItIsOn()
    {
        // Act
        var cut = Render(pressed: true, p => p.Add(x => x.Description, "Run in a transaction"));

        // Assert
        cut.FindComponent<MudTooltip>().Instance.Text.ShouldBe("Run in a transaction: on");
    }

    [Fact]
    public async Task Clicking_AsksForTheOtherState()
    {
        // Arrange
        bool? requested = null;
        var cut = Render(pressed: false, p => p.Add(x => x.PressedChanged, (bool value) => requested = value));

        // Act
        await cut.Find("button").ClickAsync(new MouseEventArgs());

        // Assert
        requested.ShouldBe(true);
    }

    [Fact]
    public void WhileDisabled_SaysWhy()
    {
        // Act
        var cut = Render(pressed: true, p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.DisabledReason, "Commit or roll back first"));

        // Assert
        cut.Find("button").HasAttribute("disabled").ShouldBeTrue();
        cut.FindComponent<MudTooltip>().Instance.Text.ShouldBe("Commit or roll back first");
    }
}
