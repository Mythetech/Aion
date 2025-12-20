using Bunit;
using AngleSharp.Dom;

namespace Aion.Test.Components.Shared.Tabs;

public class SimpleTabTests : TestContext
{
    public SimpleTabTests()
    {
        
    }

    [Fact]
    public void Can_Render_Tabs()
    {
        // Act
        var cut = RenderComponent<TestTabComponent>();
        
        // Assert
        var tabs = cut.FindAll(".mud-link");
        Assert.Equal(2, tabs.Count);
        
        // Initially first tab should be active
        var tabContents = cut.Find(".px-4").TextContent;
        Assert.Contains("Tab1", tabContents);
        Assert.DoesNotContain("Tab2", tabContents);
        
        // When clicking the second tab
        tabs[1].Click();
        
        // Then second tab should be active
        tabContents = cut.Find(".px-4").TextContent;
        Assert.DoesNotContain("Tab1", tabContents);
        Assert.Contains("Tab2", tabContents);
    }
}