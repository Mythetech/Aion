using Aion.Contracts.Connections;
using Aion.Contracts.Metrics;

namespace Aion.Components.Metrics.Services;

public class ConnectionMetricsData
{
    private readonly BoundedQueue<QueryMetricPoint> _queryMetrics;
    private readonly BoundedQueue<HealthMetricPoint> _healthHistory;
    private readonly BoundedQueue<ConnectionPoolSnapshot> _connectionPoolHistory;
    private int _totalQueries;
    private int _failedQueries;
    private double _totalDurationMs;

    public DateTime InitializedAt { get; } = DateTime.UtcNow;
    public int QueryCount => _totalQueries;
    public double ErrorRate => _totalQueries == 0 ? 0 : (double)_failedQueries / _totalQueries;
    public TimeSpan AverageQueryDuration => _totalQueries == 0
        ? TimeSpan.Zero
        : TimeSpan.FromMilliseconds(_totalDurationMs / _totalQueries);
    public TimeSpan SessionUptime => DateTime.UtcNow - InitializedAt;
    public List<QueryMetricPoint> QueriesOverTime => _queryMetrics.ToList();
    public List<HealthMetricPoint> HealthHistory => _healthHistory.ToList();
    public List<ConnectionPoolSnapshot> ConnectionPoolHistory => _connectionPoolHistory.ToList();
    public ConnectionPoolSnapshot? LatestConnectionPool { get; private set; }
    public ServerHealthSnapshot? LatestServerHealth { get; private set; }
    public StorageSnapshot? LatestStorage { get; private set; }
    public PerformanceSnapshot? LatestPerformance { get; private set; }

    public ConnectionMetricsData(int maxDataPoints)
    {
        _queryMetrics = new BoundedQueue<QueryMetricPoint>(maxDataPoints);
        _healthHistory = new BoundedQueue<HealthMetricPoint>(maxDataPoints);
        _connectionPoolHistory = new BoundedQueue<ConnectionPoolSnapshot>(maxDataPoints);
    }

    public void AddQueryMetric(DateTime timestamp, TimeSpan duration, int rowCount, bool success)
    {
        _queryMetrics.Enqueue(new QueryMetricPoint(timestamp, duration, rowCount, success));
        _totalQueries++;
        _totalDurationMs += duration.TotalMilliseconds;
        if (!success) _failedQueries++;
    }

    public void UpdateServerMetrics(
        ConnectionPoolSnapshot? connectionPool,
        ServerHealthSnapshot? serverHealth,
        StorageSnapshot? storage,
        PerformanceSnapshot? performance)
    {
        if (connectionPool is not null)
        {
            LatestConnectionPool = connectionPool;
            _connectionPoolHistory.Enqueue(connectionPool);
        }
        if (serverHealth is not null) LatestServerHealth = serverHealth;
        if (storage is not null) LatestStorage = storage;
        if (performance is not null) LatestPerformance = performance;
    }

    public void AddHealthStatus(ConnectionHealthStatus status)
    {
        _healthHistory.Enqueue(new HealthMetricPoint(DateTime.UtcNow, status));
    }
}

public record QueryMetricPoint(DateTime Timestamp, TimeSpan Duration, int RowCount, bool Success);

public record HealthMetricPoint(DateTime Timestamp, ConnectionHealthStatus Status);
