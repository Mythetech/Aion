using Aion.Components.Connections;
using Aion.Components.Infrastructure.Commands;
using Aion.Components.Querying;
using Aion.Components.Settings.Domains;
using Aion.Components.Querying.Errors;
using Aion.Components.Querying.Messages;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.Querying;

public class QueryResponsePanelTests : TestContext
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly QueryState _state;
    private readonly ConnectionState _connections;
    private readonly QueryModel _query;
    private readonly QueryMessageLog _log = new();

    public QueryResponsePanelTests()
    {
        Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;

        _state = new QueryState(_bus, Substitute.For<IQuerySaveService>());
        var providers = Substitute.For<IDatabaseProviderFactory>();
        _connections = new ConnectionState(
            Substitute.For<IConnectionService>(), providers, _bus, new NullLogger<ConnectionState>());

        Services.AddSingleton(_bus);
        Services.AddSingleton(new ResultsSettings());
        Services.AddSingleton(_state);
        Services.AddSingleton(_connections);
        Services.AddSingleton(_log);
        Services.AddSingleton(providers);
        Services.AddSingleton(new SqlCompletionService(_connections));
        Services.AddSingleton(new QueryErrorSuggester(_connections, NullLogger<QueryErrorSuggester>.Instance));

        _query = _state.Queries[0];
        _state.SetActive(_query);
    }

    private void Complete(QueryResult result, QueryResultKind kind = QueryResultKind.Results)
    {
        _query.StartExecution();
        _query.SetResult(result, kind);
        _state.SetResult(_query, result);
    }

    [Fact]
    public void FailedRun_ShowsErrorCardInsteadOfAnEmptyGrid()
    {
        // Arrange
        Complete(new QueryResult { Error = "no such column: categry_id" });

        // Act
        var cut = RenderComponent<QueryResponsePanel>();

        // Assert
        cut.Find(".query-error-card").TextContent.ShouldContain("categry_id");
        cut.FindComponents<QueryResultTable>().ShouldBeEmpty();
    }

    [Fact]
    public void FailedRun_CardShowsTheNormalizedTitleTokenAndEngineCode()
    {
        // Arrange
        Complete(new QueryResult { Error = "Worker error: SQLITE_ERROR: sqlite3 result code 1: no such column: categry_id" });

        // Act
        var cut = RenderComponent<QueryResponsePanel>();

        // Assert
        cut.Find(".query-error-title").TextContent.ShouldBe("No such column");
        cut.Find(".query-error-token").TextContent.ShouldBe("categry_id");
        cut.Find(".query-error-meta").TextContent.ShouldStartWith("SQLITE_ERROR (code 1)");
        cut.Find(".query-error-card").TextContent.ShouldNotContain("Worker error");
    }

    [Fact]
    public void FailedRun_WithAnUnrecognizedError_ShowsTheMessageAsTheTitle()
    {
        // Arrange
        Complete(new QueryResult { Error = "division by zero" });

        // Act
        var cut = RenderComponent<QueryResponsePanel>();

        // Assert
        cut.Find(".query-error-title").TextContent.ShouldBe("Division by zero");
        cut.FindAll(".query-error-token").ShouldBeEmpty();
    }

    [Fact]
    public async Task FailedRun_WithAPosition_ShowsItAndOffersGoToLine()
    {
        // Arrange
        _query.Query = "SELECT name, price\nFROM products\nWHERE categry_id = 2";
        Complete(new QueryResult { Error = "no such column: categry_id" });
        var cut = RenderComponent<QueryResponsePanel>();

        // Act
        cut.Find(".query-error-location").TextContent.ShouldBe("Line 3, column 7");
        await cut.FindComponent<QueryErrorCard>().Find(".query-error-goto").ClickAsync(new());

        // Assert
        cut.Find(".query-error-goto").TextContent.Trim().ShouldBe("Go to line 3");
        await _bus.Received(1).PublishAsync(Arg.Is<Aion.Components.Querying.Commands.GoToQueryPosition>(
            c => c.QueryId == _query.Id && c.Line == 3 && c.Column == 7));
    }

    [Fact]
    public void FailedRun_WithoutAPosition_OffersNoGoToLine()
    {
        // Arrange
        Complete(new QueryResult { Error = "division by zero" });

        // Act
        var cut = RenderComponent<QueryResponsePanel>();

        // Assert
        cut.FindAll(".query-error-goto").ShouldBeEmpty();
        cut.FindAll(".query-error-location").ShouldBeEmpty();
    }

    [Fact]
    public async Task MessagesTab_LogsWhereTheErrorIs()
    {
        // Arrange
        _query.Query = "SELECT *\nFROM prodcts";
        Complete(new QueryResult { Error = "no such table: prodcts" });
        var cut = RenderComponent<QueryResponsePanel>();

        // Act
        await cut.FindAll(".mud-tab").First(t => t.TextContent.Contains("Messages")).ClickAsync(new());

        // Assert
        cut.Find(".query-message-error").TextContent.ShouldBe("Error at line 2, column 6: no such table: prodcts");
    }

    private static QueryResult Rows(int count) => new()
    {
        Columns = ["id"],
        Rows = Enumerable.Range(1, count).Select(i => new Dictionary<string, object> { ["id"] = i }).ToList()
    };

    [Fact]
    public async Task SelectedRows_SurviveChangesThatDontTouchTheResult()
    {
        // Arrange
        Complete(Rows(3));
        var cut = RenderComponent<QueryResponsePanel>();
        var selection = cut.FindComponent<QueryResultTable>().Instance.SelectionState;
        await cut.InvokeAsync(() => selection.ToggleSelection(1, ctrlKey: true, shiftKey: false, order: [0, 1, 2]));

        // Act
        await cut.InvokeAsync(() => _state.RenameQuery(_query, "Renamed"));

        // Assert
        selection.SelectedIndices.ShouldBe([1]);
    }

    [Fact]
    public async Task SelectedRows_ClearWhenTheResultIsReplaced()
    {
        // Arrange
        Complete(Rows(3));
        var cut = RenderComponent<QueryResponsePanel>();
        var selection = cut.FindComponent<QueryResultTable>().Instance.SelectionState;
        await cut.InvokeAsync(() => selection.ToggleSelection(1, ctrlKey: true, shiftKey: false, order: [0, 1, 2]));

        // Act
        await cut.InvokeAsync(() => Complete(Rows(2)));

        // Assert
        selection.SelectedIndices.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExportCsv_ExportsEveryFetchedRow_NotJustTheRowsShown()
    {
        // Arrange
        Services.GetRequiredService<ResultsSettings>().RowLimit = 2;
        Complete(Rows(5));
        var cut = RenderComponent<QueryResponsePanel>();

        // Act
        await cut.Find("[aria-label='Export to CSV']").ClickAsync(new());

        // Assert
        await _bus.Received(1).PublishAsync(Arg.Is<Aion.Components.Querying.Commands.ExportResultsToCsv>(c => c.Result!.Rows.Count == 5));
    }

    // The find box waits for typing to pause, so the filter applies once the grid shows the matching rows.
    private static async Task FindInResultsAsync(IRenderedComponent<QueryResponsePanel> cut, string text, int expectedRows)
    {
        await cut.Find("input[placeholder='Find in results...']").InputAsync(new ChangeEventArgs { Value = text });
        cut.WaitForAssertion(() => cut.FindAll("tbody tr.mud-table-row").Count.ShouldBe(expectedRows));
    }

    [Fact]
    public async Task ExportCsv_WhileFiltering_ExportsOnlyMatchingRows()
    {
        // Arrange
        Complete(Rows(15));
        var cut = RenderComponent<QueryResponsePanel>();
        await FindInResultsAsync(cut, "1", expectedRows: 7);

        // Act
        await cut.Find("[aria-label='Export to CSV']").ClickAsync(new());

        // Assert: ids 1 and 10 to 15 contain "1".
        await _bus.Received(1).PublishAsync(Arg.Is<Aion.Components.Querying.Commands.ExportResultsToCsv>(c =>
            c.Result!.Rows.Select(r => (int)r["id"]).SequenceEqual(new[] { 1, 10, 11, 12, 13, 14, 15 }) && c.TotalRows == 15));
    }

    [Fact]
    public async Task ExportJsonAndExcel_WhileFiltering_ExportOnlyMatchingRows()
    {
        // Arrange
        Complete(Rows(15));
        var cut = RenderComponent<QueryResponsePanel>();
        await FindInResultsAsync(cut, "12", expectedRows: 1);

        // Act
        await cut.Find("[aria-label='Export to JSON']").ClickAsync(new());
        await cut.Find("[aria-label='Export to Excel']").ClickAsync(new());

        // Assert
        await _bus.Received(1).PublishAsync(Arg.Is<Aion.Components.Querying.Commands.ExportResultsToJson>(c =>
            c.Result!.Rows.Count == 1 && c.TotalRows == 15));
        await _bus.Received(1).PublishAsync(Arg.Is<Aion.Components.Querying.Commands.ExportResultsToExcel>(c =>
            c.Result!.Rows.Count == 1 && c.TotalRows == 15));
    }

    private static string Collapse(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

    private void ConnectToCachedShop()
    {
        var database = new DatabaseModel
        {
            Name = "shop",
            Tables = [new TableInfo("", "products")],
            TablesLoaded = true,
            TableColumns = { ["products"] = [new ColumnInfo { Name = "category_id" }, new ColumnInfo { Name = "name" }] },
            LoadedColumnTables = ["products"]
        };
        var connection = new ConnectionModel { Name = "Local", Type = DatabaseType.WasmSQLite, Databases = [database] };
        _connections.Connections = [connection];
        _query.ConnectionId = connection.Id;
        _query.DatabaseName = database.Name;
    }

    [Fact]
    public void FailedRun_ForAMisspelledColumn_SuggestsTheColumnAndItsTable()
    {
        // Arrange
        ConnectToCachedShop();
        _query.Query = "SELECT name FROM products WHERE categry_id = 2";
        Complete(new QueryResult { Error = "no such column: categry_id" });

        // Act
        var cut = RenderComponent<QueryResponsePanel>();

        // Assert
        cut.WaitForAssertion(() =>
            Collapse(cut.Find(".query-error-suggestion").TextContent)
                .ShouldBe("Did you mean category_id? It's a column on products."));
    }

    [Fact]
    public void FailedRun_ForAMisspelledTable_SuggestsTheTable()
    {
        // Arrange
        ConnectToCachedShop();
        _query.Query = "SELECT * FROM prodcts";
        Complete(new QueryResult { Error = "no such table: prodcts" });

        // Act
        var cut = RenderComponent<QueryResponsePanel>();

        // Assert
        cut.WaitForAssertion(() =>
            Collapse(cut.Find(".query-error-suggestion").TextContent).ShouldBe("Did you mean products?"));
    }

    [Fact]
    public void FailedRun_WithoutACloseName_SuggestsNothing()
    {
        // Arrange
        ConnectToCachedShop();
        _query.Query = "SELECT weight FROM products";
        Complete(new QueryResult { Error = "no such column: weight" });

        // Act
        var cut = RenderComponent<QueryResponsePanel>();

        // Assert
        cut.FindAll(".query-error-suggestion").ShouldBeEmpty();
    }

    [Fact]
    public void FailedRun_FooterShowsFailedAndDurationInsteadOfResultCount()
    {
        // Arrange
        Complete(new QueryResult { Error = "boom" });

        // Act
        var cut = RenderComponent<QueryResponsePanel>();

        // Assert
        var footer = cut.Find(".query-response-footer");
        footer.QuerySelector(".query-status-failed")!.TextContent.ShouldBe("Failed");
        footer.TextContent.ShouldEndWith("ms");
        footer.TextContent.ShouldNotContain("Results");
        footer.TextContent.ShouldNotContain("Completed");
    }

    [Fact]
    public void SuccessfulRun_ShowsGridAndResultCount()
    {
        // Arrange
        Complete(new QueryResult { Columns = ["id"], Rows = [new Dictionary<string, object> { ["id"] = 1 }] });

        // Act
        var cut = RenderComponent<QueryResponsePanel>();

        // Assert
        cut.FindAll(".query-error-card").ShouldBeEmpty();
        cut.FindComponents<QueryResultTable>().Count.ShouldBe(1);
        cut.Find(".query-response-footer").TextContent.ShouldContain("1 Results");
        cut.FindAll(".query-status-failed").ShouldBeEmpty();
    }

    [Fact]
    public void CancelledRun_ShowsCancelledRatherThanAnErrorCard()
    {
        // Arrange
        Complete(new QueryResult { Error = "Query cancelled", Cancelled = true });

        // Act
        var cut = RenderComponent<QueryResponsePanel>();

        // Assert
        cut.FindAll(".query-error-card").ShouldBeEmpty();
        cut.Find(".query-cancelled").TextContent.ShouldContain("cancelled");
        cut.Find(".query-response-footer .query-status-cancelled").TextContent.ShouldBe("Cancelled");
    }

    [Fact]
    public async Task CopyError_PutsTheFullRawErrorOnTheClipboard()
    {
        // Arrange
        const string raw = "Worker error: SQLITE_ERROR: sqlite3 result code 1: no such column: categry_id";
        Complete(new QueryResult { Error = raw });
        var cut = RenderComponent<QueryResponsePanel>();

        // Act
        await cut.FindComponent<QueryErrorCard>().Find(".query-error-copy").ClickAsync(new());

        // Assert
        await _bus.Received(1).PublishAsync(Arg.Is<CopyToClipboard>(c => c.Text == raw));
    }

    [Fact]
    public async Task MessagesTab_LogsTheError()
    {
        // Arrange
        Complete(new QueryResult { Error = "no such table: prodcts" });
        var cut = RenderComponent<QueryResponsePanel>();

        // Act
        await cut.FindAll(".mud-tab").First(t => t.TextContent.Contains("Messages")).ClickAsync(new());

        // Assert
        cut.Find(".query-message-error").TextContent.ShouldContain("no such table: prodcts");
    }

    private static async Task OpenMessagesAsync(IRenderedComponent<QueryResponsePanel> cut) =>
        await cut.FindAll(".mud-tab").First(t => t.TextContent.Contains("Messages")).ClickAsync(new());

    private static readonly DateTimeOffset At = new(2026, 9, 26, 14, 2, 0, TimeSpan.Zero);

    [Fact]
    public async Task MessagesTab_ShowsTheTabsLogInOrder()
    {
        // Arrange
        Complete(new QueryResult { RowsAffected = 1 });
        _log.RecordBegin(_query.Id, At);
        _log.RecordRun(_query.Id, "UPDATE products SET stock = 0 WHERE id = 1", new QueryResult { RowsAffected = 1 }, At, TimeSpan.FromMilliseconds(43));
        var cut = RenderComponent<QueryResponsePanel>();

        // Act
        await OpenMessagesAsync(cut);

        // Assert
        cut.FindAll(".query-message .query-message-text").Select(e => e.TextContent).ShouldBe(
            ["BEGIN", "UPDATE products SET stock = 0 WHERE id = 1", "1 row affected"]);
        cut.FindAll(".query-message")[0].QuerySelector(".query-message-time")!.TextContent
            .ShouldBe(At.ToLocalTime().ToString("T"));
        cut.Find(".query-message-duration").TextContent.ShouldBe("(43ms)");
    }

    [Fact]
    public async Task MessagesTab_ShowsOnlyTheActiveTabsLog()
    {
        // Arrange
        Complete(new QueryResult { RowsAffected = 1 });
        _log.RecordBegin(Guid.NewGuid(), At);
        _log.RecordRun(_query.Id, "DELETE FROM carts", new QueryResult { RowsAffected = 1 }, At, TimeSpan.Zero);
        var cut = RenderComponent<QueryResponsePanel>();

        // Act
        await OpenMessagesAsync(cut);

        // Assert
        cut.FindAll(".query-message .query-message-text").Select(e => e.TextContent).ShouldBe(["DELETE FROM carts", "1 row affected"]);
    }

    [Fact]
    public async Task MessagesTab_ShowsLinesAsTheyAreLogged()
    {
        // Arrange
        Complete(new QueryResult { RowsAffected = 1 });
        _log.RecordBegin(_query.Id, At);
        var cut = RenderComponent<QueryResponsePanel>();
        await OpenMessagesAsync(cut);

        // Act
        await cut.InvokeAsync(() => _log.RecordEnd(_query.Id, committed: true, At));

        // Assert
        cut.FindAll(".query-message .query-message-text").Select(e => e.TextContent).ShouldBe(["BEGIN", "COMMIT"]);
    }

    [Fact]
    public async Task MessagesTab_MarksErrorsAndShowsTheWholeStatementOnHover()
    {
        // Arrange
        Complete(new QueryResult { Error = "no such table: prodcts" });
        _log.RecordRun(_query.Id, "SELECT *\nFROM prodcts", new QueryResult { Error = "no such table: prodcts" }, At, TimeSpan.Zero);
        var cut = RenderComponent<QueryResponsePanel>();

        // Act
        await OpenMessagesAsync(cut);

        // Assert
        cut.Find(".query-message-text.query-message-statement").GetAttribute("title").ShouldBe("SELECT *\nFROM prodcts");
        cut.Find(".query-message-error").TextContent.ShouldBe("Error: no such table: prodcts");
    }

    private static readonly QueryPlan Plan = new() { PlanType = "Estimated", PlanFormat = "TEXT", PlanContent = "Seq Scan on products" };

    // A run shows the running view in between, which rebuilds the results tabs, as the app does.
    private async Task RunAsync(IRenderedComponent<QueryResponsePanel> cut, QueryResult result, QueryResultKind kind = QueryResultKind.Results)
    {
        await cut.InvokeAsync(() =>
        {
            _query.StartExecution();
            _state.SetActive(_query);
        });
        await cut.InvokeAsync(() =>
        {
            if (kind == QueryResultKind.EstimatedPlan) _query.EstimatedPlan = Plan;
            if (kind == QueryResultKind.ActualPlan) _query.ActualPlan = Plan;
            _query.SetResult(result, kind);
            _state.SetResult(_query, result);
        });
    }

    private static string ActiveResultsTab(IRenderedComponent<QueryResponsePanel> cut) =>
        cut.Find(".mud-tab.mud-tab-active").TextContent.Trim();

    [Fact]
    public async Task Explain_OpensTheEstimatedPlanTab()
    {
        // Arrange
        Complete(Rows(3));
        var cut = RenderComponent<QueryResponsePanel>();

        // Act
        await RunAsync(cut, new QueryResult(), QueryResultKind.EstimatedPlan);

        // Assert
        cut.WaitForAssertion(() => ActiveResultsTab(cut).ShouldBe("Estimated Plan"));
        cut.FindComponent<QueryPlanVisualizer>().Instance.Plan.ShouldBe(Plan);
    }

    [Fact]
    public async Task ExplainAnalyze_OpensTheActualPlanTab()
    {
        // Arrange
        Complete(Rows(3));
        var cut = RenderComponent<QueryResponsePanel>();

        // Act
        await RunAsync(cut, new QueryResult(), QueryResultKind.ActualPlan);

        // Assert
        cut.WaitForAssertion(() => ActiveResultsTab(cut).ShouldBe("Actual Plan"));
        cut.FindComponent<QueryPlanVisualizer>().Instance.Plan.ShouldBe(Plan);
    }

    [Fact]
    public async Task Explain_WhileTheResultsTabsStayOnScreen_StillOpensThePlan()
    {
        // Arrange
        Complete(Rows(3));
        var cut = RenderComponent<QueryResponsePanel>();

        // Act
        await cut.InvokeAsync(() =>
        {
            _query.EstimatedPlan = Plan;
            Complete(new QueryResult(), QueryResultKind.EstimatedPlan);
        });

        // Assert
        cut.WaitForAssertion(() => ActiveResultsTab(cut).ShouldBe("Estimated Plan"));
    }

    [Fact]
    public async Task Run_AfterExplain_LandsOnResults()
    {
        // Arrange
        Complete(Rows(3));
        var cut = RenderComponent<QueryResponsePanel>();
        await RunAsync(cut, new QueryResult(), QueryResultKind.EstimatedPlan);
        cut.WaitForAssertion(() => ActiveResultsTab(cut).ShouldBe("Estimated Plan"));

        // Act
        await RunAsync(cut, Rows(2));

        // Assert
        cut.WaitForAssertion(() => ActiveResultsTab(cut).ShouldBe("Results"));
        cut.FindComponents<QueryResultTable>().Count.ShouldBe(1);
    }

    [Fact]
    public async Task Run_WhileReadingMessages_KeepsMessagesOpen()
    {
        // Arrange
        Complete(Rows(3));
        var cut = RenderComponent<QueryResponsePanel>();
        await OpenMessagesAsync(cut);

        // Act
        await RunAsync(cut, Rows(2));

        // Assert
        cut.WaitForAssertion(() => ActiveResultsTab(cut).ShouldBe("Messages"));
        cut.FindAll(".query-messages").Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(QueryResultKind.EstimatedPlan, "Estimated plan, statement not run")]
    [InlineData(QueryResultKind.ActualPlan, "Actual plan, changes rolled back")]
    public async Task PlanRun_FooterSaysWhatHappenedToTheStatement(QueryResultKind kind, string expected)
    {
        // Arrange
        Complete(Rows(3));
        var cut = RenderComponent<QueryResponsePanel>();

        // Act
        await RunAsync(cut, new QueryResult(), kind);

        // Assert
        var footer = cut.Find(".query-response-footer").TextContent;
        footer.ShouldContain(expected);
        footer.ShouldNotContain("Results");
    }

    private static string FooterCount(IRenderedComponent<QueryResponsePanel> cut) =>
        cut.Find(".query-response-footer .query-result-count").TextContent.Trim();

    [Fact]
    public async Task Footer_WhileFiltering_SaysHowManyOfTheResultsMatch()
    {
        // Arrange
        Complete(Rows(10));
        var cut = RenderComponent<QueryResponsePanel>();

        // Act: ids 1 and 10 contain "1".
        await FindInResultsAsync(cut, "1", expectedRows: 2);

        // Assert
        cut.WaitForAssertion(() => FooterCount(cut).ShouldBe("2 of 10 results"));
    }

    [Fact]
    public async Task Footer_AfterTheFilterIsCleared_CountsEveryResultAgain()
    {
        // Arrange
        Complete(Rows(10));
        var cut = RenderComponent<QueryResponsePanel>();
        await FindInResultsAsync(cut, "1", expectedRows: 2);

        // Act
        await FindInResultsAsync(cut, "", expectedRows: 10);

        // Assert
        cut.WaitForAssertion(() => FooterCount(cut).ShouldBe("10 Results"));
    }

    [Fact]
    public async Task Footer_WhileFilteringAStatementThatChangedRows_StillSaysRowsAffected()
    {
        // Arrange
        Complete(new QueryResult { RowsAffected = 4 });
        var cut = RenderComponent<QueryResponsePanel>();

        // Act
        await cut.Find("input[placeholder='Find in results...']").InputAsync(new ChangeEventArgs { Value = "x" });

        // Assert
        cut.WaitForAssertion(() => FooterCount(cut).ShouldBe("4 rows affected"));
    }
}
