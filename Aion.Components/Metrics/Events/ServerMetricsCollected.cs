using Aion.Contracts.Metrics;

namespace Aion.Components.Metrics.Events;

public record ServerMetricsCollected(
    Guid ConnectionId,
    DateTime Timestamp,
    ConnectionPoolSnapshot? ConnectionPool,
    ServerHealthSnapshot? ServerHealth,
    StorageSnapshot? Storage,
    PerformanceSnapshot? Performance);
