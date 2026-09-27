namespace Aion.Components.Connections.Commands;

/// <summary>
/// Asks for the password of a connection that is waiting for one.
/// </summary>
public record PromptConnectionPassword(Guid ConnectionId);
