using Aion.Components.Connections.Commands;
using Aion.Components.Connections.Services;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Connections.Consumers;

public class ConnectionHealthRefresher : IConsumer<RefreshConnectionHealth>
{
    private readonly IConnectionHealthMonitor _healthMonitor;

    public ConnectionHealthRefresher(IConnectionHealthMonitor healthMonitor)
    {
        _healthMonitor = healthMonitor;
    }

    public async Task Consume(RefreshConnectionHealth message)
    {
        await _healthMonitor.RefreshAsync(message.ConnectionId);
    }
}
