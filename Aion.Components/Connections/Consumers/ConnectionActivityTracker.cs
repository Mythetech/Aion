using Aion.Components.Connections.Services;
using Aion.Components.Querying.Events;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Connections.Consumers;

/// <summary>
/// Tracks connection activity by updating LastActivityTime when queries are executed.
/// This enables the health monitor to know which connections are actively being used.
/// </summary>
public class ConnectionActivityTracker : IConsumer<QueryExecuted>
{
    private readonly IConnectionHealthMonitor _healthMonitor;

    public ConnectionActivityTracker(IConnectionHealthMonitor healthMonitor)
    {
        _healthMonitor = healthMonitor;
    }

    public Task Consume(QueryExecuted message)
    {
        if (message.Query.ConnectionId.HasValue && message.Query.ConnectionId.Value != Guid.Empty)
        {
            _healthMonitor.RecordActivity(message.Query.ConnectionId.Value);
        }

        return Task.CompletedTask;
    }
}
