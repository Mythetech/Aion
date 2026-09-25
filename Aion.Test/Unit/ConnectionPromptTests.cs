using Aion.Components.Connections;
using Aion.Components.Connections.Commands;
using Aion.Components.Connections.Consumers;
using Aion.Components.Shared.Dialogs.Commands;
using Aion.Contracts.Database;
using Aion.Web.Databases;
using Aion.Web.Databases.Commands;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit;

public class ConnectionPromptTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task ConnectionDialogCreator_HandsThePromptToTheHost()
    {
        var prompt = Substitute.For<IConnectionPrompt>();
        var initialValues = new ConnectionDialogModel { Name = "Reporting" };
        var creator = new ConnectionDialogCreator(prompt);

        await creator.Consume(new PromptCreateConnection(initialValues));

        await prompt.Received(1).PromptAsync(initialValues);
    }

    [Fact]
    public async Task DesktopPrompt_NewConnection_OpensTheConnectionDialog()
    {
        var prompt = new ConnectionDialogPrompt(_bus);

        await prompt.PromptAsync(null);

        await _bus.Received(1).PublishAsync(Arg.Is<ShowDialog>(d =>
            d.Dialog == typeof(ConnectionDialog) && d.Title == "Create Connection"));
    }

    [Fact]
    public async Task DesktopPrompt_Edit_PassesTheConnectionToTheDialog()
    {
        var prompt = new ConnectionDialogPrompt(_bus);
        var initialValues = new ConnectionDialogModel { EditingConnectionId = Guid.NewGuid() };

        await prompt.PromptAsync(initialValues);

        await _bus.Received(1).PublishAsync(Arg.Is<ShowDialog>(d =>
            d.Dialog == typeof(ConnectionDialog)
            && d.Title == "Edit Connection"
            && d.Parameters != null
            && d.Parameters.Get<ConnectionDialogModel>(nameof(ConnectionDialog.InitialValues)) == initialValues));
    }

    [Fact]
    public async Task BrowserPrompt_NewConnection_OpensTheNewDatabaseWizard()
    {
        var prompt = new BrowserConnectionPrompt(_bus);

        await prompt.PromptAsync(null);

        await _bus.Received(1).PublishAsync(Arg.Is<CreateBrowserDatabase>(c => c.Engine == null));
        await _bus.DidNotReceive().PublishAsync(Arg.Any<ShowDialog>());
    }

    [Fact]
    public async Task BrowserPrompt_Edit_OpensTheConnectionDialog()
    {
        var prompt = new BrowserConnectionPrompt(_bus);
        var initialValues = new ConnectionDialogModel
        {
            EditingConnectionId = Guid.NewGuid(),
            Type = DatabaseType.WasmSQLite
        };

        await prompt.PromptAsync(initialValues);

        await _bus.Received(1).PublishAsync(Arg.Is<ShowDialog>(d => d.Dialog == typeof(ConnectionDialog)));
        await _bus.DidNotReceive().PublishAsync(Arg.Any<CreateBrowserDatabase>());
    }

    [Fact]
    public void ConnectionDialogCreator_ResolvesFromTheRootProvider()
    {
        // The message bus resolves consumers from the root provider. Scoped UI services such as
        // IDialogService made this throw ScopedResolvedFromRootException on WASM.
        var services = new ServiceCollection();
        services.AddMudServices();
        services.AddSingleton(_bus);
        services.AddSingleton<IConnectionPrompt, BrowserConnectionPrompt>();
        services.AddTransient<ConnectionDialogCreator>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        Should.NotThrow(() => provider.GetRequiredService<ConnectionDialogCreator>());
    }
}
