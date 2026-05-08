using Aion.Components.CommandPalette;
using Aion.Components.Connections;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit;

public class AionCommandProviderTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly ConnectionState _connectionState;
    private readonly AionCommandProvider _provider;

    public AionCommandProviderTests()
    {
        var connectionService = Substitute.For<IConnectionService>();
        var providerFactory = Substitute.For<IDatabaseProviderFactory>();
        var logger = Substitute.For<ILogger<ConnectionState>>();
        _connectionState = new ConnectionState(connectionService, providerFactory, _bus, logger);
        _provider = new AionCommandProvider(_bus, _connectionState);
    }

    [Fact]
    public async Task GetCommandsAsync_ReturnsExpectedStaticCommands()
    {
        var result = await _provider.GetCommandsAsync("", CancellationToken.None);

        result.Count.ShouldBe(15);
    }

    [Fact]
    public async Task GetCommandsAsync_IncludesDynamicConnectionCommands()
    {
        _connectionState.Connections.Add(new ConnectionModel
        {
            Name = "TestDB",
            ConnectionString = "Server=localhost",
            Type = DatabaseType.PostgreSQL
        });

        var result = await _provider.GetCommandsAsync("", CancellationToken.None);

        result.Count.ShouldBe(16);
        result.ShouldContain(c => c.Id.StartsWith("panel.connection."));
    }

    [Fact]
    public async Task GetCommandsAsync_AllCommandsHaveUniqueIds()
    {
        var result = await _provider.GetCommandsAsync("", CancellationToken.None);

        result.Select(c => c.Id).Distinct().Count().ShouldBe(result.Count);
    }

    [Fact]
    public async Task GetCommandsAsync_AllCommandsHaveIcons()
    {
        var result = await _provider.GetCommandsAsync("", CancellationToken.None);

        result.ShouldAllBe(c => !string.IsNullOrEmpty(c.Icon));
    }

    [Fact]
    public async Task GetCommandsAsync_ContainsExpectedGroups()
    {
        var result = await _provider.GetCommandsAsync("", CancellationToken.None);
        var groups = result.Select(c => c.Group).Distinct().ToList();

        groups.ShouldContain("Panels");
        groups.ShouldContain("Actions");
        groups.ShouldContain("Export");
        groups.ShouldContain("Context");
        groups.ShouldContain("Settings");
    }
}
