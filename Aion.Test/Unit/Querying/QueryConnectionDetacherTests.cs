using Aion.Components.Connections;
using Aion.Components.Connections.Events;
using Aion.Components.Querying;
using Aion.Components.Querying.Consumers;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.Querying;

public class QueryConnectionDetacherTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly QueryState _queryState;

    public QueryConnectionDetacherTests()
    {
        _queryState = new QueryState(_bus, Substitute.For<IQuerySaveService>());
    }

    [Fact]
    public async Task RemovingAConnection_AnnouncesItsRemoval()
    {
        var connection = new ConnectionModel { Name = "sales", Type = DatabaseType.WasmSQLite };
        var connectionState = new ConnectionState(
            Substitute.For<IConnectionService>(),
            Substitute.For<IDatabaseProviderFactory>(),
            _bus,
            Substitute.For<ILogger<ConnectionState>>());
        connectionState.Connections = [connection];

        await connectionState.RemoveConnection(connection.Id);

        await _bus.Received(1).PublishAsync(Arg.Is<ConnectionRemoved>(e => e.ConnectionId == connection.Id));
    }

    [Fact]
    public async Task ConnectionRemoved_DetachesItsQueryTabsAndKeepsTheirText()
    {
        var removedId = Guid.NewGuid();
        var keptId = Guid.NewGuid();
        var orphaned = _queryState.AddQuery("Orphaned");
        orphaned.ConnectionId = removedId;
        orphaned.DatabaseName = "sales";
        orphaned.Query = "SELECT * FROM orders";
        var untouched = _queryState.AddQuery("Untouched");
        untouched.ConnectionId = keptId;
        untouched.DatabaseName = "inventory";

        await new QueryConnectionDetacher(_queryState).Consume(new ConnectionRemoved(removedId));

        _queryState.Queries.ShouldContain(orphaned);
        orphaned.ConnectionId.ShouldBeNull();
        orphaned.DatabaseName.ShouldBeNull();
        orphaned.Query.ShouldBe("SELECT * FROM orders");
        untouched.ConnectionId.ShouldBe(keptId);
        untouched.DatabaseName.ShouldBe("inventory");
    }

    [Fact]
    public void DetachConnection_NotifiesOnlyWhenATabChanged()
    {
        var notifications = 0;
        _queryState.StateChanged += () => notifications++;

        _queryState.DetachConnection(Guid.NewGuid());

        notifications.ShouldBe(0);
    }
}
