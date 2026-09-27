using Aion.Components.Connections.Commands;
using Aion.Components.Shared.Dialogs;
using Aion.Components.Shared.Dialogs.Commands;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Connections.Consumers;

public class ConnectionPasswordPrompter : IConsumer<PromptConnectionPassword>
{
    private readonly IMessageBus _bus;

    public ConnectionPasswordPrompter(IMessageBus bus)
    {
        _bus = bus;
    }

    public async Task Consume(PromptConnectionPassword message)
    {
        var parameters = new DialogParameters
        {
            { nameof(ConnectionPasswordDialog.ConnectionId), message.ConnectionId }
        };

        await _bus.PublishAsync(new ShowDialog(typeof(ConnectionPasswordDialog), "Enter Password",
            AionDialogs.CreateDefaultOptions(), parameters));
    }
}
