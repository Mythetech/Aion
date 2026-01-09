namespace Aion.Components.Connections.Commands;

/// <summary>
/// Command to refresh connection health. Pass null ConnectionId to refresh all connections.
/// </summary>
public record RefreshConnectionHealth(Guid? ConnectionId = null);
