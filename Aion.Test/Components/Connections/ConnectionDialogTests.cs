using Aion.Components.Connections;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.Connections;

public class ConnectionDialogTests : TestContext
{
    private readonly IConnectionService _connectionService;
    private readonly ConnectionState _connectionState;

    public ConnectionDialogTests()
    {
        Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;

        var provider = Substitute.For<IDatabaseProvider>();
        provider.DatabaseType.Returns(DatabaseType.PostgreSQL);
        provider.GetDefaultPort().Returns(5432);
        provider.ValidateConnectionString(Arg.Any<string>(), out Arg.Any<string?>()).Returns(true);

        var providerFactory = Substitute.For<IDatabaseProviderFactory>();
        providerFactory.SupportedDatabases.Returns([DatabaseType.PostgreSQL, DatabaseType.SQLServer]);
        providerFactory.GetProvider(Arg.Any<DatabaseType>()).Returns(provider);

        _connectionService = Substitute.For<IConnectionService>();
        _connectionState = new ConnectionState(_connectionService, providerFactory, Substitute.For<IMessageBus>(),
            NullLogger<ConnectionState>.Instance);

        Services.AddSingleton(providerFactory);
        Services.AddSingleton(_connectionState);
    }

    private static ConnectionDialogModel FilledIn() => new()
    {
        Name = "Local",
        Type = DatabaseType.PostgreSQL,
        Host = "localhost",
        Port = "5432",
        Username = "postgres",
        Password = "wrong"
    };

    private async Task<IRenderedComponent<MudDialogProvider>> ShowDialogAsync(ConnectionDialogModel model)
    {
        var provider = RenderComponent<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters { { nameof(ConnectionDialog.InitialValues), model } };

        await provider.InvokeAsync(() => dialogService.ShowAsync<ConnectionDialog>("Create Connection", parameters));
        provider.WaitForElement(".primary-action-button");
        return provider;
    }

    private void ServerRejects(string message) =>
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>())
            .Returns<List<string>?>(_ => throw new InvalidOperationException(message));

    [Fact]
    public async Task Connect_WhenServerRejects_StaysOpenAndShowsError()
    {
        ServerRejects("28P01: password authentication failed for user \"postgres\"");
        var cut = await ShowDialogAsync(FilledIn());

        await cut.Find(".primary-action-button").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
            cut.Find(".connection-dialog-error").TextContent.ShouldContain("password authentication failed"));
        cut.FindAll(".mud-dialog").Count.ShouldBe(1);
        _connectionState.Connections.ShouldBeEmpty();
        await _connectionService.DidNotReceive().AddConnection(Arg.Any<ConnectionModel>());
    }

    [Fact]
    public async Task Connect_WhenServerAnswers_ClosesAndAddsConnection()
    {
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>())
            .Returns(new List<string> { "postgres" });
        var cut = await ShowDialogAsync(FilledIn());

        await cut.Find(".primary-action-button").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => cut.FindAll(".mud-dialog").Count.ShouldBe(0));
        _connectionState.Connections.ShouldHaveSingleItem().Active.ShouldBeTrue();
    }

    [Fact]
    public async Task TestConnection_ReportsResultWithoutSaving()
    {
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>())
            .Returns(new List<string> { "postgres", "app" });
        var cut = await ShowDialogAsync(FilledIn());

        await cut.Find(".connection-dialog-test").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
            cut.Find(".connection-dialog-success").TextContent.ShouldContain("Found 2 databases"));
        cut.FindAll(".mud-dialog").Count.ShouldBe(1);
        _connectionState.Connections.ShouldBeEmpty();
        await _connectionService.DidNotReceive().AddConnection(Arg.Any<ConnectionModel>());
    }

    [Fact]
    public async Task TestConnection_WhenServerRejects_ShowsError()
    {
        ServerRejects("Connection refused");
        var cut = await ShowDialogAsync(FilledIn());

        await cut.Find(".connection-dialog-test").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
            cut.Find(".connection-dialog-error").TextContent.ShouldContain("Connection refused"));
    }

    [Fact]
    public async Task Connect_SendsPasswordWithSemicolonIntact()
    {
        var model = FilledIn();
        model.Password = "pa;ss'word";
        string? sentConnectionString = null;
        _connectionService.GetDatabasesAsync(Arg.Do<string>(cs => sentConnectionString = cs), Arg.Any<DatabaseType>())
            .Returns(new List<string> { "postgres" });
        var cut = await ShowDialogAsync(model);

        await cut.Find(".primary-action-button").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => sentConnectionString.ShouldNotBeNull());
        new Npgsql.NpgsqlConnectionStringBuilder(sentConnectionString).Password.ShouldBe("pa;ss'word");
    }

    [Fact]
    public async Task Edit_ParsesExistingConnectionStringIntoBasicFields()
    {
        var existing = new ConnectionModel
        {
            Name = "Prod",
            Type = DatabaseType.PostgreSQL,
            ConnectionString = "Host=db.internal;Port=6543;Database=app;Username=reader;Password=secret"
        };

        var cut = await ShowDialogAsync(ConnectionDialogModel.ForEdit(existing));

        var values = cut.FindAll("input").Select(i => i.GetAttribute("value")).ToList();
        values.ShouldContain("db.internal");
        values.ShouldContain("6543");
        values.ShouldContain("app");
        values.ShouldContain("reader");
    }

    [Fact]
    public async Task Edit_SaveFromBasicTab_UsesEditedFields()
    {
        var existing = new ConnectionModel
        {
            Name = "Prod",
            Type = DatabaseType.PostgreSQL,
            ConnectionString = "Host=old-host;Username=reader;Password=secret;SSL Mode=Require"
        };
        _connectionState.Connections = [existing];
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>())
            .Returns(new List<string> { "postgres" });
        var model = ConnectionDialogModel.ForEdit(existing);
        var cut = await ShowDialogAsync(model);

        var hostInput = cut.FindAll("input").First(i => i.GetAttribute("value") == "old-host");
        await hostInput.ChangeAsync(new ChangeEventArgs { Value = "new-host" });
        await cut.Find(".primary-action-button").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => cut.FindAll(".mud-dialog").Count.ShouldBe(0));
        var saved = new Npgsql.NpgsqlConnectionStringBuilder(existing.ConnectionString);
        saved.Host.ShouldBe("new-host");
        saved.SslMode.ShouldBe(Npgsql.SslMode.Require);
    }

    [Fact]
    public async Task WindowsAuthenticationSwitch_HidesCredentialFields()
    {
        var model = FilledIn();
        model.Type = DatabaseType.SQLServer;
        var cut = await ShowDialogAsync(model);

        var windowsAuthSwitch = cut.FindAll("label")
            .First(l => l.TextContent.Contains("Use Windows Authentication"))
            .QuerySelector("input")!;
        await windowsAuthSwitch.ChangeAsync(new ChangeEventArgs { Value = true });

        cut.WaitForAssertion(() =>
            cut.FindAll("label").ShouldNotContain(l => l.TextContent.Trim() == "Username"));
        model.UseWindowsAuth.ShouldBeTrue();
    }
}
