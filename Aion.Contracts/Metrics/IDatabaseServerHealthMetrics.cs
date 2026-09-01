namespace Aion.Contracts.Metrics;

public interface IDatabaseServerHealthMetrics
{
    Task<TimeSpan> GetServerUptimeAsync(string connectionString, CancellationToken cancellationToken = default);
    Task<string> GetServerVersionAsync(string connectionString, CancellationToken cancellationToken = default);
}
