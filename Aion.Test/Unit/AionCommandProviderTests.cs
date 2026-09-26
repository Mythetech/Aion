using Aion.Components.CommandPalette;
using Aion.Components.Connections;
using Aion.Components.NativeMenu;
using Aion.Components.Shortcuts;
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
        _provider = new AionCommandProvider(_bus, _connectionState, AionKeyBindings.ForDesktop(isMac: false));
    }

    [Fact]
    public async Task GetCommandsAsync_ReturnsExpectedStaticCommands()
    {
        var result = await _provider.GetCommandsAsync("", CancellationToken.None);

        result.Count.ShouldBe(16);
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

        result.Count.ShouldBe(17);
        result.ShouldContain(c => c.Id.StartsWith("panel.connection."));
    }

    [Fact]
    public async Task GetCommandsAsync_ClearHistory_PublishesClearHistory()
    {
        var result = await _provider.GetCommandsAsync("", CancellationToken.None);
        var clear = result.Single(c => c.Id == "action.clear-history");

        await clear.InvokeAsync(CancellationToken.None);

        await _bus.Received(1).PublishAsync(Arg.Any<ClearHistory>());
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

    private static async Task<string?> HintFor(AionCommandProvider provider, string id) =>
        (await provider.GetCommandsAsync("", CancellationToken.None)).Single(c => c.Id == id).Description;

    [Fact]
    public async Task Hints_OnDesktop_MatchTheNativeMenu()
    {
        // Arrange
        var provider = new AionCommandProvider(_bus, _connectionState, AionKeyBindings.ForDesktop(isMac: true));

        // Assert
        (await HintFor(provider, "action.run-query")).ShouldBe("⌘↵");
        (await HintFor(provider, "action.new-query")).ShouldBe("⌘N");
        (await HintFor(provider, "action.new-connection")).ShouldBe("⇧⌘N");
        (await HintFor(provider, "action.save-query")).ShouldBe("⌘S");
    }

    [Fact]
    public async Task Hints_InTheBrowser_LeaveOutShortcutsTheBrowserKeeps()
    {
        // Arrange
        var provider = new AionCommandProvider(_bus, _connectionState, AionKeyBindings.ForBrowser(isMac: false));

        // Assert
        (await HintFor(provider, "action.run-query")).ShouldBe("Ctrl+Enter");
        (await HintFor(provider, "action.new-query")).ShouldBeNull();
        (await HintFor(provider, "action.new-connection")).ShouldBeNull();
        (await HintFor(provider, "action.save-query")).ShouldBeNull();
    }
}
