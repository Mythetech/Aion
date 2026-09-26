using Aion.Components.Connections;
using Aion.Components.Scaffolding;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Test.TestDoubles;
using Aion.Web.Databases;
using Aion.Web.Databases.Commands;
using Aion.Web.Providers;
using Bunit;
using Microsoft.AspNetCore.Components;
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
    private readonly ISchemaExecutor _executor = Substitute.For<ISchemaExecutor>();
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

        Services.AddSingleton(_executor);
        Services.AddSingleton<ISupportedTypeProvider, SqliteWasmTypeProvider>();
        Services.AddSingleton<ISupportedTypeProvider, PGliteTypeProvider>();
    }

    private IRenderedComponent<MudPopoverProvider> _popovers = default!;

    private IRenderedComponent<MudDialogProvider> RenderHost()
    {
        _popovers = RenderComponent<MudPopoverProvider>();
        var dialogs = RenderComponent<MudDialogProvider>();
        RenderComponent<BrowserDatabaseCommandProvider>();
        return dialogs;
    }

    private static async Task ClickButtonAsync(IRenderedComponent<MudDialogProvider> dialogs, string text)
    {
        await dialogs.FindAll("button").First(b => b.TextContent.Trim() == text).ClickAsync(new MouseEventArgs());
    }

    private static AngleSharp.Dom.IElement Field(IRenderedComponent<MudDialogProvider> dialogs, string label) =>
        dialogs.FindAll(".mud-input-control")
            .First(c => c.QuerySelector("label")?.TextContent.Trim().StartsWith(label) == true);

    private static async Task TypeAsync(IRenderedComponent<MudDialogProvider> dialogs, string label, string value) =>
        await Field(dialogs, label).QuerySelector("input")!.ChangeAsync(new ChangeEventArgs { Value = value });

    private async Task ChooseAsync(IRenderedComponent<MudDialogProvider> dialogs, string label, string item)
    {
        await Field(dialogs, label).MouseDownAsync(new MouseEventArgs { Button = 0 });
        _popovers.WaitForAssertion(() => _popovers.FindAll(".mud-list-item").ShouldNotBeEmpty());
        await _popovers.FindAll(".mud-list-item")
            .First(li => li.TextContent.Trim() == item)
            .ClickAsync(new MouseEventArgs());
    }

    private async Task FillInOneTableAsync(IRenderedComponent<MudDialogProvider> dialogs)
    {
        await TypeAsync(dialogs, "Database Name", "shop");
        await ClickButtonAsync(dialogs, "Next");
        await TypeAsync(dialogs, "Table Name", "orders");
        await TypeAsync(dialogs, "Column Name", "id");
        await ChooseAsync(dialogs, "Data Type", "INTEGER");
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

    [Fact]
    public async Task CreateDatabase_WhenTheTablesCannotBeCreated_KeepsTheWizardOpenWithTheReason()
    {
        _executor.ExecuteAsync(Arg.Any<SchemaWizardModel>())
            .Returns(SchemaExecutionResult.Failed("Could not create table orders: near \"(\": syntax error"));
        var dialogs = RenderHost();
        var create = _bus.PublishAsync(new CreateBrowserDatabase(DatabaseType.WasmSQLite));
        dialogs.WaitForAssertion(() => dialogs.Markup.ShouldContain("Database Name"));
        await FillInOneTableAsync(dialogs);

        await ClickButtonAsync(dialogs, "Finish");

        dialogs.WaitForAssertion(() => dialogs.Find(".mud-alert-text-error").TextContent
            .ShouldBe("Could not create table orders: near \"(\": syntax error"));
        create.IsCompleted.ShouldBeFalse();
        await _executor.Received(1).ExecuteAsync(Arg.Is<SchemaWizardModel>(m =>
            m.DatabaseName == "shop" && m.Tables.Single().Name == "orders"));

        await ClickButtonAsync(dialogs, "Cancel");
        await create;
    }

    [Fact]
    public async Task CreateDatabase_WhenTheTablesAreCreated_ClosesTheWizard()
    {
        _executor.ExecuteAsync(Arg.Any<SchemaWizardModel>())
            .Returns(SchemaExecutionResult.Created(new ConnectionModel { Name = "shop", ConnectionString = "pglite://shop" }));
        var dialogs = RenderHost();
        var create = _bus.PublishAsync(new CreateBrowserDatabase(DatabaseType.WasmSQLite));
        dialogs.WaitForAssertion(() => dialogs.Markup.ShouldContain("Database Name"));
        await FillInOneTableAsync(dialogs);

        await ClickButtonAsync(dialogs, "Finish");
        await create;

        dialogs.Markup.ShouldNotContain("Database Name");
    }
}
