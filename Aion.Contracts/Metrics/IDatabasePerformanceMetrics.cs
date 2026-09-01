namespace Aion.Contracts.Metrics;

public interface IDatabasePerformanceMetrics
{
    Task<double?> GetCacheHitRatioAsync(string connectionString, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SlowQueryInfo>> GetSlowQueriesAsync(string connectionString, TimeSpan threshold, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActiveQueryInfo>> GetActiveQueriesAsync(string connectionString, CancellationToken cancellationToken = default);
}
