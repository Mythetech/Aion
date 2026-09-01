using Aion.Components.Connections.Events;
using Aion.Components.Metrics.Services;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Metrics.Consumers;

public class MetricsHealthConsumer : IConsumer<ConnectionHealthChanged>
{
    private readonly MetricsState _state;

    public MetricsHealthConsumer(MetricsState state)
    {
        _state = state;
    }

    public Task Consume(ConnectionHealthChanged message)
    {
        _state.RecordHealthChange(message.ConnectionId, message.NewStatus);
        return Task.CompletedTask;
    }
}
