using Aion.Contracts.Connections;

namespace Aion.Components.Connections;

/// <summary>
/// Reads the ids of the side drawer's panels. A connection's panel carries the connection's id, and the
/// drawer also holds panels that are not connections (History, plugins), so the id tells the two kinds apart.
/// </summary>
public static class ConnectionPanelId
{
    /// <summary>
    /// Whether the panel belonged to a connection that is gone. A panel that was never a connection's
    /// loses nothing when the connections change.
    /// </summary>
    public static bool IsOrphaned(string? panelId, IReadOnlyCollection<ConnectionModel> connections) =>
        Guid.TryParse(panelId, out var connectionId) && connections.All(c => c.Id != connectionId);
}
