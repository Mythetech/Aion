using Aion.Components.Metrics.Events;
using Aion.Components.Metrics.Services;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Metrics.Consumers;

public class MetricsServerConsumer : IConsumer<ServerMetricsCollected>
{
    private readonly MetricsState _state;

    public MetricsServerConsumer(MetricsState state)
    {
        _state = state;
    }

    public Task Consume(ServerMetricsCollected message)
    {
        _state.RecordServerMetrics(message);
        return Task.CompletedTask;
    }
}
