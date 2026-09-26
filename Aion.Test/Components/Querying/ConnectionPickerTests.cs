using Aion.Components.Connections;
using Aion.Components.Connections.Commands;
using Aion.Components.Querying;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.Querying;

public class ConnectionPickerTests : TestContext
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly QueryState _state;
    private readonly ConnectionState _connections;
    private readonly QueryModel _query;
    private readonly IRenderedComponent<MudPopoverProvider> _popovers;

    private readonly ConnectionModel _sample = new()
    {
        Name = "sample_store",
        Type = DatabaseType.WasmSQLite,
        ConnectionString = "Data Source=sample_store.db",
        Databases = [new DatabaseModel { Name = "sample_store" }]
    };

    private readonly ConnectionModel _prod = new()
    {
        Name = "prod-db",
        Type = DatabaseType.PostgreSQL,
        ConnectionString = "Host=db.internal;Username=app",
        Databases = [new DatabaseModel { Name = "orders" }, new DatabaseModel { Name = "analytics" }]
    };

    public ConnectionPickerTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        _state = new QueryState(_bus, Substitute.For<IQuerySaveService>());
        _connections = new ConnectionState(Substitute.For<IConnectionService>(), Substitute.For<IDatabaseProviderFactory>(), _bus, new NullLogger<ConnectionState>())
        {
            Connections = [_sample, _prod]
        };
        Services.AddSingleton(_bus);
        Services.AddSingleton(_state);
        Services.AddSingleton(_connections);

        _query = _state.Queries[0];
        _popovers = RenderComponent<MudPopoverProvider>();
    }

    private IRenderedComponent<ConnectionPicker> RenderPicker(bool disabled = false) =>
        RenderComponent<ConnectionPicker>(p => p
            .Add(x => x.Query, _query)
            .Add(x => x.Disabled, disabled)
            .Add(x => x.DisabledReason, "Commit or roll back the open transaction to change this"));

    private static async Task OpenAsync(IRenderedComponent<ConnectionPicker> cut) =>
        await cut.Find(".connection-picker-trigger").ClickAsync(new MouseEventArgs());

    private IElement? Option(string text) =>
        _popovers.FindAll(".mud-menu-item").FirstOrDefault(i => i.QuerySelector(".picker-option-name")?.TextContent == text);

    [Fact]
    public void WithoutAConnection_AsksForOne()
    {
        // Act
        var cut = RenderPicker();

        // Assert
        cut.Find(".connection-picker-trigger").TextContent.ShouldContain("Choose a connection");
        cut.FindAll(".picker-engine").ShouldBeEmpty();
    }

    [Fact]
    public void ConnectionWithOneDatabase_ShowsItsNameAndEngine()
    {
        // Arrange
        _state.UpdateQueryConnection(_query, _sample);

        // Act
        var cut = RenderPicker();

        // Assert
        cut.Find(".picker-connection").TextContent.ShouldBe("sample_store");
        cut.Find(".picker-engine").TextContent.ShouldBe("SQLite");
        cut.FindAll(".picker-database").ShouldBeEmpty();
    }

    [Fact]
    public void ConnectionWithSeveralDatabases_ShowsTheDatabaseToo()
    {
        // Arrange
        _state.UpdateQueryConnection(_query, _prod, "orders");

        // Act
        var cut = RenderPicker();

        // Assert
        cut.Find(".picker-connection").TextContent.ShouldBe("prod-db");
        cut.Find(".picker-database").TextContent.ShouldBe("orders");
        cut.Find(".picker-engine").TextContent.ShouldBe("PostgreSQL");
    }

    [Fact]
    public void ConnectionWithSeveralDatabasesAndNoneChosen_AsksForOne()
    {
        // Arrange
        _state.UpdateQueryConnection(_query, _prod);

        // Act
        var cut = RenderPicker();

        // Assert
        cut.Find(".picker-database").TextContent.ShouldBe("Choose a database");
    }

    [Fact]
    public void Tooltip_GivesTheFullNamesAndServer()
    {
        // Arrange
        _state.UpdateQueryConnection(_query, _prod, "orders");

        // Act
        var cut = RenderPicker();

        // Assert
        var tooltip = cut.FindComponent<MudTooltip>().Instance.Text;
        tooltip.ShouldContain("prod-db › orders");
        tooltip.ShouldContain("PostgreSQL on db.internal");
    }

    [Fact]
    public async Task Menu_ListsEachConnectionsDatabasesUnderIt()
    {
        // Arrange
        var cut = RenderPicker();

        // Act
        await OpenAsync(cut);

        // Assert
        _popovers.FindAll(".picker-group-name").Select(g => g.TextContent).ShouldBe(["sample_store", "prod-db"]);
        _popovers.FindAll(".picker-option-name").Select(o => o.TextContent).ShouldBe(["sample_store", "orders", "analytics"]);
    }

    [Fact]
    public async Task Menu_MarksTheDatabaseTheTabUses()
    {
        // Arrange
        _state.UpdateQueryConnection(_query, _prod, "analytics");
        var cut = RenderPicker();

        // Act
        await OpenAsync(cut);

        // Assert
        Option("analytics")!.QuerySelector(".picker-option-check").ShouldNotBeNull();
        Option("orders")!.QuerySelector(".picker-option-check").ShouldBeNull();
    }

    [Fact]
    public async Task ChoosingADatabaseOnAnotherConnection_PointsTheTabAtBoth()
    {
        // Arrange
        _state.UpdateQueryConnection(_query, _sample);
        var cut = RenderPicker();
        await OpenAsync(cut);

        // Act
        await Option("analytics")!.ClickAsync(new MouseEventArgs());

        // Assert
        _query.ConnectionId.ShouldBe(_prod.Id);
        _query.DatabaseName.ShouldBe("analytics");
    }

    [Fact]
    public async Task ConnectionWithoutDatabases_SaysSoInsteadOfOfferingNothing()
    {
        // Arrange
        _connections.Connections.Add(new ConnectionModel { Name = "offline", Type = DatabaseType.MySQL, ConnectionString = "Server=down" });
        var cut = RenderPicker();

        // Act
        await OpenAsync(cut);

        // Assert
        var empty = _popovers.FindAll(".mud-menu-item").Single(i => i.TextContent.Contains("No databases"));
        empty.GetAttribute("aria-disabled").ShouldBe("true");
    }

    [Fact]
    public async Task NewConnection_AsksForOne()
    {
        // Arrange
        var cut = RenderPicker();
        await OpenAsync(cut);

        // Act
        await _popovers.FindAll(".mud-menu-item").Single(i => i.TextContent.Contains("New connection")).ClickAsync(new MouseEventArgs());

        // Assert
        await _bus.Received(1).PublishAsync(Arg.Any<PromptCreateConnection>());
    }

    [Fact]
    public async Task WhileDisabled_StaysClosedAndSaysWhy()
    {
        // Arrange
        _state.UpdateQueryConnection(_query, _sample);
        var cut = RenderPicker(disabled: true);

        // Act
        await OpenAsync(cut);

        // Assert
        _popovers.FindAll(".mud-menu-item").ShouldBeEmpty();
        cut.FindComponent<MudTooltip>().Instance.Text.ShouldBe("Commit or roll back the open transaction to change this");
    }
}
