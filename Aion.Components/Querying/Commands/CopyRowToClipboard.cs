namespace Aion.Components.Querying.Commands;

/// <summary>
/// Copy a single row to clipboard in the specified format.
/// </summary>
/// <param name="Row">The row data to copy</param>
/// <param name="Columns">Column names for ordering/headers</param>
/// <param name="Format">Output format: "Json" or "Csv"</param>
/// <param name="Headers">Column names for the CSV header, in the order of <paramref name="Columns"/>; the keys when null</param>
public record CopyRowToClipboard(
    Dictionary<string, object> Row,
    List<string> Columns,
    string Format = "Json",
    List<string>? Headers = null);
