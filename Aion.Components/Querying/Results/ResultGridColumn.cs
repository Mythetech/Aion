using Aion.Components.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;

namespace Aion.Components.Querying.Results;

/// <summary>
/// How the results grid presents one column of a result, worked out once per result rather than per cell.
/// </summary>
/// <param name="Key">The key the column's values are stored under in each row.</param>
/// <param name="Name">The column's name as the database gave it, which can repeat or be empty.</param>
/// <param name="Type">The column's type as the provider named it, or null when it could not tell.</param>
/// <param name="TypeLabel">The short lowercase type shown under the column name, empty when unknown.</param>
/// <param name="IsNumeric">Whether the column holds numbers, which the grid right-aligns.</param>
public sealed record ResultGridColumn(string Key, string Name, string? Type, string TypeLabel, bool IsNumeric)
{
    /// <summary>
    /// The header text: the name, or what SQL Server tools show for a column the query left unnamed.
    /// </summary>
    public string Header => Name.Length == 0 ? "(No column name)" : Name;

    // Enough rows to tell numbers from text without scanning a large result.
    private const int SampleSize = 100;

    public static IReadOnlyList<ResultGridColumn> From(QueryResult result, DatabaseType engine) =>
        result.Columns
            .Select((key, ordinal) =>
            {
                var type = result.ColumnType(ordinal);
                return new ResultGridColumn(
                    key,
                    result.ColumnName(ordinal),
                    type,
                    ColumnTypeText.Short(type, engine),
                    ResultValueFormatter.IsNumericColumn(type, Sample(result, key)));
            })
            .ToList();

    private static IEnumerable<object?> Sample(QueryResult result, string key) =>
        result.Rows.Take(SampleSize).Select(row => row.GetValueOrDefault(key));
}
