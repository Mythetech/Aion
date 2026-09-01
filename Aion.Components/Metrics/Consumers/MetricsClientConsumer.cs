using Aion.Components.Metrics.Events;
using Aion.Components.Metrics.Services;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Metrics.Consumers;

public class MetricsClientConsumer : IConsumer<ClientQueryMetricRecorded>
{
    private readonly MetricsState _state;

    public MetricsClientConsumer(MetricsState state)
    {
        _state = state;
    }

    public Task Consume(ClientQueryMetricRecorded message)
    {
        _state.RecordQueryMetric(message);
        return Task.CompletedTask;
    }
}
