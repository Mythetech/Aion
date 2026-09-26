using Aion.Components.Connections;
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

public class ConnectionStateExecuteTests
{
    private const string WholeText = "SELECT 1;\nSELECT name FROM products";
    private const string Selection = "SELECT name FROM products";

    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly IDatabaseProvider _provider = Substitute.For<IDatabaseProvider>();
    private readonly ConnectionState _sut;
    private readonly QueryModel _query;

    public ConnectionStateExecuteTests()
    {
        var factory = Substitute.For<IDatabaseProviderFactory>();
        factory.GetProvider(DatabaseType.PostgreSQL).Returns(_provider);
        _provider.UpdateConnectionString(Arg.Any<string>(), Arg.Any<string>()).Returns("db");

        var connection = new ConnectionModel { Name = "Test", ConnectionString = "Host=localhost", Type = DatabaseType.PostgreSQL };
        _sut = new ConnectionState(Substitute.For<IConnectionService>(), factory, _bus, NullLogger<ConnectionState>.Instance)
        {
            Connections = [connection]
        };
        _query = new QueryModel { Query = WholeText, ConnectionId = connection.Id, DatabaseName = "db" };
    }

    [Fact]
    public async Task ExecutingASelection_RunsOnlyItAndLeavesTheTabsTextAlone()
    {
        // Arrange
        _provider.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new QueryResult());

        // Act
        await _sut.ExecuteQueryAsync(_query, Selection, CancellationToken.None);

        // Assert
        await _provider.Received(1).ExecuteQueryAsync("db", Selection, Arg.Any<CancellationToken>());
        _query.Query.ShouldBe(WholeText);
        _query.ExecutedSql.ShouldBe(Selection);
        await _bus.Received(1).PublishAsync(Arg.Is<QueryExecuted>(m => m.ExecutedSql == Selection));
    }

    [Fact]
    public async Task ExecutingASelection_LocatesATextOnlyErrorInTheSelection()
    {
        // Arrange
        _provider.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResult { Error = "no such column: name" });

        // Act
        var result = await _sut.ExecuteQueryAsync(_query, Selection, CancellationToken.None);

        // Assert
        result.ErrorDetail!.Line.ShouldBe(1);
        result.ErrorDetail.Column.ShouldBe(8);
    }
}
