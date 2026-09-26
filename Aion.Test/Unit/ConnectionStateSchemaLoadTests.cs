using Aion.Components.Connections;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit;

public class ConnectionStateSchemaLoadTests
{
    private const string Database = "shop";

    private readonly IConnectionService _connectionService = Substitute.For<IConnectionService>();
    private readonly IDatabaseProvider _provider = Substitute.For<IDatabaseProvider, IDatabaseIndexProvider, IDatabaseRoutineProvider>();
    private readonly ConnectionState _sut;
    private readonly ConnectionModel _connection;
    private readonly DatabaseModel _database = new() { Name = Database };

    public ConnectionStateSchemaLoadTests()
    {
        _provider.DatabaseType.Returns(DatabaseType.PostgreSQL);
        _provider.UpdateConnectionString(Arg.Any<string>(), Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0));
        var factory = Substitute.For<IDatabaseProviderFactory>();
        factory.GetProvider(Arg.Any<DatabaseType>()).Returns(_provider);

        _sut = new ConnectionState(_connectionService, factory, Substitute.For<IMessageBus>(), NullLogger<ConnectionState>.Instance);
        _connection = new ConnectionModel
        {
            Name = "Local",
            ConnectionString = "Host=localhost",
            Type = DatabaseType.PostgreSQL,
            Active = true,
            Databases = [_database]
        };
        _sut.Connections.Add(_connection);
    }

    private IDatabaseIndexProvider Indexes => (IDatabaseIndexProvider)_provider;

    private IDatabaseRoutineProvider Routines => (IDatabaseRoutineProvider)_provider;

    private void TablesAre(params TableInfo[] tables) =>
        _connectionService.GetTablesAsync(Arg.Any<string>(), Database, DatabaseType.PostgreSQL).Returns(tables.ToList());

    private void TablesFail(string message) =>
        _connectionService.GetTablesAsync(Arg.Any<string>(), Database, DatabaseType.PostgreSQL)
            .Returns<List<TableInfo>>(_ => throw new InvalidOperationException(message));

    private static readonly TableInfo Products = new("public", "products");
    private static readonly TableInfo Orders = new("public", "orders");

    [Fact]
    public void NothingRequested_EveryPartIsNotLoaded()
    {
        _database.TablesState.ShouldBe(SchemaLoadState.NotLoaded);
        _database.IndexesState.ShouldBe(SchemaLoadState.NotLoaded);
        _database.RoutinesState.ShouldBe(SchemaLoadState.NotLoaded);
        _database.ColumnsState(Products.DisplayName).ShouldBe(SchemaLoadState.NotLoaded);
    }

    [Fact]
    public async Task LoadTables_WhenTheServerFails_RecordsTheDriverMessageInsteadOfThrowing()
    {
        TablesFail("42501: permission denied for schema public");

        var state = await _sut.LoadTablesAsync(_connection, _database);

        state.ShouldBe(SchemaLoadState.Failed("42501: permission denied for schema public"));
        _database.TablesState.ShouldBe(state);
        _database.TablesLoaded.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadTables_WhenTheMessageCarriesAStackTrace_KeepsOnlyItsFirstLine()
    {
        // Errors thrown across JS interop arrive with the JavaScript stack appended to the message.
        TablesFail("relation \"pg_tables\" is not readable\nError: relation \"pg_tables\" is not readable\n    at PGlite.query (pglite.js:1:2)");

        var state = await _sut.LoadTablesAsync(_connection, _database);

        state.Error.ShouldBe("relation \"pg_tables\" is not readable");
    }

    [Fact]
    public async Task LoadTables_AfterAFailure_TriesAgainAndLoads()
    {
        TablesFail("connection reset");
        await _sut.LoadTablesAsync(_connection, _database);
        TablesAre(Products);

        var state = await _sut.LoadTablesAsync(_connection, _database);

        state.ShouldBe(SchemaLoadState.Loaded);
        _database.Tables.ShouldBe([Products]);
    }

    [Fact]
    public async Task LoadTables_ReportsLoadingWhileTheServerAnswers()
    {
        var answer = new TaskCompletionSource<List<TableInfo>>();
        _connectionService.GetTablesAsync(Arg.Any<string>(), Database, DatabaseType.PostgreSQL).Returns(answer.Task);

        var load = _sut.LoadTablesAsync(_connection, _database);
        _database.TablesState.ShouldBe(SchemaLoadState.Loading);

        answer.SetResult([Products]);
        (await load).ShouldBe(SchemaLoadState.Loaded);
    }

    [Fact]
    public async Task LoadTables_NotifiesWhenAFailureIsRecorded()
    {
        TablesFail("offline");
        var notified = 0;
        _sut.ConnectionStateChanged += () => notified++;

        await _sut.LoadTablesAsync(_connection, _database);

        notified.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task LoadColumns_WhenTheServerFails_MarksOnlyThatTableFailed()
    {
        _provider.GetColumnsAsync(Arg.Any<string>(), Database, "public", "products")
            .Returns<List<ColumnInfo>>(_ => throw new InvalidOperationException("relation \"products\" does not exist"));
        _provider.GetColumnsAsync(Arg.Any<string>(), Database, "public", "orders")
            .Returns([new ColumnInfo { Name = "id" }]);

        var failed = await _sut.LoadColumnsAsync(_connection, _database, "public", "products");
        var loaded = await _sut.LoadColumnsAsync(_connection, _database, "public", "orders");

        failed.ShouldBe(SchemaLoadState.Failed("relation \"products\" does not exist"));
        _database.ColumnsState(Products.DisplayName).ShouldBe(failed);
        loaded.ShouldBe(SchemaLoadState.Loaded);
        _database.ColumnsState(Orders.DisplayName).ShouldBe(SchemaLoadState.Loaded);
        _database.LoadedColumnTables.ShouldBe([Orders.DisplayName]);
    }

    [Fact]
    public async Task LoadColumns_AfterAFailure_TriesAgain()
    {
        _provider.GetColumnsAsync(Arg.Any<string>(), Database, "public", "products")
            .Returns<List<ColumnInfo>>(_ => throw new InvalidOperationException("timeout"), _ => [new ColumnInfo { Name = "id" }]);
        await _sut.LoadColumnsAsync(_connection, _database, "public", "products");

        var state = await _sut.LoadColumnsAsync(_connection, _database, "public", "products");

        state.ShouldBe(SchemaLoadState.Loaded);
        _database.TableColumns[Products.DisplayName].Select(c => c.Name).ShouldBe(["id"]);
    }

    [Fact]
    public async Task LoadIndexes_WhenTheServerFails_RecordsTheFailure()
    {
        Indexes.GetIndexesAsync(Arg.Any<string>(), Database)
            .Returns<List<IndexInfo>>(_ => throw new InvalidOperationException("pg_indexes is not readable"));

        var state = await _sut.LoadIndexesAsync(_connection, _database);

        state.ShouldBe(SchemaLoadState.Failed("pg_indexes is not readable"));
        _database.IndexesLoaded.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadRoutines_WhenTheServerFails_RecordsTheFailure()
    {
        Routines.GetRoutinesAsync(Arg.Any<string>(), Database)
            .Returns<List<RoutineInfo>>(_ => throw new InvalidOperationException("no access"));

        var state = await _sut.LoadRoutinesAsync(_connection, _database);

        state.ShouldBe(SchemaLoadState.Failed("no access"));
    }

    [Fact]
    public async Task RefreshSchema_ReloadsOnlyWhatWasAlreadyLoaded()
    {
        TablesAre(Products);
        _provider.GetColumnsAsync(Arg.Any<string>(), Database, "public", "products").Returns([new ColumnInfo { Name = "id" }]);
        await _sut.LoadTablesAsync(_connection, _database);
        await _sut.LoadColumnsAsync(_connection, _database, "public", "products");
        _connectionService.ClearReceivedCalls();
        _provider.ClearReceivedCalls();

        await _sut.RefreshSchemaAsync(_connection, _database);

        await _connectionService.Received(1).GetTablesAsync(Arg.Any<string>(), Database, DatabaseType.PostgreSQL);
        await _provider.Received(1).GetColumnsAsync(Arg.Any<string>(), Database, "public", "products");
        await Indexes.DidNotReceiveWithAnyArgs().GetIndexesAsync(default!, default!);
        await Routines.DidNotReceiveWithAnyArgs().GetRoutinesAsync(default!, default!);
    }

    [Fact]
    public async Task RefreshSchema_PicksUpNewTablesAndForgetsColumnsOfDroppedOnes()
    {
        TablesAre(Products);
        _provider.GetColumnsAsync(Arg.Any<string>(), Database, "public", "products").Returns([new ColumnInfo { Name = "id" }]);
        await _sut.LoadTablesAsync(_connection, _database);
        await _sut.LoadColumnsAsync(_connection, _database, "public", "products");
        TablesAre(Orders);

        await _sut.RefreshSchemaAsync(_connection, _database);

        _database.Tables.ShouldBe([Orders]);
        _database.ColumnsState(Products.DisplayName).ShouldBe(SchemaLoadState.NotLoaded);
        _database.TableColumns.ShouldNotContainKey(Products.DisplayName);
    }

    [Fact]
    public async Task RefreshSchema_RetriesPartsThatFailed()
    {
        TablesFail("offline");
        await _sut.LoadTablesAsync(_connection, _database);
        TablesAre(Products);

        await _sut.RefreshSchemaAsync(_connection, _database);

        _database.TablesState.ShouldBe(SchemaLoadState.Loaded);
    }

    [Fact]
    public async Task SchemaChange_InTheDatabase_ReloadsItsLoadedTables()
    {
        TablesAre(Products);
        await _sut.LoadTablesAsync(_connection, _database);
        TablesAre(Products, Orders);

        await _sut.RefreshAfterSchemaChangeAsync(_connection.Id, Database, databasesChanged: false);

        _database.Tables.ShouldBe([Products, Orders]);
        await _connectionService.DidNotReceiveWithAnyArgs().GetDatabasesAsync(default!, default);
    }

    [Fact]
    public async Task SchemaChange_ToDatabases_ListsTheDatabasesAgain()
    {
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), DatabaseType.PostgreSQL).Returns([Database, "new_database"]);

        await _sut.RefreshAfterSchemaChangeAsync(_connection.Id, Database, databasesChanged: true);

        _connection.Databases.Select(d => d.Name).ShouldBe([Database, "new_database"]);
    }

    [Fact]
    public async Task RefreshDatabase_KeepsLoadedDatabasesSoTheTreeKeepsItsShape()
    {
        TablesAre(Products);
        await _sut.LoadTablesAsync(_connection, _database);
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), DatabaseType.PostgreSQL).Returns([Database, "archive"]);
        TablesAre(Products, Orders);

        await _sut.RefreshDatabaseAsync(_connection);

        _connection.Databases.Select(d => d.Name).ShouldBe([Database, "archive"]);
        _connection.Databases[0].ShouldBeSameAs(_database);
        _database.Tables.ShouldBe([Products, Orders]);
        _connection.Databases[1].TablesState.ShouldBe(SchemaLoadState.NotLoaded);
    }
}
