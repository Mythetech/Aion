using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Querying.Consumers;
using Aion.Components.Querying.Events;
using Aion.Components.Querying.Messages;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.Querying;

public class QueryMessageRecorderTests
{
    private readonly QueryMessageLog _log = new();
    private readonly IDatabaseProvider _provider = Substitute.For<IDatabaseProvider>();
    private readonly ConnectionModel _connection = new() { Name = "Test", Type = DatabaseType.PostgreSQL, ConnectionString = "Host=localhost" };
    private readonly ConnectionState _connections;
    private readonly QueryModel _query;

    public QueryMessageRecorderTests()
    {
        _provider.UpdateConnectionString(Arg.Any<string>(), Arg.Any<string>()).Returns("db-connection");
        _provider.BeginTransactionAsync(Arg.Any<string>()).Returns(_ => new TransactionInfo());
        _provider.ExecuteInTransactionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResult { RowsAffected = 2 });
        var factory = Substitute.For<IDatabaseProviderFactory>();
        factory.GetProvider(DatabaseType.PostgreSQL).Returns(_provider);

        var services = new ServiceCollection();
        services.AddSingleton(_log);
        services.AddTransient<QueryMessageRecorder>();
        var bus = new InMemoryMessageBus(services.BuildServiceProvider(), new NullLogger<InMemoryMessageBus>(), [], []);
        bus.RegisterConsumerType<TransactionStarted, QueryMessageRecorder>();
        bus.RegisterConsumerType<QueryExecuted, QueryMessageRecorder>();
        bus.RegisterConsumerType<TransactionFinished, QueryMessageRecorder>();

        _connections = new ConnectionState(Substitute.For<IConnectionService>(), factory, bus, NullLogger<ConnectionState>.Instance)
        {
            Connections = [_connection]
        };

        _query = new QueryModel
        {
            Name = "Query1",
            Query = "UPDATE accounts SET balance = 0 WHERE id IN (1, 2)",
            ConnectionId = _connection.Id,
            DatabaseName = "db",
            UseTransaction = true
        };
    }

    [Fact]
    public async Task RunAndCommit_LogsTheWholeTransactionInTheTab()
    {
        // Act
        await _connections.ExecuteQueryAsync(_query, CancellationToken.None);
        await _connections.CommitTransactionAsync(_query);

        // Assert
        _log.For(_query.Id).Select(m => m.Text).ShouldBe(
        [
            "BEGIN",
            "UPDATE accounts SET balance = 0 WHERE id IN (1, 2)",
            "2 rows affected",
            "COMMIT"
        ]);
    }

    [Fact]
    public async Task Rollback_IsLogged()
    {
        // Arrange
        await _connections.ExecuteQueryAsync(_query, CancellationToken.None);

        // Act
        await _connections.RollbackTransactionAsync(_query);

        // Assert
        _log.For(_query.Id).Last().Text.ShouldBe("ROLLBACK");
    }

    [Fact]
    public async Task ClosingTheTab_DropsItsLog()
    {
        // Arrange
        await _connections.ExecuteQueryAsync(_query, CancellationToken.None);

        // Act
        await new QueryMessageRecorder(_log).Consume(new DeleteQuery(_query));

        // Assert
        _log.For(_query.Id).ShouldBeEmpty();
    }
}
