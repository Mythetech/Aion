using Aion.Core.Database;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Aion.Test.Integration;

public class PostgresConnectionStateTests : ConnectionStateTestBase
{
    private readonly PostgreSqlContainer _container;
    
    protected override IDatabaseProvider Provider => new PostgreSqlProvider();
    protected override string ConnectionString => _container.GetConnectionString();

    public PostgresConnectionStateTests() 
    {
        _container = new PostgreSqlBuilder()
            .Build();
    }

    public override async Task InitializeAsync()
    {
        await _container.StartAsync();
        await SetupTestDatabase();

    }

    [Fact]
    public async Task ConnectAsync_ListsPostgresDatabase()
    {
        var connection = new ConnectionModel
        {
            Type = Provider.DatabaseType,
            ConnectionString = ConnectionString,
            Name = "Postgres"
        };

        var result = await ConnectionState.ConnectAsync(connection);

        result.Success.ShouldBeTrue(result.Error);
        connection.Databases.ShouldContain(d => d.Name == "postgres");
    }

    public override async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}