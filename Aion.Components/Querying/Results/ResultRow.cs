namespace Aion.Components.Querying.Results;

/// <summary>
/// A fetched row together with its place in <c>QueryResult.Rows</c>. Selection, edits and foreign key
/// lookups all identify rows by that place, so the grid carries it instead of searching for the row.
/// </summary>
public sealed class ResultRow
{
    public ResultRow(int index, Dictionary<string, object> values)
    {
        Index = index;
        Values = values;
    }

    public int Index { get; }

    public Dictionary<string, object> Values { get; }

    /// <summary>
    /// The text find in results searches, built the first time the row is searched.
    /// </summary>
    public string SearchText => _searchText ??= ResultRowFilter.SearchTextOf(Values);

    private string? _searchText;

    public static IReadOnlyList<ResultRow> Wrap(IReadOnlyList<Dictionary<string, object>> rows)
    {
        var wrapped = new ResultRow[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            wrapped[i] = new ResultRow(i, rows[i]);
        }

        return wrapped;
    }
}
