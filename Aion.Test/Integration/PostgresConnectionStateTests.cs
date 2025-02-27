using Aion.Core.Database;
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

    public override async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}