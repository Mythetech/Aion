namespace Aion.Components.History.Commands;

/// <summary>
/// Opens a history entry's SQL in a new query tab on the connection and database it ran against,
/// and runs it straight away when <paramref name="Run"/> is set.
/// </summary>
public record OpenHistoryEntry(QueryHistoryEntry Entry, bool Run = false);
