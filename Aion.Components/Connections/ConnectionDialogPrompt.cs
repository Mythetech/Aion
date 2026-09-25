using Aion.Components.Shared.Dialogs.Commands;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Connections;

public class ConnectionDialogPrompt : IConnectionPrompt
{
    private readonly IMessageBus _bus;

    public ConnectionDialogPrompt(IMessageBus bus)
    {
        _bus = bus;
    }

    public async Task PromptAsync(ConnectionDialogModel? initialValues)
    {
        await _bus.PublishAsync(CreateDialog(initialValues));
    }

    public static ShowDialog CreateDialog(ConnectionDialogModel? initialValues)
    {
        var isEdit = initialValues?.EditingConnectionId != null;
        var title = isEdit ? "Edit Connection" : "Create Connection";

        var parameters = new DialogParameters();
        if (initialValues != null)
        {
            parameters.Add(nameof(ConnectionDialog.InitialValues), initialValues);
        }

        return new ShowDialog(typeof(ConnectionDialog), title, Parameters: parameters);
    }
}
