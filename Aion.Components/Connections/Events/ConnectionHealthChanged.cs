using Aion.Core.Connections;

namespace Aion.Components.Connections.Events;

public record ConnectionHealthChanged(
    Guid ConnectionId,
    ConnectionHealthStatus NewStatus,
    ConnectionHealthStatus OldStatus,
    string? ErrorMessage = null
);
