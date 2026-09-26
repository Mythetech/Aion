using Aion.Components.Connections;
using Aion.Components.ForeignKeys;
using Aion.Components.ForeignKeys.Commands;
using Aion.Components.ForeignKeys.Consumers;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.RequestContextPanel;
using Aion.Components.Shared.Snackbar.Commands;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Database.Dialects;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.ForeignKeys;

public class ForeignKeyRowsOpenerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly ConnectionState _connections;
    private readonly QueryState _queries;
    private readonly ForeignKeyRowsOpener _sut;

    public ForeignKeyRowsOpenerTests()
    {
        var provider = Substitute.For<IDatabaseProvider, ISqlDialectProvider>();
        provider.DatabaseType.Returns(DatabaseType.WasmSQLite);
        ((ISqlDialectProvider)provider).Dialect.Returns(SqliteDialect.Instance);
        var factory = Substitute.For<IDatabaseProviderFactory>();
        factory.GetProvider(DatabaseType.WasmSQLite).Returns(provider);

        _connections = new ConnectionState(Substitute.For<IConnectionService>(), factory, _bus, NullLogger<ConnectionState>.Instance);
        _queries = new QueryState(_bus, Substitute.For<IQuerySaveService>());
        _sut = new ForeignKeyRowsOpener(new ForeignKeyService(_connections), _queries, _bus);
    }

    private List<object> Published() => _bus.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(IMessageBus.PublishAsync))
        .Select(c => c.GetArguments()[0]!)
        .ToList();

    private ConnectionModel Connection()
    {
        var connection = new ConnectionModel { Name = "shop", Type = DatabaseType.WasmSQLite, ConnectionString = "shop", Active = true };
        _connections.Connections.Add(connection);
        return connection;
    }

    [Fact]
    public async Task OpensTheLookupInANewTabOnTheSameDatabaseAndRunsIt()
    {
        var connection = Connection();
        var detail = new ForeignKeyDetail("Orders", "customer_id", "customers", "id", 7L, connection.Id, "shop");

        await _sut.Consume(new OpenForeignKeyRows(detail));

        var query = _queries.Active!;
        query.ConnectionId.ShouldBe(connection.Id);
        query.DatabaseName.ShouldBe("shop");
        query.Query.ShouldBe("SELECT * FROM \"customers\"\nWHERE \"id\" = 7;");
        Published().OfType<FocusQuery>().ShouldHaveSingleItem().Query.ShouldBeSameAs(query);
        Published().OfType<RunQuery>().ShouldHaveSingleItem();
        var published = Published();
        published.FindIndex(m => m is FocusQuery).ShouldBeLessThan(published.FindIndex(m => m is RunQuery));
    }

    [Fact]
    public async Task ConnectionThatIsGone_SaysSoAndOpensNothing()
    {
        var tabs = _queries.Queries.Count;
        var detail = new ForeignKeyDetail("Orders", "customer_id", "customers", "id", 7L, Guid.NewGuid(), "shop");

        await _sut.Consume(new OpenForeignKeyRows(detail));

        _queries.Queries.Count.ShouldBe(tabs);
        Published().OfType<AddNotification>().ShouldHaveSingleItem().Severity.ShouldBe(Severity.Warning);
        Published().OfType<RunQuery>().ShouldBeEmpty();
    }
}
