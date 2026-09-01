namespace Aion.Contracts.Metrics;

public interface IDatabaseConnectionMetrics
{
    Task<int> GetActiveConnectionCountAsync(string connectionString, CancellationToken cancellationToken = default);
    Task<int> GetMaxConnectionsAsync(string connectionString, CancellationToken cancellationToken = default);
}
