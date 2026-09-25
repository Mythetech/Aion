using Aion.Components.Connections;
using Aion.Components.History;
using Aion.Components.Querying;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Test.TestDoubles;
using Aion.Web.Databases;
using Aion.Web.Providers;
using Aion.Web.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;
using SqliteWasmBlazor;

namespace Aion.Test.Web.Databases;

public class BrowserStorageCleanerTests
{
    private readonly IQueryHistoryStore _historyStore = Substitute.For<IQueryHistoryStore>();
    private readonly HistoryState _history;
    private readonly BrowserStorageCleaner _sut;

    public BrowserStorageCleanerTests()
    {
        var js = new JsModuleFake();
        var connectionService = Substitute.For<IConnectionService>();
        connectionService.GetSavedConnections().Returns(Enumerable.Empty<ConnectionModel>());
        var factory = new DatabaseProviderFactory(
        [
            new PGliteProvider(js.Runtime),
            new SqliteWasmProvider(Substitute.For<ISqliteWasmDatabaseService>())
        ]);
        var bus = Substitute.For<IMessageBus>();

        _history = new HistoryState(_historyStore, NullLogger<HistoryState>.Instance);
        _sut = new BrowserStorageCleaner(
            new ConnectionState(connectionService, factory, bus, Substitute.For<ILogger<ConnectionState>>()),
            new QueryState(bus, Substitute.For<IQuerySaveService>()),
            _history,
            new IndexedDbStorageService(js.Runtime),
            factory);
    }

    [Fact]
    public async Task ClearAllAsync_ClearsQueryHistory()
    {
        await _history.AddAsync(new QueryHistoryEntry { Sql = "SELECT 1", ExecutedAt = DateTimeOffset.Now });
        _history.Entries.ShouldNotBeEmpty();
        _historyStore.ClearReceivedCalls();

        await _sut.ClearAllAsync();

        _history.Entries.ShouldBeEmpty();
        await _historyStore.Received().SaveAsync(Arg.Is<IReadOnlyList<QueryHistoryEntry>>(e => e.Count == 0));
    }
}
