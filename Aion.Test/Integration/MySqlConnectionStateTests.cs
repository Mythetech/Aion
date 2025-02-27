using Aion.Core.Database;
using Microsoft.Extensions.Logging;
using Testcontainers.MySql;

namespace Aion.Test.Integration;

public class MySqlConnectionStateTests : ConnectionStateTestBase
{
    private readonly MySqlContainer _container;
    
    protected override IDatabaseProvider Provider => new MySqlProvider(LoggerFactory.Create((builder) => { }).CreateLogger<MySqlProvider>());
    protected override string ConnectionString => _container.GetConnectionString();

    public MySqlConnectionStateTests() 
    {
        _container = new MySqlBuilder()
            .WithImage("mysql:8.0")
            .WithUsername("root")
            .WithPassword("test")
            .WithEnvironment("MYSQL_ROOT_PASSWORD", "test")
            .WithPortBinding(3306, true)
            .WithAutoRemove(true)
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