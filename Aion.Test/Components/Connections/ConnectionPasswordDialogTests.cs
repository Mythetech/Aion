using Aion.Components.Connections;
using Aion.Components.Connections.Secrets;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Test.TestDoubles;
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

public class ConnectionPasswordDialogTests : TestContext
{
    private readonly IConnectionService _connectionService = Substitute.For<IConnectionService>();
    private readonly ConnectionSecretStoreFake _secrets = new() { ActiveStoreName = "macOS Keychain" };
    private readonly ConnectionState _connectionState;
    private readonly ConnectionModel _connection;

    public ConnectionPasswordDialogTests()
    {
        Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;

        _connectionState = new ConnectionState(_connectionService, Substitute.For<IDatabaseProviderFactory>(),
            Substitute.For<IMessageBus>(), NullLogger<ConnectionState>.Instance, _secrets);
        _connection = new ConnectionModel
        {
            Name = "Prod",
            Type = DatabaseType.PostgreSQL,
            ConnectionString = "Host=db;Username=app",
            UsesPassword = true,
            HealthStatus = ConnectionHealthStatus.NeedsPassword,
            LastError = "The password wasn't found in macOS Keychain"
        };
        _connectionState.Connections.Add(_connection);

        Services.AddSingleton(_connectionState);
        Services.AddSingleton<IConnectionSecretStore>(_secrets);
    }

    private async Task<IRenderedComponent<MudDialogProvider>> ShowDialogAsync()
    {
        var provider = RenderComponent<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters { { nameof(ConnectionPasswordDialog.ConnectionId), _connection.Id } };

        await provider.InvokeAsync(() => dialogService.ShowAsync<ConnectionPasswordDialog>("Enter Password", parameters));
        provider.WaitForElement(".primary-action-button");
        return provider;
    }

    private static async Task TypePasswordAsync(IRenderedFragment cut, string password) =>
        await cut.Find("input[type=password]").ChangeAsync(new ChangeEventArgs { Value = password });

    private static AngleSharp.Dom.IElement RadioLabel(IRenderedFragment cut, string text) =>
        cut.FindAll("label.mud-radio").Single(l => l.TextContent.Contains(text));

    private void ServerAccepts() =>
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>()).Returns(["app"]);

    [Fact]
    public async Task OffersTheSameStoreChoice_DefaultingToDontStore()
    {
        var cut = await ShowDialogAsync();

        RadioLabel(cut, "Store in macOS Keychain").ShouldNotBeNull();
        RadioLabel(cut, "Don't store (ask when connecting)").QuerySelector("input")!.HasAttribute("checked").ShouldBeTrue();
    }

    [Fact]
    public async Task ExplainsWhyThePasswordIsNeeded()
    {
        var cut = await ShowDialogAsync();

        cut.Find(".password-prompt-reason").TextContent.ShouldContain("The password wasn't found in macOS Keychain");
    }

    [Fact]
    public async Task Connect_WithTheRightPassword_ConnectsAndCloses()
    {
        ServerAccepts();
        var cut = await ShowDialogAsync();

        await TypePasswordAsync(cut, "hunter2");
        await cut.Find(".primary-action-button").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => cut.FindAll(".mud-dialog").Count.ShouldBe(0));
        _connection.Active.ShouldBeTrue();
        _secrets.Passwords.ShouldBeEmpty();
    }

    [Fact]
    public async Task Connect_WithStoreChosen_StoresThePassword()
    {
        ServerAccepts();
        var cut = await ShowDialogAsync();

        await TypePasswordAsync(cut, "hunter2");
        await RadioLabel(cut, "Store in macOS Keychain").QuerySelector("input")!.ClickAsync(new MouseEventArgs());
        await cut.Find(".primary-action-button").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => cut.FindAll(".mud-dialog").Count.ShouldBe(0));
        _secrets.Passwords[("macOS Keychain", _connection.Id)].ShouldBe("hunter2");
    }

    [Fact]
    public async Task Connect_Rejected_ShowsTheErrorAndStaysOpen()
    {
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>())
            .Returns<List<string>?>(_ => throw new InvalidOperationException("password authentication failed for user \"app\""));
        var cut = await ShowDialogAsync();

        await TypePasswordAsync(cut, "wrong");
        await cut.Find(".primary-action-button").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
            cut.Find(".password-prompt-error").TextContent.ShouldContain("password authentication failed"));
        cut.FindAll(".mud-dialog").Count.ShouldBe(1);
        _connection.HealthStatus.ShouldBe(ConnectionHealthStatus.NeedsPassword);
    }

    [Fact]
    public async Task Connect_WithoutAPassword_AsksForOne()
    {
        var cut = await ShowDialogAsync();

        await cut.Find(".primary-action-button").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Password is required"));
        await _connectionService.DidNotReceiveWithAnyArgs().GetDatabasesAsync(default!, default);
    }

    [Fact]
    public async Task LiteDb_AsksForTheEncryptionPassword()
    {
        _connection.Type = DatabaseType.LiteDB;
        _connection.ConnectionString = "Filename=/data/app.db";

        var cut = await ShowDialogAsync();

        cut.FindAll("label").ShouldContain(l => l.TextContent.Trim() == "Encryption Password");
    }
}
