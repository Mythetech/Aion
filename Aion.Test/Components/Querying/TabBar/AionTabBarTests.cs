using Aion.Components.Querying;
using Aion.Components.Querying.TabBar;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.Querying.TabBar;

public class AionTabBarTests : TestContext
{
    private readonly QueryState _state;
    private readonly IMessageBus _bus;

    public AionTabBarTests()
    {
        _bus = Substitute.For<IMessageBus>();
        var saveService = Substitute.For<IQuerySaveService>();
        _state = new QueryState(_bus, saveService);

        Services.AddSingleton(_state);
        Services.AddSingleton(_bus);
        Services.AddMudServices(x =>
        {
            x.PopoverOptions.CheckForPopoverProvider = false;
        });
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Should_Render_All_Queries_As_Tabs()
    {
        _state.AddQuery("Query 2");
        _state.AddQuery("Query 3");

        var cut = RenderComponent<AionTabBar>();

        var tabNames = cut.FindAll(".tab-name");
        tabNames.Count.ShouldBe(3);
    }

    [Fact]
    public void Should_Mark_Active_Tab()
    {
        var q2 = _state.AddQuery("Query 2");
        _state.SetActive(q2);

        var cut = RenderComponent<AionTabBar>();

        var activeTabs = cut.FindAll(".aion-tab-item.active");
        activeTabs.Count.ShouldBe(1);
    }

    [Fact]
    public void Should_Render_Tabs_In_Order()
    {
        _state.AddQuery("Second");
        _state.AddQuery("Third");

        var cut = RenderComponent<AionTabBar>();

        var tabNames = cut.FindAll(".tab-name");
        tabNames[0].TextContent.ShouldBe("Query1");
        tabNames[1].TextContent.ShouldBe("Second");
        tabNames[2].TextContent.ShouldBe("Third");
    }
}
