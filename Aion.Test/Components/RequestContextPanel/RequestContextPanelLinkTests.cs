using Aion.Components.RequestContextPanel;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MudBlazor.Services;
using Shouldly;

namespace Aion.Test.Components.RequestContextPanel;

public class RequestContextPanelLinkTests : TestContext
{
    public RequestContextPanelLinkTests()
    {
        Services.AddMudServices(x =>
        {
            x.PopoverOptions.CheckForPopoverProvider = false;
        });
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Should_Render_As_Default_When_Not_Active()
    {
        // Arrange & Act
        var cut = RenderComponent<RequestContextPanelLink>(parameters => parameters
            .Add(p => p.Text, "Test Link")
            .Add(p => p.IsActive, false)
        );

        // Assert
        var link = cut.FindComponent<MudLink>();
        link.Instance.Color.ShouldBe(Color.Default);
        link.Markup.ShouldNotContain("bg-primary-50");
        link.Find(".mud-typography").TextContent.ShouldBe("Test Link");
    }

    [Fact]
    public void Should_Render_As_Primary_When_Active()
    {
        // Arrange & Act
        var cut = RenderComponent<RequestContextPanelLink>(parameters => parameters
            .Add(p => p.Text, "Test Link")
            .Add(p => p.IsActive, true)
        );

        // Assert
        var link = cut.FindComponent<MudLink>();
        link.Instance.Color.ShouldBe(Color.Primary);
        link.Markup.ShouldContain("bg-primary-50");
        link.Find(".mud-typography").TextContent.ShouldBe("Test Link");
    }

    [Fact]
    public async Task Should_Trigger_OnClick_Event_When_Clicked()
    {
        // Arrange
        bool wasClicked = false;
        var cut = RenderComponent<RequestContextPanelLink>(parameters => parameters
            .Add(p => p.Text, "Test Link")
            .Add(p => p.OnClick, EventCallback.Factory.Create(this, () => { wasClicked = true; }))
        );

        // Act
        await cut.Find(".mud-link").ClickAsync(new MouseEventArgs());

        // Assert
        wasClicked.ShouldBeTrue();
    }
} 



