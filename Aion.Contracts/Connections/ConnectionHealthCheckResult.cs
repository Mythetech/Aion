namespace Aion.Contracts.Connections;

public record ConnectionHealthCheckResult(
    Guid ConnectionId,
    bool IsHealthy,
    DateTime CheckTime,
    TimeSpan? ResponseTime,
    string? ErrorMessage
);
