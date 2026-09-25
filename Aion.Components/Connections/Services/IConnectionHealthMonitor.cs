using Aion.Contracts.Connections;

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
    /// Manually check the health of a specific connection without updating its state.
    /// </summary>
    Task<ConnectionHealthCheckResult> CheckConnectionHealthAsync(ConnectionModel connection);

    /// <summary>
    /// Check and update the health of one connection, or of every server connection when <paramref name="connectionId"/> is null.
    /// </summary>
    Task RefreshAsync(Guid? connectionId = null);

    /// <summary>
    /// Update the last activity time for a connection, marking it as recently used.
    /// </summary>
    void RecordActivity(Guid connectionId);
}
