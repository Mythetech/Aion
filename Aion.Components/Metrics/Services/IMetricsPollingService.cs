using Aion.Contracts.Database;

namespace Aion.Components.Metrics.Services;

public interface IMetricsPollingService : IDisposable
{
    Task StartPollingAsync(Guid connectionId, IDatabaseProvider provider, string connectionString, string? databaseName);
    Task StopPollingAsync(Guid connectionId);
    Task PollConnectionAsync(Guid connectionId, IDatabaseProvider provider, string connectionString, string? databaseName, CancellationToken cancellationToken);
}
