using Aion.Components.Connections.Events;
using Aion.Components.Infrastructure.MessageBus;

namespace Aion.Components.Connections.Consumers;

public class DatabaseRefresher : IConsumer<DatabaseCreated>
{
    private readonly ConnectionState _state;

    public DatabaseRefresher(ConnectionState state)
    {
        _state = state;
    }
    public async Task Consume(DatabaseCreated message)
    {
        await _state.RefreshDatabaseAsync(message.Connection);
    }
}