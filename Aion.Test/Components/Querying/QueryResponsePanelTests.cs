using Aion.Components.Connections;
using Aion.Components.Infrastructure.Commands;
using Aion.Components.Querying;
using Aion.Components.Querying.Errors;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Bunit;
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
    private readonly QueryModel _query;

    public QueryResponsePanelTests()
    {
        Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;

        _state = new QueryState(_bus, Substitute.For<IQuerySaveService>());
        var connections = new ConnectionState(
            Substitute.For<IConnectionService>(), Substitute.For<IDatabaseProviderFactory>(), _bus, new NullLogger<ConnectionState>());

        Services.AddSingleton(_bus);
        Services.AddSingleton(_state);
        Services.AddSingleton(connections);
        Services.AddSingleton(new SqlCompletionService(connections));

        _query = _state.Queries[0];
        _state.SetActive(_query);
    }

    private void Complete(QueryResult result)
    {
        _query.StartExecution();
        _query.SetResult(result);
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
}
