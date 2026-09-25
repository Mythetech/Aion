using Aion.Components.Connections;
using Aion.Components.History;
using Aion.Components.Querying;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Test.TestDoubles;
using Aion.Web.Providers;
using Aion.Web.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;
using SqliteWasmBlazor;

namespace Aion.Test.Web.Services;

public class StorageRestoreServiceTests
{
    private readonly JsModuleFake _js = new();
    private readonly IConnectionService _connectionService = Substitute.For<IConnectionService>();
    private readonly ISqliteWasmInitializer _sqliteInitializer = Substitute.For<ISqliteWasmInitializer>();
    private readonly StorageRestoreService _sut;

    public StorageRestoreServiceTests()
    {
        _connectionService.GetSavedConnections().Returns(Enumerable.Empty<ConnectionModel>());

        var factory = new DatabaseProviderFactory(
        [
            new PGliteProvider(_js.Runtime),
            new SqliteWasmProvider(Substitute.For<ISqliteWasmDatabaseService>())
        ]);
        var bus = Substitute.For<IMessageBus>();
        var connectionState = new ConnectionState(_connectionService, factory, bus, Substitute.For<ILogger<ConnectionState>>());
        var queryState = new QueryState(bus, Substitute.For<IQuerySaveService>());

        _sut = new StorageRestoreService(
            new IndexedDbStorageService(_js.Runtime),
            connectionState,
            queryState,
            new HistoryState(Substitute.For<IQueryHistoryStore>(), NullLogger<HistoryState>.Instance),
            factory,
            _sqliteInitializer,
            NullLogger<StorageRestoreService>.Instance);
    }

    [Fact]
    public async Task RestoreAsync_CalledRepeatedly_LoadsConnectionsOnce()
    {
        await Task.WhenAll(_sut.RestoreAsync(), _sut.RestoreAsync());
        await _sut.RestoreAsync();

        await _sqliteInitializer.Received(1).InitializeAsync();
        await _connectionService.Received(1).InitializeAsync();
        _js.CallsTo("loadDatabaseMetas").Count.ShouldBe(1);
    }

    [Fact]
    public async Task RestoreAsync_SecondCaller_WaitsForTheRestoreInProgress()
    {
        var sqliteReady = new TaskCompletionSource();
        _sqliteInitializer.InitializeAsync().Returns(sqliteReady.Task);

        var first = _sut.RestoreAsync();
        var second = _sut.RestoreAsync();

        second.IsCompleted.ShouldBeFalse();

        sqliteReady.SetResult();
        await Task.WhenAll(first, second);

        await _connectionService.Received(1).InitializeAsync();
    }

    [Fact]
    public async Task RestoreAsync_OpensEachStoredPGliteDatabase()
    {
        _js.Returns("loadDatabaseMetas", """[{"name":"sales","type":6,"createdAt":"2026-01-01T00:00:00Z"}]""");

        await _sut.RestoreAsync();

        _js.CallsTo("create").ShouldHaveSingleItem().ShouldBe(["sales"]);
    }
}
