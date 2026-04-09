using Aion.Components.Connections.Commands;
using Mythetech.Framework.Infrastructure.MessageBus;
using Aion.Components.Shared.Dialogs.Commands;
using MudBlazor;

namespace Aion.Components.Connections.Consumers;

public class ConnectionDialogCreator : IConsumer<PromptCreateConnection>
{
    private readonly IDialogService _dialogService;
    private readonly IMessageBus _bus;

    public ConnectionDialogCreator(IDialogService dialogService, IMessageBus bus)
    {
        _dialogService = dialogService;
        _bus = bus;
    }

    public async Task Consume(PromptCreateConnection message)
    {
        var isEdit = message.InitialValues?.EditingConnectionId != null;
        var title = isEdit ? "Edit Connection" : "Create Connection";

        var parameters = new DialogParameters();
        if (message.InitialValues != null)
        {
            parameters.Add(nameof(ConnectionDialog.InitialValues), message.InitialValues);
        }

        await _bus.PublishAsync(new ShowDialog(typeof(ConnectionDialog), title, Parameters: parameters));
    }
}
