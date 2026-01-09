namespace Aion.Components.Querying.Commands;

/// <summary>
/// Copy selected rows to clipboard in the specified format.
/// </summary>
/// <param name="Rows">The selected rows to copy</param>
/// <param name="Columns">Column names for ordering/headers</param>
/// <param name="Format">Output format: "Json" or "Csv"</param>
public record CopySelectedRowsToClipboard(
    List<Dictionary<string, object>> Rows,
    List<string> Columns,
    string Format = "Csv");
