using System.Text.Json;
using Aion.Components.Metrics.Events;
using Aion.Components.Metrics.Settings;
using Aion.Contracts.Connections;

namespace Aion.Components.Metrics.Services;

public class MetricsState
{
    private readonly Dictionary<Guid, ConnectionMetricsData> _connections = new();
    private readonly MetricsSettings _settings;

    public event Action? StateChanged;

    public MetricsState(MetricsSettings settings)
    {
        _settings = settings;
    }

    public ConnectionMetricsData? GetConnectionData(Guid connectionId)
    {
        return _connections.GetValueOrDefault(connectionId);
    }

    public void InitializeConnection(Guid connectionId)
    {
        _connections[connectionId] = new ConnectionMetricsData(_settings.MaxDataPoints);
    }

    public void ClearConnection(Guid connectionId)
    {
        _connections.Remove(connectionId);
    }

    public void RecordQueryMetric(ClientQueryMetricRecorded metric)
    {
        var data = GetOrCreateConnectionData(metric.ConnectionId);
        data.AddQueryMetric(metric.Timestamp, metric.Duration, metric.RowCount, metric.Success);
        StateChanged?.Invoke();
    }

    public void RecordServerMetrics(ServerMetricsCollected metrics)
    {
        var data = GetOrCreateConnectionData(metrics.ConnectionId);
        data.UpdateServerMetrics(
            metrics.ConnectionPool,
            metrics.ServerHealth,
            metrics.Storage,
            metrics.Performance);
        StateChanged?.Invoke();
    }

    public void RecordHealthChange(Guid connectionId, ConnectionHealthStatus status)
    {
        var data = GetOrCreateConnectionData(connectionId);
        data.AddHealthStatus(status);
        StateChanged?.Invoke();
    }

    public string ExportAsJson(Guid connectionId)
    {
        var data = GetConnectionData(connectionId);
        if (data is null)
            return "{}";

        var summary = new
        {
            ExportedAt = DateTime.UtcNow,
            SessionUptime = data.SessionUptime.ToString(@"hh\:mm\:ss"),
            QueryCount = data.QueryCount,
            AverageQueryDurationMs = data.AverageQueryDuration.TotalMilliseconds,
            ErrorRate = data.ErrorRate,
            QueryHistory = data.QueriesOverTime.Select(q => new
            {
                q.Timestamp,
                DurationMs = q.Duration.TotalMilliseconds,
                q.RowCount,
                q.Success,
            }),
            HealthHistory = data.HealthHistory.Select(h => new
            {
                h.Timestamp,
                Status = h.Status.ToString(),
            }),
            LatestConnectionPool = data.LatestConnectionPool,
            LatestServerHealth = data.LatestServerHealth,
            LatestStorage = data.LatestStorage,
            LatestPerformance = data.LatestPerformance,
        };

        return JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
    }

    private ConnectionMetricsData GetOrCreateConnectionData(Guid connectionId)
    {
        if (!_connections.TryGetValue(connectionId, out var data))
        {
            data = new ConnectionMetricsData(_settings.MaxDataPoints);
            _connections[connectionId] = data;
        }
        return data;
    }
}
