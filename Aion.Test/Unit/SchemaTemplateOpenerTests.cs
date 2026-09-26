using Aion.Components.Connections;
using Aion.Components.Connections.Commands;
using Aion.Components.Connections.Consumers;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Shared.Snackbar.Commands;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Database.Dialects;
using Aion.Core.Database.PostgreSQL;
using Aion.Web.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit;

public class SchemaTemplateOpenerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly IDatabaseProviderFactory _factory = Substitute.For<IDatabaseProviderFactory>();
    private readonly ConnectionState _connections;
    private readonly QueryState _queries;
    private readonly SchemaTemplateOpener _sut;

    public SchemaTemplateOpenerTests()
    {
        _connections = new ConnectionState(Substitute.For<IConnectionService>(), _factory, _bus, NullLogger<ConnectionState>.Instance);
        _queries = new QueryState(_bus, Substitute.For<IQuerySaveService>());
        _sut = new SchemaTemplateOpener(_connections, _queries, _bus);
    }

    private ConnectionModel Server()
    {
        var provider = Substitute.For<IDatabaseProvider, ISqlDialectProvider, IDatabaseCreationProvider>();
        provider.DatabaseType.Returns(DatabaseType.PostgreSQL);
        provider.Commands.Returns(new PostgreSqlCommands());
        ((ISqlDialectProvider)provider).Dialect.Returns(PostgreSqlDialect.Instance);
        return Add(DatabaseType.PostgreSQL, provider, ["postgres", "shop"]);
    }

    private ConnectionModel InBrowserSqlite()
    {
        var provider = Substitute.For<IDatabaseProvider, ISqlDialectProvider, IManagedDatabaseProvider>();
        provider.DatabaseType.Returns(DatabaseType.WasmSQLite);
        provider.Commands.Returns(new SqliteWasmCommands());
        ((ISqlDialectProvider)provider).Dialect.Returns(SqliteDialect.Instance);
        return Add(DatabaseType.WasmSQLite, provider, ["shop"]);
    }

    private ConnectionModel Add(DatabaseType type, IDatabaseProvider provider, string[] databases)
    {
        _factory.GetProvider(type).Returns(provider);
        var connection = new ConnectionModel
        {
            Name = "local", Type = type, Active = true,
            Databases = databases.Select(name => new DatabaseModel { Name = name }).ToList()
        };
        _connections.Connections.Add(connection);
        return connection;
    }

    private List<object> Published() => _bus.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(IMessageBus.PublishAsync))
        .Select(c => c.GetArguments()[0]!)
        .ToList();

    [Fact]
    public async Task CreateTable_OpensTheEnginesTemplateOnThatDatabaseWithoutRunningIt()
    {
        var connection = InBrowserSqlite();

        await _sut.Consume(new OpenCreateTableTemplate(connection.Id, "shop"));

        var query = _queries.Active!;
        query.Query.ShouldBe(SqliteDialect.Instance.CreateTableTemplate());
        query.ConnectionId.ShouldBe(connection.Id);
        query.DatabaseName.ShouldBe("shop");
        Published().OfType<FocusQuery>().ShouldHaveSingleItem();
        Published().OfType<RunQuery>().ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateDatabase_OpensAPlainCreateDatabaseOnAnExistingDatabaseWithoutRunningIt()
    {
        var connection = Server();

        await _sut.Consume(new OpenCreateDatabaseTemplate(connection.Id));

        var query = _queries.Active!;
        query.Query.ShouldBe("CREATE DATABASE \"new_database\";");
        query.DatabaseName.ShouldBe("postgres");
        Published().OfType<RunQuery>().ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateDatabase_ForAnEngineWithOneDatabasePerConnection_IsRefused()
    {
        var connection = InBrowserSqlite();
        var tabs = _queries.Queries.Count;

        await _sut.Consume(new OpenCreateDatabaseTemplate(connection.Id));

        _queries.Queries.Count.ShouldBe(tabs);
        Published().OfType<AddNotification>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Capabilities_OfferAddDatabaseOnlyWhereTheServerCanCreateOne()
    {
        Server();
        InBrowserSqlite();

        _connections.SupportsCreatingDatabases(DatabaseType.PostgreSQL).ShouldBeTrue();
        _connections.SupportsCreatingDatabases(DatabaseType.WasmSQLite).ShouldBeFalse();
        _connections.SupportsCreatingTables(DatabaseType.WasmSQLite).ShouldBeTrue();
    }
}
