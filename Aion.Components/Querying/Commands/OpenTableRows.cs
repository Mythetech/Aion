namespace Aion.Components.Querying.Commands;

/// <summary>
/// Opens a new tab that reads the first rows of a table, with the engine's own row limit, and runs it.
/// </summary>
public record OpenTableRows(Guid ConnectionId, string DatabaseName, string Schema, string TableName, int Count = 1000);
