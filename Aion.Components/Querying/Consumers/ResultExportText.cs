namespace Aion.Components.Querying.Consumers;

/// <summary>
/// The notification after exporting results, which says when only the rows find in results kept were exported.
/// </summary>
public static class ResultExportText
{
    public static string Exported(int exportedRows, int? totalRows, string fileName) =>
        totalRows is { } total && total != exportedRows
            ? $"Exported {exportedRows:N0} of {total:N0} rows to {fileName}"
            : $"Exported results to {fileName}";
}
