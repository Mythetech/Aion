using Aion.Components.Connections.Events;
using Aion.Components.Settings.Domains;
using Aion.Core.Connections;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Connections.Services;

public class ConnectionHealthMonitor : IConnectionHealthMonitor
{
    private readonly IConnectionService _connectionService;
    private readonly ConnectionState _connectionState;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<ConnectionHealthMonitor> _logger;
    private readonly ConnectionSettings _settings;

    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _monitoringTask;
    private bool _disposed;

    public ConnectionHealthMonitor(
        IConnectionService connectionService,
        ConnectionState connectionState,
        IMessageBus messageBus,
        ConnectionSettings settings,
        ILogger<ConnectionHealthMonitor> logger)
    {
        _connectionService = connectionService;
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
                await CheckActiveConnectionsAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during connection health check cycle");
            }
        }
    }

    private async Task CheckActiveConnectionsAsync(CancellationToken cancellationToken)
    {
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

        var tasks = connectionsToCheck.Select(c => CheckAndUpdateConnectionAsync(c, cancellationToken));
        await Task.WhenAll(tasks);
    }

    private bool ShouldCheckConnection(ConnectionModel connection, DateTime now)
    {
        // Always check if we don't know the health status
        if (connection.HealthStatus == ConnectionHealthStatus.Unknown)
            return true;

        // Check if connection was recently active
        if (connection.LastActivityTime.HasValue)
        {
            var timeSinceActivity = now - connection.LastActivityTime.Value;
            return timeSinceActivity <= _settings.ActivityThreshold;
        }

        // If no activity recorded but connection is marked active, check it
        return connection.Active;
    }

    private async Task CheckAndUpdateConnectionAsync(ConnectionModel connection, CancellationToken cancellationToken)
    {
        var oldStatus = connection.HealthStatus;
        connection.HealthStatus = ConnectionHealthStatus.Checking;
        await PublishHealthChangedAsync(connection.Id, ConnectionHealthStatus.Checking, oldStatus);

        var result = await CheckConnectionHealthAsync(connection);

        connection.LastHealthCheckTime = result.CheckTime;
        connection.HealthStatus = result.IsHealthy ? ConnectionHealthStatus.Healthy : ConnectionHealthStatus.Unhealthy;
        connection.Active = result.IsHealthy;

        if (connection.HealthStatus != oldStatus || !result.IsHealthy)
        {
            await PublishHealthChangedAsync(connection.Id, connection.HealthStatus, oldStatus, result.ErrorMessage);
        }
    }

    public async Task<ConnectionHealthCheckResult> CheckConnectionHealthAsync(ConnectionModel connection)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            using var timeoutCts = new CancellationTokenSource(_settings.ConnectionTimeout);

            var databases = await _connectionService.GetDatabasesAsync(
                connection.ConnectionString,
                connection.Type);

            var responseTime = DateTime.UtcNow - startTime;

            var isHealthy = databases != null;

            _logger.LogDebug(
                "Health check for {ConnectionName}: {Status} (response time: {ResponseTime}ms)",
                connection.Name,
                isHealthy ? "Healthy" : "Unhealthy",
                responseTime.TotalMilliseconds);

            return new ConnectionHealthCheckResult(
                connection.Id,
                isHealthy,
                startTime,
                responseTime,
                isHealthy ? null : "Failed to retrieve databases");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Health check for {ConnectionName} timed out", connection.Name);

            return new ConnectionHealthCheckResult(
                connection.Id,
                false,
                startTime,
                _settings.ConnectionTimeout,
                "Connection timed out");
        }
        catch (Exception ex)
        {
            var responseTime = DateTime.UtcNow - startTime;

            _logger.LogWarning(ex, "Health check failed for {ConnectionName}", connection.Name);

            return new ConnectionHealthCheckResult(
                connection.Id,
                false,
                startTime,
                responseTime,
                ex.Message);
        }
    }

    public void RecordActivity(Guid connectionId)
    {
        var connection = _connectionState.Connections.FirstOrDefault(c => c.Id == connectionId);
        if (connection != null)
        {
            connection.LastActivityTime = DateTime.UtcNow;
            _logger.LogDebug("Recorded activity for connection {ConnectionName}", connection.Name);
        }
    }

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
