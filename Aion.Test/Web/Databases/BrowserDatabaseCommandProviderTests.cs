using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Test.TestDoubles;
using Aion.Web.Databases;
using Aion.Web.Databases.Commands;
using Aion.Web.Providers;
using Aion.Web.Services;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Web.Databases;

public class BrowserDatabaseCommandProviderTests : TestContext
{
    private readonly InBrowserEngines _engines = new();
    private readonly IMessageBus _bus;
    private readonly ConnectionState _connectionState;
    private readonly ConnectionModel _sales = new()
    {
        Name = "sales",
        ConnectionString = "pglite://sales",
        Type = DatabaseType.WasmPostgreSQL
    };
    private readonly ConnectionModel _inventory = new()
    {
        Name = "inventory",
        ConnectionString = "pglite://inventory",
        Type = DatabaseType.WasmPostgreSQL
    };

    public BrowserDatabaseCommandProviderTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        _bus = new InMemoryMessageBus(
            Services,
            Substitute.For<ILogger<InMemoryMessageBus>>(),
            Enumerable.Empty<IMessagePipe>(),
            Enumerable.Empty<IConsumerFilter>());
        Services.AddSingleton(_bus);

        _connectionState = new ConnectionState(_engines.ConnectionService, _engines.Factory, _bus, Substitute.For<ILogger<ConnectionState>>());
        _connectionState.Connections = [_sales, _inventory];
        Services.AddSingleton(_connectionState);

        var queryState = new QueryState(_bus, Substitute.For<IQuerySaveService>());
        Services.AddSingleton(queryState);
        Services.AddSingleton(new SchemaExecutor(
            _engines.Factory,
            _connectionState,
            queryState,
            new IndexedDbStorageService(new JsModuleFake().Runtime),
            _bus));
        Services.AddSingleton<ISupportedTypeProvider, SqliteWasmTypeProvider>();
        Services.AddSingleton<ISupportedTypeProvider, PGliteTypeProvider>();
    }

    private IRenderedComponent<MudDialogProvider> RenderHost()
    {
        RenderComponent<MudPopoverProvider>();
        var dialogs = RenderComponent<MudDialogProvider>();
        RenderComponent<BrowserDatabaseCommandProvider>();
        return dialogs;
    }

    private static async Task ClickButtonAsync(IRenderedComponent<MudDialogProvider> dialogs, string text)
    {
        await dialogs.FindAll("button").First(b => b.TextContent.Trim() == text).ClickAsync(new MouseEventArgs());
    }

    [Fact]
    public async Task ClearDatabase_AsksBeforeDeletingAnything()
    {
        var dialogs = RenderHost();

        var clear = _bus.PublishAsync(new ClearBrowserDatabase(_sales.Id));

        dialogs.WaitForAssertion(() => dialogs.Markup.ShouldContain("Delete the in-browser database \"sales\""));
        await _engines.ConnectionService.DidNotReceive().RemoveConnection(Arg.Any<Guid>());

        await ClickButtonAsync(dialogs, "Cancel");
        await clear;

        await _engines.ConnectionService.DidNotReceive().RemoveConnection(Arg.Any<Guid>());
        _connectionState.Connections.ShouldBe([_sales, _inventory]);
    }

    [Fact]
    public async Task ClearDatabase_Confirmed_RemovesOnlyThatDatabase()
    {
        var dialogs = RenderHost();

        var clear = _bus.PublishAsync(new ClearBrowserDatabase(_sales.Id));

        dialogs.WaitForAssertion(() => dialogs.Markup.ShouldContain("Delete the in-browser database \"sales\""));
        await ClickButtonAsync(dialogs, "Clear Database");
        await clear;

        await _engines.ConnectionService.Received(1).RemoveConnection(_sales.Id);
        await _engines.ConnectionService.DidNotReceive().RemoveConnection(_inventory.Id);
        _connectionState.Connections.ShouldBe([_inventory]);
    }

    [Fact]
    public async Task CreateDatabase_OpensTheWizardOnTheRequestedEngine()
    {
        var dialogs = RenderHost();

        var create = _bus.PublishAsync(new CreateBrowserDatabase(DatabaseType.WasmPostgreSQL));

        dialogs.WaitForAssertion(() => dialogs.Markup.ShouldContain("Database Name"));
        dialogs.FindAll("input")
            .Select(i => i.GetAttribute("value"))
            .ShouldContain(v => v == nameof(DatabaseType.WasmPostgreSQL) || v == "PostgreSQL (PGlite)");

        await ClickButtonAsync(dialogs, "Cancel");
        await create;

        _connectionState.Connections.ShouldBe([_sales, _inventory]);
    }
}
