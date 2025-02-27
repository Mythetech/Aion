using Aion.Core.Database;
using Aion.Core.Database.SqlServer;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;

namespace Aion.Test.Integration;

public class SqlServerConnectionStateTests : ConnectionStateTestBase
{
    private readonly MsSqlContainer _container;
    
    protected override IDatabaseProvider Provider => new SqlServerProvider(LoggerFactory.Create(builder => { }).CreateLogger<SqlServerProvider>());
    protected override string ConnectionString => _container.GetConnectionString() + ";TrustServerCertificate=true";

    public SqlServerConnectionStateTests() 
    {
        _container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("Strong_Password_123!")
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithPortBinding(1433, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
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