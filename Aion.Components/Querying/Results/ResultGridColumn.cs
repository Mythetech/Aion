using Aion.Contracts.Queries;

namespace Aion.Components.Querying.Results;

/// <summary>
/// How the results grid presents one column of a result, worked out once per result rather than per cell.
/// </summary>
/// <param name="Key">The key the column's values are stored under in each row.</param>
/// <param name="IsNumeric">Whether the column holds numbers, which the grid right-aligns.</param>
public sealed record ResultGridColumn(string Key, bool IsNumeric)
{
    // Enough rows to tell numbers from text without scanning a large result.
    private const int SampleSize = 100;

    public static IReadOnlyList<ResultGridColumn> From(QueryResult result) =>
        result.Columns
            .Select(key => new ResultGridColumn(key, ResultValueFormatter.IsNumericColumn(null, Sample(result, key))))
            .ToList();

    private static IEnumerable<object?> Sample(QueryResult result, string key) =>
        result.Rows.Take(SampleSize).Select(row => row.GetValueOrDefault(key));
}
