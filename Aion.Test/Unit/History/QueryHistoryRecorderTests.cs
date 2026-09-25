using Aion.Components.Connections;
using Aion.Components.History;
using Aion.Components.History.Consumers;
using Aion.Components.Querying;
using Aion.Components.Querying.Events;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Aion.Test.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.History;

public class QueryHistoryRecorderTests
{
    private readonly HistoryState _history = new(new InMemoryQueryHistoryStore(), NullLogger<HistoryState>.Instance);
    private readonly ConnectionModel _connection = new() { Name = "sample_store", ConnectionString = "Data Source=:memory:", Type = DatabaseType.WasmSQLite };
    private readonly QueryHistoryRecorder _recorder;

    public QueryHistoryRecorderTests()
    {
        var connections = new ConnectionState(
            Substitute.For<IConnectionService>(),
            Substitute.For<IDatabaseProviderFactory>(),
            Substitute.For<IMessageBus>(),
            NullLogger<ConnectionState>.Instance);
        connections.Connections.Add(_connection);
        _recorder = new QueryHistoryRecorder(_history, connections);
    }

    [Fact]
    public async Task FailedRun_StaysFailedAfterTheTabRunsSomethingElse()
    {
        var query = NewTab("SELECT name FROM products WHERE category_id = 2");

        await RunAsync(query, new QueryResult { Error = "no such column: category_id" });
        query.Query = "SELECT * FROM products";
        await RunAsync(query, Rows(3));

        var failed = _history.Entries.Single(e => e.Status == QueryHistoryStatus.Failed);
        failed.Sql.ShouldBe("SELECT name FROM products WHERE category_id = 2");
        failed.ErrorMessage.ShouldBe("no such column: category_id");
        failed.RowCount.ShouldBeNull();
    }

    [Fact]
    public async Task EachRun_KeepsTheSqlItRan()
    {
        var query = NewTab("SELECT 1");

        await RunAsync(query, Rows(1));
        query.Query = "SELECT 2";
        await RunAsync(query, Rows(2));

        _history.Entries.Select(e => e.Sql).ShouldBe(["SELECT 2", "SELECT 1"]);
        _history.Entries.Select(e => e.RowCount).ShouldBe([2, 1]);
    }

    [Fact]
    public async Task RecordsTheSelectionThatRan_EvenAfterTheEditorRestoresTheFullText()
    {
        var query = NewTab("SELECT 1;\nSELECT 2;");
        query.Query = "SELECT 2;";
        query.StartExecution();
        query.SetResult(Rows(1));
        var message = new QueryExecuted(query);

        query.Query = "SELECT 1;\nSELECT 2;";
        await _recorder.Consume(message);

        _history.Entries.Single().Sql.ShouldBe("SELECT 2;");
    }

    [Fact]
    public async Task SuccessfulRun_RecordsConnectionDatabaseRowsAndTiming()
    {
        var query = NewTab("SELECT * FROM customers");

        await RunAsync(query, Rows(10));

        var entry = _history.Entries.Single();
        entry.Status.ShouldBe(QueryHistoryStatus.Success);
        entry.ConnectionId.ShouldBe(_connection.Id);
        entry.ConnectionName.ShouldBe("sample_store");
        entry.DatabaseName.ShouldBe("main");
        entry.RowCount.ShouldBe(10);
        entry.ErrorMessage.ShouldBeNull();
        entry.Duration.ShouldNotBeNull();
        entry.ExecutedAt.ShouldBe(query.ExecutionStartTime!.Value);
    }

    [Fact]
    public async Task CancelledRun_RecordsCancelled()
    {
        var query = NewTab("SELECT pg_sleep(60)");

        await RunAsync(query, new QueryResult { Error = "Query cancelled", Cancelled = true });

        var entry = _history.Entries.Single();
        entry.Status.ShouldBe(QueryHistoryStatus.Cancelled);
        entry.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task RunWithoutAResult_DoesNotReportTheResultOfAnEarlierRun()
    {
        var query = NewTab("SELECT broken");
        await RunAsync(query, new QueryResult { Error = "syntax error" });

        query.StartExecution();
        await _recorder.Consume(new QueryExecuted(query));

        _history.Entries[0].Status.ShouldBe(QueryHistoryStatus.Success);
        _history.Entries[0].ErrorMessage.ShouldBeNull();
    }

    private QueryModel NewTab(string sql) => new()
    {
        Name = "Sample: Products by Price",
        Query = sql,
        ConnectionId = _connection.Id,
        DatabaseName = "main"
    };

    private async Task RunAsync(QueryModel query, QueryResult result)
    {
        query.StartExecution();
        query.SetResult(result);
        await _recorder.Consume(new QueryExecuted(query));
    }

    private static QueryResult Rows(int count) => new()
    {
        Columns = ["id"],
        Rows = Enumerable.Range(0, count).Select(i => new Dictionary<string, object> { ["id"] = i }).ToList()
    };
}
