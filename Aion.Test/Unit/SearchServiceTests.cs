using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Components.Search;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Core.Database;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit;

public class SearchServiceTests
{
    private readonly IConnectionService _connectionService = Substitute.For<IConnectionService>();
    private readonly ConnectionState _connections;
    private readonly QueryState _queries;
    private readonly SearchService _sut;

    public SearchServiceTests()
    {
        var bus = Substitute.For<IMessageBus>();
        var providerFactory = Substitute.For<IDatabaseProviderFactory>();
        _connections = new ConnectionState(_connectionService, providerFactory, bus, Substitute.For<ILogger<ConnectionState>>());
        _queries = new QueryState(bus, Substitute.For<IQuerySaveService>());
        _sut = new SearchService(_connections, _queries, Substitute.For<ILogger<SearchService>>(), bus);
    }

    private async Task<List<SearchModel>> SearchAsync(string value, CancellationToken token = default)
    {
        List<SearchModel> results = [];
        await foreach (var result in _sut.SearchAsync(value, token))
            results.Add(result);
        return results;
    }

    [Fact]
    public async Task ConnectionResult_DoesNotExposeConnectionString()
    {
        _connections.Connections =
        [
            new ConnectionModel
            {
                Name = "Production",
                Type = DatabaseType.PostgreSQL,
                ConnectionString = "Host=db.example.com;Port=5432;Username=admin;Password=hunter2"
            }
        ];

        var result = (await SearchAsync("prod")).ShouldHaveSingleItem();

        result.Kind.ShouldBe(ResultKind.Connection);
        result.Description.ShouldBe("PostgreSQL on db.example.com");
        result.Description.ShouldNotContain("hunter2");
        result.Description.ShouldNotContain("admin");
        result.Description.ShouldNotContain("Password");
    }

    [Theory]
    [InlineData(DatabaseType.SQLServer, "Server=sql01,1433;User Id=sa;Password=secret", "SQL Server on sql01")]
    [InlineData(DatabaseType.SQLServer, "Server=tcp:sql01.example.com,1433;Password=secret", "SQL Server on sql01.example.com")]
    [InlineData(DatabaseType.MySQL, "Server=mysql.local;Port=3306;User=root;Password=secret", "MySQL on mysql.local")]
    [InlineData(DatabaseType.LiteDB, "Filename=/Users/me/data/app.db;Password=secret", "LiteDB on app.db")]
    [InlineData(DatabaseType.WasmPostgreSQL, "pglite://sample_store", "PostgreSQL (PGlite)")]
    [InlineData(DatabaseType.WasmSQLite, "Data Source=sample_store;Mode=Memory;Cache=Shared", "SQLite (In-Browser)")]
    [InlineData(DatabaseType.PostgreSQL, "not a connection string", "PostgreSQL")]
    public async Task ConnectionResult_DescribesEngineAndHostOnly(DatabaseType type, string connectionString, string expected)
    {
        _connections.Connections = [new ConnectionModel { Name = "Target", Type = type, ConnectionString = connectionString }];

        var result = (await SearchAsync("target")).ShouldHaveSingleItem();

        result.Description.ShouldBe(expected);
        result.Description.ShouldNotContain("secret");
    }

    [Fact]
    public async Task QueryResult_ShowsConnectionNameInsteadOfId()
    {
        var connection = new ConnectionModel { Name = "Analytics", Type = DatabaseType.PostgreSQL, ConnectionString = "Host=h" };
        _connections.Connections = [connection];
        var query = _queries.AddQuery("Monthly revenue");
        query.ConnectionId = connection.Id;
        query.DatabaseName = "warehouse";

        var result = (await SearchAsync("monthly")).ShouldHaveSingleItem();

        result.Kind.ShouldBe(ResultKind.Query);
        result.Description.ShouldBe("Connection: Analytics > Database: warehouse");
        result.Description.ShouldNotContain(connection.Id.ToString());
    }

    [Fact]
    public async Task QueryResult_WithoutConnection_SaysSo()
    {
        _queries.AddQuery("Scratch");

        var result = (await SearchAsync("scratch")).ShouldHaveSingleItem();

        result.Description.ShouldBe("No connection");
    }

    [Fact]
    public async Task Search_DoesNotLoadTablesForInactiveConnection()
    {
        _connections.Connections =
        [
            new ConnectionModel
            {
                Name = "Offline",
                Type = DatabaseType.PostgreSQL,
                ConnectionString = "Host=h",
                Active = false,
                Databases = [new DatabaseModel { Name = "app" }]
            }
        ];

        await SearchAsync("orders");

        await _connectionService.DidNotReceiveWithAnyArgs().GetTablesAsync(default!, default!, default);
    }

    [Fact]
    public async Task Search_StopsLoadingTablesOnceCancelled()
    {
        using var cts = new CancellationTokenSource();
        _connections.Connections =
        [
            new ConnectionModel
            {
                Name = "Primary",
                Type = DatabaseType.PostgreSQL,
                ConnectionString = "Host=h",
                Active = true,
                Databases = [new DatabaseModel { Name = "one" }, new DatabaseModel { Name = "two" }]
            }
        ];
        _connectionService.GetTablesAsync(default!, default!, default).ReturnsForAnyArgs(_ =>
        {
            cts.Cancel();
            return Task.FromResult(new List<TableInfo>());
        });

        await SearchAsync("orders", cts.Token);

        await _connectionService.ReceivedWithAnyArgs(1).GetTablesAsync(default!, default!, default);
    }
}
