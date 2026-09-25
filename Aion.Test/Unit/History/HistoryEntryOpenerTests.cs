using Aion.Components.Connections;
using Aion.Components.History;
using Aion.Components.History.Commands;
using Aion.Components.History.Consumers;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Shared.Snackbar.Commands;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Test.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.History;

public class HistoryEntryOpenerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly QueryState _queries;
    private readonly ConnectionModel _connection = new() { Name = "sample_store", ConnectionString = "", Type = DatabaseType.WasmSQLite };
    private readonly HistoryEntryOpener _opener;

    public HistoryEntryOpenerTests()
    {
        _queries = new QueryState(_bus, Substitute.For<IQuerySaveService>());
        var connections = new ConnectionState(
            Substitute.For<IConnectionService>(),
            Substitute.For<IDatabaseProviderFactory>(),
            _bus,
            NullLogger<ConnectionState>.Instance);
        connections.Connections.Add(_connection);
        _opener = new HistoryEntryOpener(_queries, connections, _bus);
    }

    [Fact]
    public async Task Open_CreatesAFocusedTabWithTheEntrysSqlConnectionAndDatabase_WithoutRunning()
    {
        var original = _queries.Queries.Single();
        var entry = Entry(_connection.Id, "SELECT * FROM products WHERE category_id = 2");

        await _opener.Consume(new OpenHistoryEntry(entry));

        _queries.Queries.Count.ShouldBe(2);
        var opened = _queries.Active.ShouldNotBeNull();
        opened.Id.ShouldNotBe(original.Id);
        opened.Query.ShouldBe("SELECT * FROM products WHERE category_id = 2");
        opened.ConnectionId.ShouldBe(_connection.Id);
        opened.DatabaseName.ShouldBe("main");
        opened.IsDirty.ShouldBeTrue();
        await _bus.Received(1).PublishAsync(Arg.Is<FocusQuery>(f => f.Query == opened));
        await _bus.DidNotReceive().PublishAsync(Arg.Any<RunQuery>());
    }

    [Fact]
    public async Task RunAgain_FocusesTheNewTabThenRunsIt()
    {
        var entry = Entry(_connection.Id, "SELECT 1");

        await _opener.Consume(new OpenHistoryEntry(entry, Run: true));

        Received.InOrder(() =>
        {
            _bus.PublishAsync(Arg.Is<FocusQuery>(f => f.Query.Query == "SELECT 1"));
            _bus.PublishAsync(Arg.Any<RunQuery>());
        });
    }

    [Fact]
    public async Task RunAgain_WhenTheConnectionIsGone_OpensWithoutAConnectionWarnsAndDoesNotRun()
    {
        var entry = Entry(Guid.NewGuid(), "SELECT 1") with { ConnectionName = "old_db" };

        await _opener.Consume(new OpenHistoryEntry(entry, Run: true));

        var opened = _queries.Active.ShouldNotBeNull();
        opened.Query.ShouldBe("SELECT 1");
        opened.ConnectionId.ShouldBeNull();
        opened.DatabaseName.ShouldBeNull();
        await _bus.Received(1).PublishAsync(Arg.Is<AddNotification>(n =>
            n.Severity == Severity.Warning && n.Message.Contains("old_db")));
        await _bus.DidNotReceive().PublishAsync(Arg.Any<RunQuery>());
    }

    private static QueryHistoryEntry Entry(Guid connectionId, string sql) =>
        HistoryEntries.Success(sql) with { ConnectionId = connectionId, ConnectionName = "sample_store", DatabaseName = "main" };
}
