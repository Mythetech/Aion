using Aion.Components.Connections;
using Aion.Components.Connections.Consumers;
using Aion.Components.Querying;
using Aion.Components.Querying.Events;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit;

public class SchemaChangeRefresherTests
{
    private readonly IConnectionService _connectionService = Substitute.For<IConnectionService>();
    private readonly ConnectionState _connections;
    private readonly ConnectionModel _connection;
    private readonly DatabaseModel _database = new() { Name = "shop", TablesLoaded = true };
    private readonly SchemaChangeRefresher _sut;

    public SchemaChangeRefresherTests()
    {
        var provider = Substitute.For<IDatabaseProvider>();
        provider.DatabaseType.Returns(DatabaseType.WasmSQLite);
        var factory = Substitute.For<IDatabaseProviderFactory>();
        factory.GetProvider(DatabaseType.WasmSQLite).Returns(provider);
        _connections = new ConnectionState(_connectionService, factory, Substitute.For<IMessageBus>(), NullLogger<ConnectionState>.Instance);
        _connection = new ConnectionModel { Name = "shop", Type = DatabaseType.WasmSQLite, Active = true, Databases = [_database] };
        _connections.Connections.Add(_connection);
        _connectionService.GetTablesAsync(Arg.Any<string>(), "shop", DatabaseType.WasmSQLite).Returns([new TableInfo("", "new_table")]);
        _sut = new SchemaChangeRefresher(_connections, new PendingSchemaChanges());
    }

    private QueryExecuted Ran(string sql, QueryResult result, TransactionInfo? transaction = null,
        QueryResultKind kind = QueryResultKind.Results)
    {
        var query = new QueryModel { Query = sql, ConnectionId = _connection.Id, DatabaseName = "shop", Transaction = transaction };
        query.StartExecution();
        query.SetResult(result, kind);
        return new QueryExecuted(query);
    }

    private TransactionFinished Finished(TransactionInfo transaction, bool committed) =>
        new(_connection.Id, Guid.NewGuid(),
            transaction.WithStatus(committed ? TransactionStatus.Committed : TransactionStatus.RolledBack), committed);

    private Task NoTablesReloaded() =>
        _connectionService.DidNotReceiveWithAnyArgs().GetTablesAsync(default!, default!, default);

    [Fact]
    public async Task SuccessfulCreateTable_ReloadsTheTablesOfThatDatabase()
    {
        await _sut.Consume(Ran("CREATE TABLE \"new_table\" (id INTEGER)", new QueryResult()));

        _database.Tables.ShouldBe([new TableInfo("", "new_table")]);
    }

    [Fact]
    public async Task FailedCreateTable_ChangesNothing()
    {
        await _sut.Consume(Ran("CREATE TABLE \"new_table\" (id INTEGER)", new QueryResult { Error = "table new_table already exists" }));

        await _connectionService.DidNotReceiveWithAnyArgs().GetTablesAsync(default!, default!, default);
    }

    [Fact]
    public async Task PlainSelect_ChangesNothing()
    {
        await _sut.Consume(Ran("SELECT * FROM products", new QueryResult()));

        await _connectionService.DidNotReceiveWithAnyArgs().GetTablesAsync(default!, default!, default);
    }

    [Fact]
    public async Task CreateTableInsideATransaction_WaitsForTheCommit()
    {
        // Arrange
        var transaction = new TransactionInfo();

        // Act
        await _sut.Consume(Ran("CREATE TABLE \"new_table\" (id INTEGER)", new QueryResult(), transaction));

        // Assert
        await NoTablesReloaded();
    }

    [Fact]
    public async Task CreateTableInsideATransaction_ReloadsTheTablesOnCommit()
    {
        // Arrange
        var transaction = new TransactionInfo();
        await _sut.Consume(Ran("CREATE TABLE \"new_table\" (id INTEGER)", new QueryResult(), transaction));

        // Act
        await _sut.Consume(Finished(transaction, committed: true));

        // Assert
        _database.Tables.ShouldBe([new TableInfo("", "new_table")]);
    }

    [Fact]
    public async Task CreateTableInsideATransaction_RolledBack_ChangesNothing()
    {
        // Arrange
        var transaction = new TransactionInfo();
        await _sut.Consume(Ran("CREATE TABLE \"new_table\" (id INTEGER)", new QueryResult(), transaction));

        // Act
        await _sut.Consume(Finished(transaction, committed: false));

        // Assert
        await NoTablesReloaded();
    }

    [Fact]
    public async Task CommittingATransactionWithoutSchemaChanges_ChangesNothing()
    {
        // Arrange
        var transaction = new TransactionInfo();
        await _sut.Consume(Ran("UPDATE products SET price = 1", new QueryResult { RowsAffected = 3 }, transaction));

        // Act
        await _sut.Consume(Finished(transaction, committed: true));

        // Assert
        await NoTablesReloaded();
    }

    [Fact]
    public async Task CommittingAnotherTransaction_LeavesThePendingChangeWaiting()
    {
        // Arrange
        var transaction = new TransactionInfo();
        await _sut.Consume(Ran("CREATE TABLE \"new_table\" (id INTEGER)", new QueryResult(), transaction));

        // Act
        await _sut.Consume(Finished(new TransactionInfo(), committed: true));

        // Assert
        await NoTablesReloaded();
    }

    [Theory]
    [InlineData(QueryResultKind.EstimatedPlan)]
    [InlineData(QueryResultKind.ActualPlan)]
    public async Task PlanOfAStatementThatChangesTheSchema_ChangesNothing(QueryResultKind kind)
    {
        // Act
        await _sut.Consume(Ran("CREATE TABLE \"new_table\" AS SELECT 1", new QueryResult(), kind: kind));

        // Assert
        await NoTablesReloaded();
    }
}
