using Aion.Components.Connections.Events;
using Aion.Components.Settings.Domains;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Connections.Services;

public class ConnectionHealthMonitor : IConnectionHealthMonitor
{
    private readonly ConnectionState _connectionState;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<ConnectionHealthMonitor> _logger;
    private readonly ConnectionSettings _settings;

    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _monitoringTask;
    private bool _disposed;

    public ConnectionHealthMonitor(
        ConnectionState connectionState,
        IMessageBus messageBus,
        ConnectionSettings settings,
        ILogger<ConnectionHealthMonitor> logger)
    {
        _connectionState = connectionState;
        _messageBus = messageBus;
        _settings = settings;
        _logger = logger;
    }

    public Task StartAsync()
    {
        if (!_settings.EnableAutoHealthCheck)
        {
            _logger.LogInformation("Auto health check is disabled");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Starting connection health monitor with poll interval: {PollInterval}", _settings.PollInterval);

        _cancellationTokenSource = new CancellationTokenSource();
        _timer = new PeriodicTimer(_settings.PollInterval);
        _monitoringTask = MonitorConnectionsAsync(_cancellationTokenSource.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping connection health monitor");

        _cancellationTokenSource?.Cancel();
        _timer?.Dispose();

        if (_monitoringTask != null)
        {
            try
            {
                await _monitoringTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
        }
    }

    private async Task MonitorConnectionsAsync(CancellationToken cancellationToken)
    {
        while (await _timer!.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                await CheckConnectionsAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Error during connection health check cycle");
            }
        }
    }

    /// <summary>
    /// Runs one polling cycle: checks every remote connection that is unchecked or was used recently.
    /// </summary>
    public async Task CheckConnectionsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTime.UtcNow;
        var connectionsToCheck = _connectionState.Connections
            .Where(c => ShouldCheckConnection(c, now))
            .ToList();

        if (connectionsToCheck.Count == 0)
        {
            _logger.LogDebug("No connections require health check");
            return;
        }

        _logger.LogDebug("Checking health of {Count} connections", connectionsToCheck.Count);

        await Task.WhenAll(connectionsToCheck.Select(CheckAndUpdateConnectionAsync));
    }

    public async Task RefreshAsync(Guid? connectionId = null)
    {
        var connectionsToCheck = _connectionState.Connections
            .Where(c => connectionId == null || c.Id == connectionId)
            .Where(IsCheckable)
            .ToList();

        await Task.WhenAll(connectionsToCheck.Select(CheckAndUpdateConnectionAsync));
    }

    private bool ShouldCheckConnection(ConnectionModel connection, DateTime now)
    {
        if (!IsCheckable(connection))
            return false;

        if (connection.HealthStatus == ConnectionHealthStatus.Unknown)
            return true;

        if (connection.LastActivityTime.HasValue)
        {
            var timeSinceActivity = now - connection.LastActivityTime.Value;
            return timeSinceActivity <= _settings.ActivityThreshold;
        }

        return connection.Active;
    }

    // In-process engines have no server to lose, and polling them only flickers the status chip.
    private static bool IsCheckable(ConnectionModel connection) =>
        !connection.Type.IsInProcess() && connection.HealthStatus != ConnectionHealthStatus.Checking;

    private async Task CheckAndUpdateConnectionAsync(ConnectionModel connection)
    {
        var oldStatus = connection.HealthStatus;
        _connectionState.MarkHealthCheckStarted(connection);
        await PublishHealthChangedAsync(connection.Id, ConnectionHealthStatus.Checking, oldStatus);

        var result = await CheckConnectionHealthAsync(connection);
        _connectionState.ApplyHealthCheck(connection, result);

        if (connection.HealthStatus != oldStatus || !result.IsHealthy)
        {
            await PublishHealthChangedAsync(connection.Id, connection.HealthStatus, oldStatus, result.ErrorMessage);
        }
    }

    public async Task<ConnectionHealthCheckResult> CheckConnectionHealthAsync(ConnectionModel connection)
    {
        var startTime = DateTime.UtcNow;

        // The providers take no cancellation token, so the driver's own connect timeout enforces the setting.
        var connectionString = ConnectionStringComposer.WithConnectTimeout(
            connection.ConnectionString, connection.Type, _settings.ConnectionTimeout);

        var result = await _connectionState.TestConnectionAsync(connectionString, connection.Type);
        var responseTime = DateTime.UtcNow - startTime;

        _logger.LogDebug(
            "Health check for {ConnectionName}: {Status} (response time: {ResponseTime}ms)",
            connection.Name,
            result.Success ? "Healthy" : "Unhealthy",
            responseTime.TotalMilliseconds);

        return new ConnectionHealthCheckResult(
            connection.Id,
            result.Success,
            startTime,
            responseTime,
            result.Error,
            result.TimedOut);
    }

    public void RecordActivity(Guid connectionId) => _connectionState.RecordActivity(connectionId);

    private async Task PublishHealthChangedAsync(
        Guid connectionId,
        ConnectionHealthStatus newStatus,
        ConnectionHealthStatus oldStatus,
        string? errorMessage = null)
    {
        await _messageBus.PublishAsync(new ConnectionHealthChanged(
            connectionId,
            newStatus,
            oldStatus,
            errorMessage));
    }

    public void Dispose()
    {
        if (_disposed) return;

        _cancellationTokenSource?.Cancel();
        _timer?.Dispose();
        _cancellationTokenSource?.Dispose();
        _disposed = true;
    }
}
