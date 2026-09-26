using Aion.Components.Connections;
using Aion.Components.Shared.Snackbar.Commands;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit;

public class ConnectionStateConnectTests
{
    private const string ConnectionString = "Host=localhost;Port=5432;Username=postgres;Password=wrong";

    private readonly ConnectionState _sut;
    private readonly IConnectionService _connectionService;
    private readonly IMessageBus _messageBus;

    public ConnectionStateConnectTests()
    {
        _messageBus = Substitute.For<IMessageBus>();
        _connectionService = Substitute.For<IConnectionService>();
        _sut = new ConnectionState(_connectionService, Substitute.For<IDatabaseProviderFactory>(), _messageBus,
            NullLogger<ConnectionState>.Instance);
    }

    private static ConnectionModel CreateConnection(string name = "Local") => new()
    {
        Name = name,
        ConnectionString = ConnectionString,
        Type = DatabaseType.PostgreSQL,
        SaveCredentials = true
    };

    private void ServerRejects(string message) =>
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>())
            .Returns<List<string>?>(_ => throw new InvalidOperationException(message));

    private void ServerReturns(params string[] databases) =>
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>())
            .Returns(databases.ToList());

    [Fact]
    public async Task ConnectAsync_ProviderThrows_ReturnsFailureWithDriverMessage()
    {
        ServerRejects("28P01: password authentication failed for user \"postgres\"");

        var result = await _sut.ConnectAsync(CreateConnection());

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("28P01: password authentication failed for user \"postgres\"");
    }

    [Fact]
    public async Task ConnectAsync_ProviderThrows_SavesNothingAndDoesNotAnnounce()
    {
        ServerRejects("Access denied for user 'root'");
        var connection = CreateConnection();

        await _sut.ConnectAsync(connection);

        connection.Active.ShouldBeFalse();
        _sut.Connections.ShouldBeEmpty();
        await _connectionService.DidNotReceive().AddConnection(Arg.Any<ConnectionModel>());
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<AddNotification>());
    }

    [Fact]
    public async Task ConnectAsync_ProviderReturnsNull_IsReportedAsFailure()
    {
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>())
            .Returns(Task.FromResult<List<string>?>(null));

        var result = await _sut.ConnectAsync(CreateConnection());

        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrWhiteSpace();
        await _connectionService.DidNotReceive().AddConnection(Arg.Any<ConnectionModel>());
    }

    [Fact]
    public async Task ConnectAsync_Success_MarksActiveSavesAndAnnounces()
    {
        ServerReturns("postgres", "app");
        var connection = CreateConnection();

        var result = await _sut.ConnectAsync(connection);

        result.Success.ShouldBeTrue();
        connection.Active.ShouldBeTrue();
        connection.HealthStatus.ShouldBe(ConnectionHealthStatus.Healthy);
        connection.Databases.Select(d => d.Name).ShouldBe(["postgres", "app"]);
        _sut.Connections.ShouldContain(connection);
        await _connectionService.Received(1).AddConnection(connection);
        await _messageBus.Received(1).PublishAsync(Arg.Is<AddNotification>(n => n.Severity == Severity.Success));
    }

    [Fact]
    public async Task ConnectAsync_InnerExceptionReason_IsIncludedInError()
    {
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>())
            .Returns<List<string>?>(_ => throw new InvalidOperationException(
                "Failed to connect to 127.0.0.1:5432", new Exception("Connection refused")));

        var result = await _sut.ConnectAsync(CreateConnection());

        result.Error.ShouldBe("Failed to connect to 127.0.0.1:5432 (Connection refused)");
    }

    [Fact]
    public async Task TestConnectionAsync_DoesNotChangeState()
    {
        ServerReturns("app");

        var result = await _sut.TestConnectionAsync(ConnectionString, DatabaseType.PostgreSQL);

        result.Success.ShouldBeTrue();
        result.Databases.ShouldBe(["app"]);
        _sut.Connections.ShouldBeEmpty();
        await _connectionService.DidNotReceive().AddConnection(Arg.Any<ConnectionModel>());
    }

    [Fact]
    public async Task TestConnectionAsync_Timeout_IsFlagged()
    {
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>())
            .Returns<List<string>?>(_ => throw new InvalidOperationException(
                "Exception while connecting", new TimeoutException("The operation has timed out.")));

        var result = await _sut.TestConnectionAsync(ConnectionString, DatabaseType.PostgreSQL);

        result.Success.ShouldBeFalse();
        result.TimedOut.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshDatabaseAsync_Failure_MarksUnhealthyAndKeepsError()
    {
        ServerRejects("Unable to connect to any of the specified MySQL hosts.");
        var connection = CreateConnection();
        connection.Active = true;
        connection.HealthStatus = ConnectionHealthStatus.Healthy;

        var result = await _sut.RefreshDatabaseAsync(connection);

        result.Success.ShouldBeFalse();
        connection.Active.ShouldBeFalse();
        connection.HealthStatus.ShouldBe(ConnectionHealthStatus.Unhealthy);
        connection.LastError.ShouldBe("Unable to connect to any of the specified MySQL hosts.");
    }

    [Fact]
    public async Task InitializeAsync_FailedStartupRefresh_MarksConnectionNotActive()
    {
        var saved = CreateConnection();
        _connectionService.GetSavedConnections().Returns([saved]);
        ServerRejects("No such host is known");

        await _sut.InitializeAsync();

        saved.Active.ShouldBeFalse();
        saved.HealthStatus.ShouldBe(ConnectionHealthStatus.Unhealthy);
        saved.LastError.ShouldBe("No such host is known");
    }

    [Fact]
    public async Task InitializeAsync_RefreshesSavedConnectionsConcurrently()
    {
        var first = CreateConnection("First");
        var second = CreateConnection("Second");
        second.ConnectionString = "Host=other;Username=postgres";
        _connectionService.GetSavedConnections().Returns([first, second]);

        var bothStarted = new TaskCompletionSource();
        var release = new TaskCompletionSource<List<string>?>();
        var started = 0;
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref started) == 2)
                    bothStarted.SetResult();
                return release.Task;
            });

        var initialize = _sut.InitializeAsync();

        await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        release.SetResult(["app"]);
        await initialize;

        first.Active.ShouldBeTrue();
        second.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task RenameConnection_SavesTheNewNameWithoutReconnecting()
    {
        var connection = CreateConnection("Local");
        _sut.Connections.Add(connection);

        await _sut.RenameConnectionAsync(connection.Id, "  Reporting  ");

        connection.Name.ShouldBe("Reporting");
        await _connectionService.Received(1).UpdateConnection(connection);
        await _connectionService.DidNotReceiveWithAnyArgs().GetDatabasesAsync(default!, default);
    }

    [Fact]
    public async Task RenameConnection_ToABlankName_ChangesNothing()
    {
        var connection = CreateConnection("Local");
        _sut.Connections.Add(connection);

        await _sut.RenameConnectionAsync(connection.Id, "   ");

        connection.Name.ShouldBe("Local");
        await _connectionService.DidNotReceiveWithAnyArgs().UpdateConnection(default!);
    }
}
