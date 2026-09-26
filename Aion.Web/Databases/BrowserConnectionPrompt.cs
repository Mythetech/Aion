using Aion.Components.Connections;
using Aion.Components.Shared.Dialogs.Commands;
using Aion.Contracts.Database;
using Aion.Web.Databases.Commands;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Web.Databases;

public class BrowserConnectionPrompt : IConnectionPrompt
{
    private readonly IMessageBus _bus;

    public BrowserConnectionPrompt(IMessageBus bus)
    {
        _bus = bus;
    }

    public async Task PromptAsync(ConnectionDialogModel? initialValues)
    {
        if (initialValues?.EditingConnectionId is { } id)
        {
            // An in-browser database has no server, port or credentials, so its name is the only thing to edit.
            if (initialValues.Type is DatabaseType.WasmSQLite or DatabaseType.WasmPostgreSQL)
            {
                var parameters = new DialogParameters { { nameof(RenameConnectionDialog.ConnectionId), id } };
                await _bus.PublishAsync(new ShowDialog(typeof(RenameConnectionDialog), $"Rename {initialValues.Name}", Parameters: parameters));
                return;
            }

            await _bus.PublishAsync(ConnectionDialogPrompt.CreateDialog(initialValues));
            return;
        }

        // The browser can't reach database servers, so a new connection here is a new in-browser database.
        await _bus.PublishAsync(new CreateBrowserDatabase());
    }
}
