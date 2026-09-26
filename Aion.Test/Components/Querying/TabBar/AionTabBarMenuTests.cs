using Aion.Components.Querying;
using Aion.Components.Querying.TabBar;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.Querying.TabBar;

public class AionTabBarMenuTests : TestContext
{
    private readonly QueryState _state;
    private readonly IRenderedComponent<MudPopoverProvider> _popovers;
    private readonly IRenderedComponent<MudDialogProvider> _dialogs;
    private readonly List<QueryModel> _activated = [];

    public AionTabBarMenuTests()
    {
        _state = new QueryState(Substitute.For<IMessageBus>(), Substitute.For<IQuerySaveService>());
        Services.AddSingleton(_state);
        Services.AddSingleton(Substitute.For<IMessageBus>());
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        _popovers = RenderComponent<MudPopoverProvider>();
        _dialogs = RenderComponent<MudDialogProvider>();
    }

    private QueryModel Tab(string name, string sql)
    {
        var query = _state.AddQuery(name);
        _state.EditQueryText(query, sql);
        return query;
    }

    private static Task CloseButtonOf(IRenderedComponent<AionTabBar> cut, string name) =>
        cut.FindAll(".aion-tab-item")
            .Single(t => t.QuerySelector(".tab-name")!.TextContent == name)
            .QuerySelector(".close-btn")!
            .ClickAsync(new MouseEventArgs());

    /// <returns>The menu item's click, which stays pending until any confirmation is answered.</returns>
    private async Task<Task> ChooseFromTabMenuAsync(IRenderedComponent<AionTabBar> cut, string tabName, string item)
    {
        await cut.FindAll(".aion-tab-item")
            .Single(t => t.QuerySelector(".tab-name")!.TextContent == tabName)
            .ContextMenuAsync(new MouseEventArgs());
        _popovers.WaitForAssertion(() => _popovers.FindAll(".mud-list-item").ShouldNotBeEmpty());

        return _popovers.FindAll(".mud-list-item")
            .First(li => li.QuerySelector(".mud-list-item-text")?.TextContent.Trim() == item)
            .ClickAsync(new MouseEventArgs());
    }

    private async Task AnswerAsync(string button) =>
        await _dialogs.FindAll("button").First(b => b.TextContent.Trim() == button).ClickAsync(new MouseEventArgs());

    private IEnumerable<string> TabNames => _state.Queries.Select(q => q.Name);

    private IRenderedComponent<AionTabBar> RenderTabBar() =>
        RenderComponent<AionTabBar>(p => p.Add(x => x.OnTabActivated, query => _activated.Add(query)));

    [Fact]
    public async Task Clone_ShowsTheCloneInTheEditor()
    {
        // Arrange
        Tab("Report", "SELECT 1");
        var cut = RenderTabBar();

        // Act
        await await ChooseFromTabMenuAsync(cut, "Report", "Clone");

        // Assert
        _activated.ShouldHaveSingleItem().ShouldBe(_state.Active);
        _state.Active!.Query.ShouldBe("SELECT 1");
        _state.Queries.Count.ShouldBe(3);
    }

    [Fact]
    public async Task CloseToRight_ShowsTheTabThatBecomesActive()
    {
        // Arrange
        var report = Tab("Report", "");
        Tab("Totals", "");
        var cut = RenderTabBar();

        // Act
        await await ChooseFromTabMenuAsync(cut, "Report", "Close to Right");

        // Assert
        _state.Active.ShouldBe(report);
        _activated.ShouldHaveSingleItem().ShouldBe(report);
    }

    [Fact]
    public async Task ClosingATabWithoutSql_DoesNotAsk()
    {
        // Arrange
        Tab("Report", "SELECT 1");
        var cut = RenderComponent<AionTabBar>();

        // Act
        await CloseButtonOf(cut, "Query1");

        // Assert
        _dialogs.FindAll(".mud-dialog").ShouldBeEmpty();
        TabNames.ShouldBe(["Report"]);
    }

    [Fact]
    public async Task ClosingATabWithSql_AsksAndCancelKeepsIt()
    {
        // Arrange
        Tab("Report", "SELECT 1");
        var cut = RenderComponent<AionTabBar>();

        // Act
        var close = CloseButtonOf(cut, "Report");
        _dialogs.WaitForAssertion(() => _dialogs.Markup.ShouldContain("Close \"Report\"?"));
        await AnswerAsync("Cancel");
        await close;

        // Assert
        TabNames.ShouldBe(["Query1", "Report"]);
    }

    [Fact]
    public async Task ClosingATabWithSql_ClosesItOnceConfirmed()
    {
        // Arrange
        Tab("Report", "SELECT 1");
        var cut = RenderComponent<AionTabBar>();

        // Act
        var close = CloseButtonOf(cut, "Report");
        _dialogs.WaitForAssertion(() => _dialogs.Markup.ShouldContain("Close \"Report\"?"));
        await AnswerAsync("Close Tab");
        await close;

        // Assert
        TabNames.ShouldBe(["Query1"]);
    }

    [Fact]
    public async Task CloseAll_AsksOnceAndListsOnlyTheTabsWithSql()
    {
        // Arrange
        Tab("Report", "SELECT 1");
        Tab("Totals", "SELECT 2");
        var cut = RenderComponent<AionTabBar>();

        // Act
        var closeAll = await ChooseFromTabMenuAsync(cut, "Report", "Close All");

        // Assert
        _dialogs.WaitForAssertion(() => _dialogs.FindAll(".mud-dialog").Count.ShouldBe(1));
        var listed = _dialogs.FindAll(".confirm-dialog-items li").Select(li => li.TextContent.Trim());
        listed.ShouldBe(["Report", "Totals"]);

        await AnswerAsync("Close Tabs");
        await closeAll;
        TabNames.ShouldNotContain("Report");
        TabNames.ShouldNotContain("Totals");
    }

    [Fact]
    public async Task CloseOthers_OnlyAsksAboutTheTabsItCloses()
    {
        // Arrange
        Tab("Report", "SELECT 1");
        Tab("Totals", "SELECT 2");
        var cut = RenderComponent<AionTabBar>();

        // Act
        var closeOthers = await ChooseFromTabMenuAsync(cut, "Report", "Close Others");

        // Assert
        _dialogs.WaitForAssertion(() => _dialogs.FindAll(".confirm-dialog-items li").ShouldNotBeEmpty());
        _dialogs.FindAll(".confirm-dialog-items li").Select(li => li.TextContent.Trim()).ShouldBe(["Totals"]);

        await AnswerAsync("Cancel");
        await closeOthers;
        TabNames.ShouldBe(["Query1", "Report", "Totals"]);
    }
}
