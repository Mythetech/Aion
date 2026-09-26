namespace Aion.Components.Querying.Commands;

/// <summary>Moves the editor's cursor to a 1-based line and column of a query tab's SQL and focuses it.</summary>
public record GoToQueryPosition(Guid QueryId, int Line, int Column);
