using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Desktop;
using Aion.Desktop.Services;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.Storage;

public class DesktopConnectionServiceTests
{
    private readonly IConnectionStorage _storage = Substitute.For<IConnectionStorage>();

    private ConnectionService CreateService() => new(Substitute.For<IDatabaseProviderFactory>(), _storage);

    [Fact]
    public async Task AddConnection_SavesItWhetherOrNotThePasswordIsStored()
    {
        var service = CreateService();
        var connection = new ConnectionModel
        {
            Name = "Asked", Type = DatabaseType.PostgreSQL, ConnectionString = "Host=db;Username=app;Password=hunter2"
        };

        await service.AddConnection(connection);

        await _storage.Received(1).SaveConnectionsAsync(Arg.Is<IEnumerable<ConnectionModel>>(c => c.Contains(connection)));
    }

    // Saved strings have no password, so two connections to one server as one user share a connection string.
    [Fact]
    public async Task InitializeAsync_KeepsConnectionsThatShareAConnectionString()
    {
        var first = new ConnectionModel { Name = "Reporting", Type = DatabaseType.PostgreSQL, ConnectionString = "Host=db;Username=app" };
        var second = new ConnectionModel { Name = "Admin", Type = DatabaseType.PostgreSQL, ConnectionString = "Host=db;Username=app" };
        _storage.LoadConnectionsAsync().Returns([first, second]);
        var service = CreateService();

        await service.InitializeAsync();
        await service.InitializeAsync();

        (await service.GetSavedConnections()).ShouldBe([first, second]);
    }
}
