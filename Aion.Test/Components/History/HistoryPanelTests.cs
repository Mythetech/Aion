using Aion.Components.History;
using Aion.Components.History.Commands;
using Aion.Components.Infrastructure.Commands;
using Aion.Components.NativeMenu;
using Aion.Test.TestDoubles;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.History;

public class HistoryPanelTests : TestContext
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly HistoryState _history = new(new InMemoryQueryHistoryStore(), NullLogger<HistoryState>.Instance);

    public HistoryPanelTests()
    {
        Services.AddMudServices(x => x.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(_bus);
        Services.AddSingleton(_history);
    }

    [Fact]
    public async Task SuccessAndFailure_RenderTheirOwnStatusAndMeta()
    {
        var now = DateTimeOffset.Now;
        await _history.AddAsync(HistoryEntries.Failed("SELECT name FROM products WHERE category_id = 2", "no such column: category_id", now.AddMinutes(-1)));
        await _history.AddAsync(HistoryEntries.Success("SELECT * FROM customers", now, rows: 10));

        var cut = RenderComponent<HistoryPanel>();

        var success = cut.Find("li.history-entry[data-status=Success]");
        success.QuerySelector(".history-sql")!.TextContent.ShouldBe("SELECT * FROM customers");
        success.QuerySelector(".history-meta")!.TextContent.ShouldContain("10 rows");
        success.QuerySelector(".history-meta")!.TextContent.ShouldContain("16 ms");
        success.QuerySelector(".history-error").ShouldBeNull();

        var failed = cut.Find("li.history-entry[data-status=Failed]");
        failed.QuerySelector(".history-sql")!.TextContent.ShouldBe("SELECT name FROM products WHERE category_id = 2");
        failed.QuerySelector(".history-error")!.TextContent.ShouldBe("no such column: category_id");
        failed.QuerySelector(".history-meta")!.TextContent.ShouldNotContain("rows");
    }

    [Fact]
    public async Task Update_ShowsRowsAffectedInsteadOfRowsReturned()
    {
        await _history.AddAsync(HistoryEntries.Success("UPDATE products SET price = 1 WHERE id = 1", DateTimeOffset.Now, rows: 0) with { RowsAffected = 1 });

        var cut = RenderComponent<HistoryPanel>();

        var meta = cut.Find("li.history-entry .history-meta").TextContent;
        meta.ShouldContain("1 row affected");
        meta.ShouldNotContain("0 rows");
    }

    [Fact]
    public async Task Entries_AreGroupedByDay()
    {
        var now = DateTimeOffset.Now;
        await _history.AddAsync(HistoryEntries.Success("SELECT yesterday", now.AddDays(-1)));
        await _history.AddAsync(HistoryEntries.Success("SELECT today", now));

        var cut = RenderComponent<HistoryPanel>();

        cut.FindAll(".history-group-label").Select(e => e.TextContent).ShouldBe(["Today", "Yesterday"]);
    }

    [Fact]
    public async Task Search_FiltersEntriesBySql()
    {
        var now = DateTimeOffset.Now;
        await _history.AddAsync(HistoryEntries.Success("SELECT * FROM customers", now.AddMinutes(-1)));
        await _history.AddAsync(HistoryEntries.Success("SELECT * FROM products", now));
        var cut = RenderComponent<HistoryPanel>();

        await cut.Find(".history-search input").InputAsync(new ChangeEventArgs { Value = "CUSTOMERS" });

        cut.FindAll(".history-sql").Select(e => e.TextContent).ShouldBe(["SELECT * FROM customers"]);
    }

    [Fact]
    public async Task Search_WithNoMatches_SaysSo()
    {
        await _history.AddAsync(HistoryEntries.Success("SELECT 1", DateTimeOffset.Now));
        var cut = RenderComponent<HistoryPanel>();

        await cut.Find(".history-search input").InputAsync(new ChangeEventArgs { Value = "orders" });

        cut.FindAll("li.history-entry").ShouldBeEmpty();
        cut.Find(".history-empty").TextContent.ShouldContain("No queries match");
    }

    [Fact]
    public void Empty_ShowsEmptyStateAndDisablesClear()
    {
        var cut = RenderComponent<HistoryPanel>();

        cut.Find(".history-empty").TextContent.ShouldContain("No history yet");
        ClearButton(cut).HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task NewRuns_AppearWithoutReopeningThePanel()
    {
        var cut = RenderComponent<HistoryPanel>();

        await cut.InvokeAsync(() => _history.AddAsync(HistoryEntries.Success("SELECT 42", DateTimeOffset.Now)));

        cut.Find(".history-sql").TextContent.ShouldBe("SELECT 42");
    }

    [Fact]
    public async Task Open_PublishesOpenWithoutRunning()
    {
        var entry = HistoryEntries.Success("SELECT 1", DateTimeOffset.Now);
        await _history.AddAsync(entry);
        var cut = RenderComponent<HistoryPanel>();

        await EntryButton(cut, "Open").ClickAsync(new MouseEventArgs());

        await _bus.Received(1).PublishAsync(Arg.Is<OpenHistoryEntry>(m => m.Entry == entry && !m.Run));
    }

    [Fact]
    public async Task RunAgain_PublishesOpenAndRun()
    {
        var entry = HistoryEntries.Success("SELECT 1", DateTimeOffset.Now);
        await _history.AddAsync(entry);
        var cut = RenderComponent<HistoryPanel>();

        await EntryButton(cut, "Run again").ClickAsync(new MouseEventArgs());

        await _bus.Received(1).PublishAsync(Arg.Is<OpenHistoryEntry>(m => m.Entry == entry && m.Run));
    }

    [Fact]
    public async Task Copy_PublishesTheEntrysSql()
    {
        await _history.AddAsync(HistoryEntries.Success("SELECT 1", DateTimeOffset.Now));
        var cut = RenderComponent<HistoryPanel>();

        await cut.Find("button[aria-label='Copy SQL']").ClickAsync(new MouseEventArgs());

        await _bus.Received(1).PublishAsync(Arg.Is<CopyToClipboard>(c => c.Text == "SELECT 1"));
    }

    [Fact]
    public async Task EnterOnAnEntry_OpensIt()
    {
        var entry = HistoryEntries.Success("SELECT 1", DateTimeOffset.Now);
        await _history.AddAsync(entry);
        var cut = RenderComponent<HistoryPanel>();

        await cut.Find("li.history-entry").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        await _bus.Received(1).PublishAsync(Arg.Is<OpenHistoryEntry>(m => m.Entry == entry && !m.Run));
    }

    [Fact]
    public async Task Clear_AsksForConfirmationThroughTheBus()
    {
        await _history.AddAsync(HistoryEntries.Success("SELECT 1", DateTimeOffset.Now));
        var cut = RenderComponent<HistoryPanel>();

        await ClearButton(cut).ClickAsync(new MouseEventArgs());

        await _bus.Received(1).PublishAsync(Arg.Any<ClearHistory>());
        _history.Entries.Count.ShouldBe(1);
    }

    // Button text also contains the icon's ligature, which is aria-hidden, so match on the label's end.
    private static IElement EntryButton(IRenderedFragment cut, string text) =>
        cut.FindAll("li.history-entry button").First(b => b.TextContent.Trim().EndsWith(text, StringComparison.Ordinal));

    private static IElement ClearButton(IRenderedFragment cut) =>
        cut.FindAll(".history-toolbar button").First(b => b.TextContent.Trim() == "Clear");
}
