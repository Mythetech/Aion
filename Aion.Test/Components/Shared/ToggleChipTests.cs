using Aion.Components.Shared;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MudBlazor.Services;
using Shouldly;

namespace Aion.Test.Components.Shared;

public class ToggleChipTests : TestContext
{
    public ToggleChipTests()
    {
        Services.AddMudServices(x =>
        {
            x.PopoverOptions.CheckForPopoverProvider = false;
        });
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Should_Render_With_Initial_Parameters()
    {
        // Arrange
        var parameters = new ComponentParameter[]
        {
            ComponentParameter.CreateParameter("Text", "Test Toggle"),
            ComponentParameter.CreateParameter("Icon", "test-icon"),
            ComponentParameter.CreateParameter("Value", false)
        };

        // Act
        var cut = RenderComponent<ToggleChip>(parameters);

        // Assert
        var chip = cut.FindComponent<MudChip<string>>();
        var tooltip = cut.FindComponent<MudTooltip>();

        chip.Find(".mud-chip-content").InnerHtml.ShouldBe("Test Toggle");
        chip.Instance.Icon.ShouldBe("test-icon");
        chip.Instance.Variant.ShouldBe(Variant.Outlined);
        tooltip.Instance.Text.ShouldBe("Test Toggle (Off)");
    }

    [Fact]
    public void Should_Update_Appearance_When_Value_Changes()
    {
        // Arrange
        var cut = RenderComponent<ToggleChip>(parameters => parameters
            .Add(p => p.Text, "Test Toggle")
            .Add(p => p.Value, false)
        );

        // Act
        cut.Find(".mud-chip").Click();

        // Assert
        var chip = cut.FindComponent<MudChip<string>>();
        var tooltip = cut.FindComponent<MudTooltip>();
        
        chip.Instance.Variant.ShouldBe(Variant.Text); 
        tooltip.Instance.Text.ShouldBe("Test Toggle (On)");
    }

    [Fact]
    public async Task Should_Trigger_ValueChanged_When_Clicked()
    {
        // Arrange
        bool newValue = false;
        var cut = RenderComponent<ToggleChip>(parameters => parameters
            .Add(p => p.Text, "Test Toggle")
            .Add(p => p.Value, false)
            .Add(p => p.ValueChanged, (bool val) => newValue = val)
        );

        // Act
        await cut.Find(".mud-chip").ClickAsync(new MouseEventArgs());

        // Assert
        newValue.ShouldBeTrue();
    }
}