using Aion.Core.Connections;

namespace Aion.Components.Connections.Services;

public interface IConnectionHealthMonitor : IDisposable
{
    /// <summary>
    /// Start the background health monitoring service.
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stop the background health monitoring service.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Manually check the health of a specific connection.
    /// </summary>
    Task<ConnectionHealthCheckResult> CheckConnectionHealthAsync(ConnectionModel connection);

    /// <summary>
    /// Update the last activity time for a connection, marking it as recently used.
    /// </summary>
    void RecordActivity(Guid connectionId);
}
