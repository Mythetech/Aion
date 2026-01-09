namespace Aion.Components.Querying.Commands;

/// <summary>
/// Export selected rows to a file in the specified format.
/// </summary>
/// <param name="Rows">The selected rows to export</param>
/// <param name="Columns">Column names for ordering/headers</param>
/// <param name="Format">Output format: "Csv", "Json", or "Excel"</param>
public record ExportSelectedRows(
    List<Dictionary<string, object>> Rows,
    List<string> Columns,
    string Format);
