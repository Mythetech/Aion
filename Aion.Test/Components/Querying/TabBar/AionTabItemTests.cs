using Aion.Components.Querying;
using Aion.Components.Querying.TabBar;
using Bunit;
using MudBlazor.Services;
using Shouldly;

namespace Aion.Test.Components.Querying.TabBar;

public class AionTabItemTests : TestContext
{
    public AionTabItemTests()
    {
        Services.AddMudServices(x =>
        {
            x.PopoverOptions.CheckForPopoverProvider = false;
        });
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Should_Render_Tab_Name()
    {
        var query = new QueryModel { Name = "My Query", SavedQuery = "" };

        var cut = RenderComponent<AionTabItem>(p => p
            .Add(x => x.Query, query));

        cut.Find(".tab-name").TextContent.ShouldBe("My Query");
    }

    [Fact]
    public void Should_Show_Active_Class_When_Active()
    {
        var query = new QueryModel { Name = "Test", SavedQuery = "" };

        var cut = RenderComponent<AionTabItem>(p => p
            .Add(x => x.Query, query)
            .Add(x => x.IsActive, true));

        cut.Find(".aion-tab-item").ClassList.ShouldContain("active");
    }

    [Fact]
    public void Should_Not_Show_Active_Class_When_Inactive()
    {
        var query = new QueryModel { Name = "Test", SavedQuery = "" };

        var cut = RenderComponent<AionTabItem>(p => p
            .Add(x => x.Query, query)
            .Add(x => x.IsActive, false));

        cut.Find(".aion-tab-item").ClassList.ShouldNotContain("active");
    }

    [Fact]
    public void Should_Show_Dirty_Class_When_Query_Is_Dirty()
    {
        var query = new QueryModel { Name = "Test", SavedQuery = "", Query = "SELECT 1" };

        var cut = RenderComponent<AionTabItem>(p => p
            .Add(x => x.Query, query));

        cut.Find(".aion-tab-item").ClassList.ShouldContain("dirty");
    }

    [Fact]
    public void Should_Not_Show_Dirty_Class_When_Query_Is_Clean()
    {
        var query = new QueryModel { Name = "Test", SavedQuery = "", Query = "" };

        var cut = RenderComponent<AionTabItem>(p => p
            .Add(x => x.Query, query));

        cut.Find(".aion-tab-item").ClassList.ShouldNotContain("dirty");
    }

    [Fact]
    public async Task Should_Invoke_OnClick_When_Clicked()
    {
        var query = new QueryModel { Name = "Test", SavedQuery = "" };
        QueryModel? clicked = null;

        var cut = RenderComponent<AionTabItem>(p => p
            .Add(x => x.Query, query)
            .Add(x => x.OnClick, (QueryModel q) => { clicked = q; }));

        await cut.Find(".aion-tab-item").ClickAsync(new());

        clicked.ShouldBe(query);
    }

    [Fact]
    public async Task Should_Invoke_OnClose_On_Middle_Click()
    {
        var query = new QueryModel { Name = "Test", SavedQuery = "" };
        QueryModel? closed = null;

        var cut = RenderComponent<AionTabItem>(p => p
            .Add(x => x.Query, query)
            .Add(x => x.OnClose, (QueryModel q) => { closed = q; }));

        await cut.Find(".aion-tab-item").TriggerEventAsync("onmouseup",
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs { Button = 1 });

        closed.ShouldBe(query);
    }

    [Fact]
    public void Should_Show_Emphasis_Color_Data_Attribute()
    {
        var query = new QueryModel { Name = "Test", SavedQuery = "", EmphasisColor = "#FF0000" };

        var cut = RenderComponent<AionTabItem>(p => p
            .Add(x => x.Query, query));

        var tabItem = cut.Find(".aion-tab-item");
        tabItem.GetAttribute("data-has-color").ShouldBe("true");
        tabItem.GetAttribute("style").ShouldContain("--tab-color: #FF0000");
    }

    [Fact]
    public void Should_Not_Show_Color_Attribute_When_No_Emphasis()
    {
        var query = new QueryModel { Name = "Test", SavedQuery = "" };

        var cut = RenderComponent<AionTabItem>(p => p
            .Add(x => x.Query, query));

        var tabItem = cut.Find(".aion-tab-item");
        tabItem.GetAttribute("data-has-color").ShouldBeNull();
    }
}
