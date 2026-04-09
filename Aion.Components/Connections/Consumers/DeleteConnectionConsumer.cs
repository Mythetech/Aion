using Aion.Components.Connections.Commands;
using Aion.Components.Shared.Dialogs;
using Aion.Components.Shared.Dialogs.Commands;
using Mythetech.Framework.Infrastructure.MessageBus;
using MudBlazor;

namespace Aion.Components.Connections.Consumers;

public class DeleteConnectionConsumer : IConsumer<DeleteConnection>
{
    private readonly ConnectionState _connectionState;
    private readonly IMessageBus _bus;

    public DeleteConnectionConsumer(ConnectionState connectionState, IMessageBus bus)
    {
        _connectionState = connectionState;
        _bus = bus;
    }

    public async Task Consume(DeleteConnection message)
    {
        var connection = _connectionState.Connections.FirstOrDefault(c => c.Id == message.ConnectionId);
        if (connection == null) return;

        var parameters = new DialogParameters
        {
            { nameof(DeleteConnectionDialog.ConnectionName), connection.Name },
            { nameof(DeleteConnectionDialog.ConnectionId), connection.Id }
        };

        var options = AionDialogs.CreateDefaultOptions();
        await _bus.PublishAsync(new ShowDialog(typeof(DeleteConnectionDialog), "Delete Connection", options, parameters));
    }
}
