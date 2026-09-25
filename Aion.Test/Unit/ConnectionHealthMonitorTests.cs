using Aion.Components.Connections;
using Aion.Components.Connections.Commands;
using Aion.Components.Connections.Consumers;
using Aion.Components.Connections.Services;
using Aion.Components.Settings.Domains;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure.MessageBus;
using Npgsql;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit;

public class ConnectionHealthMonitorTests
{
    private readonly IConnectionService _connectionService;
    private readonly ConnectionState _connectionState;
    private readonly ConnectionSettings _settings = new() { ConnectionTimeoutSeconds = 3 };
    private readonly ConnectionHealthMonitor _sut;

    public ConnectionHealthMonitorTests()
    {
        var messageBus = Substitute.For<IMessageBus>();
        _connectionService = Substitute.For<IConnectionService>();
        _connectionState = new ConnectionState(_connectionService, Substitute.For<IDatabaseProviderFactory>(), messageBus,
            NullLogger<ConnectionState>.Instance);
        _sut = new ConnectionHealthMonitor(_connectionState, messageBus, _settings,
            NullLogger<ConnectionHealthMonitor>.Instance);
    }

    private static ConnectionModel Connection(DatabaseType type, string name) => new()
    {
        Name = name,
        Type = type,
        ConnectionString = type == DatabaseType.PostgreSQL ? "Host=localhost;Username=postgres" : "Data Source=local.db",
        Active = true
    };

    private void ServerReturns(params string[] databases) =>
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>())
            .Returns(databases.ToList());

    [Fact]
    public async Task CheckConnectionsAsync_SkipsInProcessWasmEngines()
    {
        var sqlite = Connection(DatabaseType.WasmSQLite, "sqlite");
        var pglite = Connection(DatabaseType.WasmPostgreSQL, "pglite");
        var postgres = Connection(DatabaseType.PostgreSQL, "postgres");
        _connectionState.Connections = [sqlite, pglite, postgres];
        ServerReturns("app");

        await _sut.CheckConnectionsAsync(CancellationToken.None);

        await _connectionService.Received(1).GetDatabasesAsync(Arg.Any<string>(), DatabaseType.PostgreSQL);
        await _connectionService.DidNotReceive().GetDatabasesAsync(Arg.Any<string>(), DatabaseType.WasmSQLite);
        await _connectionService.DidNotReceive().GetDatabasesAsync(Arg.Any<string>(), DatabaseType.WasmPostgreSQL);
        sqlite.HealthStatus.ShouldBe(ConnectionHealthStatus.Unknown);
        pglite.HealthStatus.ShouldBe(ConnectionHealthStatus.Unknown);
        postgres.HealthStatus.ShouldBe(ConnectionHealthStatus.Healthy);
    }

    [Fact]
    public async Task RefreshAsync_AllConnections_SkipsInProcessWasmEngines()
    {
        _connectionState.Connections = [Connection(DatabaseType.WasmSQLite, "sqlite"), Connection(DatabaseType.PostgreSQL, "postgres")];
        ServerReturns("app");

        await _sut.RefreshAsync();

        await _connectionService.Received(1).GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>());
    }

    [Fact]
    public async Task CheckConnectionsAsync_Failure_MarksInactiveAndRaisesStateChanged()
    {
        var postgres = Connection(DatabaseType.PostgreSQL, "postgres");
        postgres.HealthStatus = ConnectionHealthStatus.Healthy;
        _connectionState.Connections = [postgres];
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>())
            .Returns<List<string>?>(_ => throw new InvalidOperationException("Connection refused"));
        var stateChanges = 0;
        _connectionState.ConnectionStateChanged += () => stateChanges++;

        await _sut.CheckConnectionsAsync(CancellationToken.None);

        postgres.Active.ShouldBeFalse();
        postgres.HealthStatus.ShouldBe(ConnectionHealthStatus.Unhealthy);
        postgres.LastError.ShouldBe("Connection refused");
        stateChanges.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task CheckConnectionsAsync_TimedOutCheck_ReportsTimeoutStatus()
    {
        var postgres = Connection(DatabaseType.PostgreSQL, "postgres");
        _connectionState.Connections = [postgres];
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>())
            .Returns<List<string>?>(_ => throw new InvalidOperationException("Exception while connecting",
                new TimeoutException("The operation has timed out.")));

        await _sut.CheckConnectionsAsync(CancellationToken.None);

        postgres.HealthStatus.ShouldBe(ConnectionHealthStatus.Timeout);
        postgres.Active.ShouldBeFalse();
    }

    [Fact]
    public async Task CheckConnectionHealthAsync_AppliesConnectionTimeoutSetting()
    {
        string? checkedConnectionString = null;
        _connectionService.GetDatabasesAsync(Arg.Do<string>(cs => checkedConnectionString = cs), Arg.Any<DatabaseType>())
            .Returns(new List<string> { "app" });

        await _sut.CheckConnectionHealthAsync(Connection(DatabaseType.PostgreSQL, "postgres"));

        new NpgsqlConnectionStringBuilder(checkedConnectionString).Timeout.ShouldBe(3);
    }

    [Fact]
    public async Task CheckConnectionsAsync_SkipsIdleHealthyConnection()
    {
        var idle = Connection(DatabaseType.PostgreSQL, "idle");
        idle.HealthStatus = ConnectionHealthStatus.Healthy;
        idle.LastActivityTime = DateTime.UtcNow - _settings.ActivityThreshold - TimeSpan.FromMinutes(1);
        _connectionState.Connections = [idle];

        await _sut.CheckConnectionsAsync(CancellationToken.None);

        await _connectionService.DidNotReceive().GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>());
    }

    [Fact]
    public void RecordActivity_UpdatesConnectionActivityTime()
    {
        var postgres = Connection(DatabaseType.PostgreSQL, "postgres");
        _connectionState.Connections = [postgres];

        _sut.RecordActivity(postgres.Id);

        postgres.LastActivityTime.ShouldNotBeNull();
    }

    [Fact]
    public async Task RefreshConnectionHealth_IsHandledByHealthMonitor()
    {
        var monitor = Substitute.For<IConnectionHealthMonitor>();
        var id = Guid.NewGuid();

        await new ConnectionHealthRefresher(monitor).Consume(new RefreshConnectionHealth(id));

        await monitor.Received(1).RefreshAsync(id);
    }
}
