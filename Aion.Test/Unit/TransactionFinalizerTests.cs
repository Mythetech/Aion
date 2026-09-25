using Aion.Components.Connections;
using Aion.Components.Connections.Consumers;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit;

public class TransactionFinalizerTests
{
    private readonly IDatabaseProvider _provider = Substitute.For<IDatabaseProvider>();
    private readonly ConnectionModel _connection = new() { Name = "Test", Type = DatabaseType.PostgreSQL, ConnectionString = "Host=localhost" };
    private readonly TransactionFinalizer _sut;

    public TransactionFinalizerTests()
    {
        _provider.UpdateConnectionString(Arg.Any<string>(), Arg.Any<string>()).Returns("db-connection");
        var factory = Substitute.For<IDatabaseProviderFactory>();
        factory.GetProvider(DatabaseType.PostgreSQL).Returns(_provider);

        var connections = new ConnectionState(Substitute.For<IConnectionService>(), factory, Substitute.For<IMessageBus>(),
            NullLogger<ConnectionState>.Instance)
        {
            Connections = [_connection]
        };

        _sut = new TransactionFinalizer(connections);
    }

    [Fact]
    public async Task ClosingTab_WithOpenTransaction_RollsItBackAndReleasesIt()
    {
        // Arrange
        var transaction = new TransactionInfo();
        var query = new QueryModel { Name = "Query1", ConnectionId = _connection.Id, DatabaseName = "db", Transaction = transaction };

        // Act
        await _sut.Consume(new DeleteQuery(query));

        // Assert
        await _provider.Received(1).RollbackTransactionAsync("db-connection", transaction.Id);
        query.Transaction.ShouldBeNull();
    }

    [Fact]
    public async Task ClosingTab_WithoutTransaction_DoesNotTouchProvider()
    {
        // Arrange
        var query = new QueryModel { Name = "Query1", ConnectionId = _connection.Id, DatabaseName = "db" };

        // Act
        await _sut.Consume(new DeleteQuery(query));

        // Assert
        await _provider.DidNotReceive().RollbackTransactionAsync(Arg.Any<string>(), Arg.Any<string>());
    }
}
