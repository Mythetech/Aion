namespace Aion.Components.Metrics.Events;

public record ClientQueryMetricRecorded(
    Guid ConnectionId,
    DateTime Timestamp,
    TimeSpan Duration,
    int RowCount,
    bool Success);
