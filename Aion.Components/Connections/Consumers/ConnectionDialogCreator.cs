using Aion.Components.Connections.Commands;
using Mythetech.Framework.Infrastructure.MessageBus;
using Aion.Components.Shared;
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
        await _bus.PublishAsync(new ShowDialog(typeof(ConnectionDialog), "Create Connection"));
    }
}