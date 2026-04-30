using Aion.Core.Database;
using Aion.Components.Connections;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Test.TestDoubles;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit;

public class ConnectionStateEditDeleteTests
{
    private readonly ConnectionState _sut;
    private readonly IConnectionService _connectionService;
    private readonly IMessageBus _messageBus;
    private readonly IDatabaseProviderFactory _providerFactory;

    public ConnectionStateEditDeleteTests()
    {
        _messageBus = Substitute.For<IMessageBus>();
        _providerFactory = Substitute.For<IDatabaseProviderFactory>();
        _connectionService = Substitute.For<IConnectionService>();
        var logger = Substitute.For<ILogger<ConnectionState>>();
        _sut = new ConnectionState(_connectionService, _providerFactory, _messageBus, logger);
    }

    private ConnectionModel CreateTestConnection(string name = "Test")
    {
        return new ConnectionModel
        {
            Name = name,
            ConnectionString = "Host=localhost;Port=5432;Username=test;Password=test",
            Type = DatabaseType.PostgreSQL,
            Active = true,
            SaveCredentials = true
        };
    }

    [Fact]
    public async Task RemoveConnection_RemovesFromList()
    {
        var connection = CreateTestConnection();
        _sut.Connections = [connection];

        await _sut.RemoveConnection(connection.Id);

        _sut.Connections.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveConnection_PersistsViaService()
    {
        var connection = CreateTestConnection();
        _sut.Connections = [connection];

        await _sut.RemoveConnection(connection.Id);

        await _connectionService.Received(1).RemoveConnection(connection.Id);
    }

    [Fact]
    public async Task RemoveConnection_FiresStateChanged()
    {
        var connection = CreateTestConnection();
        _sut.Connections = [connection];
        var stateChanged = false;
        _sut.ConnectionStateChanged += () => stateChanged = true;

        await _sut.RemoveConnection(connection.Id);

        stateChanged.ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveConnection_NonexistentId_DoesNotThrow()
    {
        _sut.Connections = [CreateTestConnection()];

        await Should.NotThrowAsync(() => _sut.RemoveConnection(Guid.NewGuid()));

        _sut.Connections.Count.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateConnection_UpdatesModelProperties()
    {
        var connection = CreateTestConnection("Original");
        _sut.Connections = [connection];
        var provider = Substitute.For<IDatabaseProvider>();
        provider.GetDatabasesAsync(Arg.Any<string>()).Returns(new List<string> { "testdb" });
        _providerFactory.GetProvider(DatabaseType.PostgreSQL).Returns(provider);

        var updated = new ConnectionModel
        {
            Name = "Updated",
            ConnectionString = "Host=newhost;Port=5432;Username=test;Password=test",
            Type = DatabaseType.PostgreSQL,
            SaveCredentials = true
        };

        await _sut.UpdateConnection(connection.Id, updated);

        var result = _sut.Connections.First();
        result.Name.ShouldBe("Updated");
        result.ConnectionString.ShouldBe("Host=newhost;Port=5432;Username=test;Password=test");
    }

    [Fact]
    public async Task UpdateConnection_ReconnectsWithNewSettings()
    {
        var connection = CreateTestConnection();
        _sut.Connections = [connection];
        var provider = Substitute.For<IDatabaseProvider>();
        provider.GetDatabasesAsync(Arg.Any<string>()).Returns(new List<string> { "db1" });
        _providerFactory.GetProvider(DatabaseType.PostgreSQL).Returns(provider);

        var updated = new ConnectionModel
        {
            Name = "Updated",
            ConnectionString = "Host=newhost;Port=5432;Username=test;Password=test",
            Type = DatabaseType.PostgreSQL,
            SaveCredentials = true
        };

        await _sut.UpdateConnection(connection.Id, updated);

        var result = _sut.Connections.First();
        result.Active.ShouldBeTrue();
        result.Databases.Count.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateConnection_PersistsViaService()
    {
        var connection = CreateTestConnection();
        _sut.Connections = [connection];
        var provider = Substitute.For<IDatabaseProvider>();
        provider.GetDatabasesAsync(Arg.Any<string>()).Returns(new List<string> { "testdb" });
        _providerFactory.GetProvider(DatabaseType.PostgreSQL).Returns(provider);

        var updated = new ConnectionModel
        {
            Name = "Updated",
            ConnectionString = "Host=newhost;Port=5432;Username=test;Password=test",
            Type = DatabaseType.PostgreSQL,
            SaveCredentials = true
        };

        await _sut.UpdateConnection(connection.Id, updated);

        await _connectionService.Received(1).UpdateConnection(Arg.Is<ConnectionModel>(c => c.Name == "Updated"));
    }

    [Fact]
    public async Task UpdateConnection_ReconnectFails_SetsInactive()
    {
        var connection = CreateTestConnection();
        _sut.Connections = [connection];
        var provider = Substitute.For<IDatabaseProvider>();
        provider.GetDatabasesAsync(Arg.Any<string>()).Returns(Task.FromResult<List<string>?>(null));
        _providerFactory.GetProvider(DatabaseType.PostgreSQL).Returns(provider);

        var updated = new ConnectionModel
        {
            Name = "Updated",
            ConnectionString = "Host=badhost;Port=5432;Username=test;Password=test",
            Type = DatabaseType.PostgreSQL,
            SaveCredentials = true
        };

        await _sut.UpdateConnection(connection.Id, updated);

        var result = _sut.Connections.First();
        result.Active.ShouldBeFalse();
    }
}
