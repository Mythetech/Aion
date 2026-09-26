namespace Aion.Components.Querying.Commands;

/// <summary>
/// Copy selected rows to clipboard in the specified format.
/// </summary>
/// <param name="Rows">The selected rows to copy</param>
/// <param name="Columns">Column names for ordering/headers</param>
/// <param name="Format">Output format: "Json" or "Csv"</param>
/// <param name="Headers">Column names for the CSV header, in the order of <paramref name="Columns"/>; the keys when null</param>
public record CopySelectedRowsToClipboard(
    List<Dictionary<string, object>> Rows,
    List<string> Columns,
    string Format = "Csv",
    List<string>? Headers = null);
