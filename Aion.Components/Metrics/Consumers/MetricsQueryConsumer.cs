using Aion.Components.Metrics.Events;
using Aion.Components.Querying.Events;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Metrics.Consumers;

public class MetricsQueryConsumer : IConsumer<QueryExecuted>
{
    private readonly IMessageBus _bus;

    public MetricsQueryConsumer(IMessageBus bus)
    {
        _bus = bus;
    }

    public async Task Consume(QueryExecuted message)
    {
        var query = message.Query;
        if (query.ConnectionId is not { } connectionId || connectionId == Guid.Empty)
            return;
        if (query.ExecutionDuration is not { } duration)
            return;

        var rowCount = query.Result?.Rows?.Count ?? 0;
        var success = query.Result?.Error is null;

        await _bus.PublishAsync(new ClientQueryMetricRecorded(
            connectionId,
            DateTime.UtcNow,
            duration,
            rowCount,
            success));
    }
}
