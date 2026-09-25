using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Test.TestDoubles;
using Aion.Web.Providers;
using Aion.Web.Services;
using NSubstitute;
using Shouldly;
using SqliteWasmBlazor;

namespace Aion.Test.Web.Services;

public class WebConnectionServiceTests
{
    private readonly JsModuleFake _js = new();
    private readonly ISqliteWasmDatabaseService _sqliteFiles = Substitute.For<ISqliteWasmDatabaseService>();
    private readonly WebConnectionService _sut;

    public WebConnectionServiceTests()
    {
        var factory = new DatabaseProviderFactory(
        [
            new PGliteProvider(_js.Runtime),
            new SqliteWasmProvider(_sqliteFiles)
        ]);
        _sut = new WebConnectionService(factory, new IndexedDbStorageService(_js.Runtime));
    }

    private static ConnectionModel PGlite(string database) => new()
    {
        Name = database,
        ConnectionString = $"pglite://{database}",
        Type = DatabaseType.WasmPostgreSQL
    };

    private static ConnectionModel Sqlite(string database) => new()
    {
        Name = database,
        ConnectionString = $"Data Source={database};Mode=Memory;Cache=Shared",
        Type = DatabaseType.WasmSQLite
    };

    [Fact]
    public async Task RemoveConnection_DeletesOnlyThatConnectionsDatabase()
    {
        var sales = PGlite("sales");
        await _sut.AddConnection(sales);
        await _sut.AddConnection(PGlite("inventory"));

        await _sut.RemoveConnection(sales.Id);

        _js.CallsTo("destroy").ShouldHaveSingleItem().ShouldBe(["sales"]);
        _js.CallsTo("deleteDatabaseMeta").ShouldHaveSingleItem().ShouldBe(["sales"]);
        _js.CallsTo("deleteConnection").ShouldHaveSingleItem().ShouldBe([sales.Id.ToString()]);
        (await _sut.GetSavedConnections()).Select(c => c.Name).ShouldBe(["inventory"]);
    }

    [Fact]
    public async Task RemoveConnection_KeepsADatabaseAnotherConnectionStillUses()
    {
        var first = PGlite("sales");
        await _sut.AddConnection(first);
        await _sut.AddConnection(PGlite("sales"));

        await _sut.RemoveConnection(first.Id);

        _js.CallsTo("destroy").ShouldBeEmpty();
        _js.CallsTo("deleteDatabaseMeta").ShouldBeEmpty();
        _js.CallsTo("deleteConnection").ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RemoveConnection_SameNameOnTheOtherEngine_KeepsThatEnginesDatabase()
    {
        var postgres = PGlite("sample_store");
        await _sut.AddConnection(postgres);
        await _sut.AddConnection(Sqlite("sample_store"));

        await _sut.RemoveConnection(postgres.Id);

        _js.CallsTo("destroy").ShouldHaveSingleItem().ShouldBe(["sample_store"]);
        await _sqliteFiles.DidNotReceive().DeleteDatabaseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _js.CallsTo("deleteDatabaseMeta").ShouldBeEmpty();
        var savedMeta = _js.CallsTo("saveDatabaseMeta").ShouldHaveSingleItem();
        ((string)savedMeta[0]!).ShouldContain($"\"type\":{(int)DatabaseType.WasmSQLite}");
    }

    [Fact]
    public async Task RemoveConnection_SqliteConnection_DeletesItsDatabaseFile()
    {
        var sqlite = Sqlite("notes");
        await _sut.AddConnection(sqlite);

        await _sut.RemoveConnection(sqlite.Id);

        await _sqliteFiles.Received(1).DeleteDatabaseAsync("notes.db", Arg.Any<CancellationToken>());
        _js.CallsTo("deleteDatabaseMeta").ShouldHaveSingleItem().ShouldBe(["notes"]);
    }

    [Fact]
    public async Task InitializeAsync_OverlappingCalls_DoNotDuplicateConnections()
    {
        var id = Guid.NewGuid();
        var json = $$"""[{"id":"{{id}}","name":"sales","connectionString":"pglite://sales","type":6}]""";
        var firstLoad = new TaskCompletionSource<string>();
        var secondLoad = new TaskCompletionSource<string>();
        var loads = new Queue<TaskCompletionSource<string>>([firstLoad, secondLoad]);
        _js.Module.InvokeAsync<string>("loadConnections", Arg.Any<object?[]?>())
            .Returns(_ => new ValueTask<string>(loads.Dequeue().Task));

        var first = _sut.InitializeAsync();
        var second = _sut.InitializeAsync();
        firstLoad.SetResult(json);
        await first;
        secondLoad.SetResult(json);
        await second;

        (await _sut.GetSavedConnections()).ShouldHaveSingleItem().Id.ShouldBe(id);
    }
}
