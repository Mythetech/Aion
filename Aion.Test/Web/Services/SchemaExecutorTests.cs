using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Components.Scaffolding;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Aion.Test.TestDoubles;
using Aion.Web.Services;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Aion.Test.Web.Services;

public class SchemaExecutorTests
{
    private readonly InBrowserEngines _engines = new();
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly JsModuleFake _js = new();
    private readonly ConnectionState _connectionState;
    private readonly QueryState _queryState;
    private readonly SchemaExecutor _sut;
    private readonly TransactionInfo _transaction = new();
    private readonly List<string> _executed = [];

    public SchemaExecutorTests()
    {
        _connectionState = new ConnectionState(_engines.ConnectionService, _engines.Factory, _bus, Substitute.For<ILogger<ConnectionState>>());
        _queryState = new QueryState(_bus, Substitute.For<IQuerySaveService>());
        _sut = new SchemaExecutor(
            _engines.Factory,
            _connectionState,
            _queryState,
            new IndexedDbStorageService(_js.Runtime),
            _bus);

        var provider = _engines.Postgres;
        provider.Commands.GenerateCreateTableScript(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IEnumerable<ColumnDefinition>>())
            .Returns(ci => $"CREATE TABLE {ci.ArgAt<string>(2)}");
        provider.BeginTransactionAsync(Arg.Any<string>()).Returns(_transaction);
        provider.ExecuteInTransactionAsync(Arg.Any<string>(), Arg.Any<string>(), _transaction.Id, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                _executed.Add(ci.ArgAt<string>(1));
                return new QueryResult();
            });
    }

    private IDatabaseProvider Provider => _engines.Postgres;

    private static SchemaWizardModel Model(params string[] tables) => new()
    {
        DatabaseName = "shop",
        EngineType = DatabaseType.WasmPostgreSQL,
        Tables = tables.Select(name => new TableDefinitionModel
        {
            Name = name,
            Columns = [new ColumnDefinitionModel { Name = "id", DataType = "integer", IsPrimaryKey = true, IsNullable = false }]
        }).ToList()
    };

    private void FailTable(string table, QueryResult failure)
    {
        Provider.ExecuteInTransactionAsync(Arg.Any<string>(), $"CREATE TABLE {table}", _transaction.Id, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _executed.Add($"CREATE TABLE {table}");
                return failure;
            });
    }

    private static QueryResult EngineError(string message)
    {
        var result = new QueryResult();
        result.SetError(new QueryError { Raw = $"42P07: {message}", Message = message, Title = "Error" });
        return result;
    }

    [Fact]
    public async Task Execute_WhenEveryTableIsCreated_CommitsAndConnects()
    {
        var result = await _sut.ExecuteAsync(Model("customers", "orders"));

        result.Error.ShouldBeNull();
        result.Connection.ShouldNotBeNull();
        _executed.ShouldBe(["CREATE TABLE customers", "CREATE TABLE orders"]);
        await Provider.Received(1).CommitTransactionAsync(Arg.Any<string>(), _transaction.Id);
        _connectionState.Connections.ShouldHaveSingleItem().ShouldBe(result.Connection);
        _js.CallsTo("saveDatabaseMeta").ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Execute_WhenATableFails_ReportsTheEnginesMessageAndDoesNotConnect()
    {
        FailTable("orders", EngineError("relation \"orders\" already exists"));

        var result = await _sut.ExecuteAsync(Model("customers", "orders"));

        result.Connection.ShouldBeNull();
        result.Error.ShouldBe("Could not create table \"orders\": relation \"orders\" already exists");
        _connectionState.Connections.ShouldBeEmpty();
        _queryState.Queries.Select(q => q.Name).ShouldBe(["Query1"]);
        _js.CallsTo("saveDatabaseMeta").ShouldBeEmpty();
    }

    [Fact]
    public async Task Execute_StopsAtTheFirstFailingTableAndRollsBackTheOthers()
    {
        FailTable("orders", EngineError("syntax error at or near \"(\""));

        await _sut.ExecuteAsync(Model("customers", "orders", "items"));

        _executed.ShouldBe(["CREATE TABLE customers", "CREATE TABLE orders"]);
        await Provider.Received(1).RollbackTransactionAsync(Arg.Any<string>(), _transaction.Id);
        await Provider.DidNotReceive().CommitTransactionAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Execute_WhenTheEngineGivesOnlyRawErrorText_ReportsThatText()
    {
        FailTable("orders", new QueryResult { Error = "SQLite Error 1: 'near \"(\": syntax error'." });

        var result = await _sut.ExecuteAsync(Model("orders"));

        result.Error.ShouldBe("Could not create table \"orders\": SQLite Error 1: 'near \"(\": syntax error'.");
    }

    [Fact]
    public async Task Execute_WhenTheTransactionCannotStart_ReportsWhyAndRunsNothing()
    {
        Provider.BeginTransactionAsync(Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("Database 'shop' has an open transaction in a query tab."));

        var result = await _sut.ExecuteAsync(Model("orders"));

        result.Error.ShouldBe("Could not create the tables: Database 'shop' has an open transaction in a query tab.");
        _executed.ShouldBeEmpty();
        _connectionState.Connections.ShouldBeEmpty();
    }

    [Fact]
    public async Task Execute_WhenTheCommitFails_ReportsItAndDoesNotConnect()
    {
        Provider.CommitTransactionAsync(Arg.Any<string>(), _transaction.Id)
            .ThrowsAsync(new InvalidOperationException("database is locked"));

        var result = await _sut.ExecuteAsync(Model("orders"));

        result.Error.ShouldBe("Could not create the tables: database is locked");
        _connectionState.Connections.ShouldBeEmpty();
    }
}
