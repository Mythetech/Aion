namespace Aion.Components.CommandPalette.Commands;

/// <summary>
/// Opens the command palette from places the page-level hotkey can't reach, such as the query editor,
/// which handles its own keys before the page sees them.
/// </summary>
public record OpenCommandPalette;
