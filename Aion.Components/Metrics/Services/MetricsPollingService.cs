using Aion.Components.Metrics.Events;
using Aion.Components.Metrics.Settings;
using Aion.Contracts.Database;
using Aion.Contracts.Metrics;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Metrics.Services;

public class MetricsPollingService : IMetricsPollingService
{
    private readonly IMessageBus _bus;
    private readonly MetricsSettings _settings;
    private readonly ILogger<MetricsPollingService> _logger;
    private readonly Dictionary<Guid, CancellationTokenSource> _activePollers = new();
    private readonly Dictionary<Guid, Task> _pollerTasks = new();
    private bool _disposed;

    public MetricsPollingService(
        IMessageBus bus,
        MetricsSettings settings,
        ILogger<MetricsPollingService> logger)
    {
        _bus = bus;
        _settings = settings;
        _logger = logger;
    }

    public Task StartPollingAsync(Guid connectionId, IDatabaseProvider provider, string connectionString, string? databaseName)
    {
        if (_activePollers.TryGetValue(connectionId, out var existingCts))
        {
            existingCts.Cancel();
            existingCts.Dispose();
            _activePollers.Remove(connectionId);
            _pollerTasks.Remove(connectionId);
        }

        var cts = new CancellationTokenSource();
        _activePollers[connectionId] = cts;
        _pollerTasks[connectionId] = PollLoopAsync(connectionId, provider, connectionString, databaseName, cts.Token);

        return Task.CompletedTask;
    }

    public async Task StopPollingAsync(Guid connectionId)
    {
        if (_activePollers.TryGetValue(connectionId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _activePollers.Remove(connectionId);
        }

        if (_pollerTasks.TryGetValue(connectionId, out var task))
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }

            _pollerTasks.Remove(connectionId);
        }
    }

    private async Task PollLoopAsync(Guid connectionId, IDatabaseProvider provider, string connectionString, string? databaseName, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_settings.PollingInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await PollConnectionAsync(connectionId, provider, connectionString, databaseName, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Error polling metrics for connection {ConnectionId}", connectionId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when polling is stopped
        }
    }

    public async Task PollConnectionAsync(Guid connectionId, IDatabaseProvider provider, string connectionString, string? databaseName, CancellationToken cancellationToken)
    {
        ConnectionPoolSnapshot? connectionPool = null;
        ServerHealthSnapshot? serverHealth = null;
        StorageSnapshot? storage = null;
        PerformanceSnapshot? performance = null;

        if (provider is IDatabaseConnectionMetrics connMetrics)
        {
            var activeConnections = await connMetrics.GetActiveConnectionCountAsync(connectionString, cancellationToken);
            var maxConnections = await connMetrics.GetMaxConnectionsAsync(connectionString, cancellationToken);
            connectionPool = new ConnectionPoolSnapshot(activeConnections, maxConnections);
        }

        if (provider is IDatabaseServerHealthMetrics healthMetrics)
        {
            var uptime = await healthMetrics.GetServerUptimeAsync(connectionString, cancellationToken);
            var version = await healthMetrics.GetServerVersionAsync(connectionString, cancellationToken);
            serverHealth = new ServerHealthSnapshot(uptime, version);
        }

        if (provider is IDatabaseStorageMetrics storageMetrics && databaseName is not null)
        {
            var dbSize = await storageMetrics.GetDatabaseSizeAsync(connectionString, databaseName, cancellationToken);
            var tableSizes = await storageMetrics.GetTableSizesAsync(connectionString, databaseName, cancellationToken);
            storage = new StorageSnapshot(dbSize, tableSizes);
        }

        if (provider is IDatabasePerformanceMetrics perfMetrics)
        {
            var cacheHitRatio = await perfMetrics.GetCacheHitRatioAsync(connectionString, cancellationToken);
            var activeQueries = await perfMetrics.GetActiveQueriesAsync(connectionString, cancellationToken);
            performance = new PerformanceSnapshot(cacheHitRatio, activeQueries);
        }

        await _bus.PublishAsync(new ServerMetricsCollected(
            connectionId,
            DateTime.UtcNow,
            connectionPool,
            serverHealth,
            storage,
            performance));
    }

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var cts in _activePollers.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _activePollers.Clear();
        _pollerTasks.Clear();
        _disposed = true;
    }
}
