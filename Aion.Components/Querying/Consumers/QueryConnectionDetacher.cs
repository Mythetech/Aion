using Aion.Components.Connections.Events;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Querying.Consumers;

public class QueryConnectionDetacher : IConsumer<ConnectionRemoved>
{
    private readonly QueryState _state;

    public QueryConnectionDetacher(QueryState state)
    {
        _state = state;
    }

    public Task Consume(ConnectionRemoved message)
    {
        _state.DetachConnection(message.ConnectionId);
        return Task.CompletedTask;
    }
}
