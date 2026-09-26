namespace Aion.Components.Querying.Results;

/// <summary>
/// The rows the results grid shows, in the order it shows them. It is built once whenever the result, the
/// find-in-results text or the sort changes, so rendering a row never has to search the whole result.
/// </summary>
public sealed class ResultGridView
{
    private ResultGridView(IReadOnlyList<ResultRow> rows, bool isFiltered)
    {
        Rows = rows;
        IsFiltered = isFiltered;
    }

    public static ResultGridView Empty { get; } = new([], false);

    public IReadOnlyList<ResultRow> Rows { get; }

    public int Count => Rows.Count;

    /// <summary>
    /// Whether find-in-results text narrowed the rows, so counts should say "matching".
    /// </summary>
    public bool IsFiltered { get; }

    public static ResultGridView Build(IReadOnlyList<ResultRow> rows, string? filter, ResultSort? sort = null)
    {
        var isFiltered = !string.IsNullOrEmpty(filter);
        var shown = isFiltered ? Filter(rows, filter!) : rows;

        if (sort != null)
            shown = ResultSorter.Sort(shown, sort);

        return new ResultGridView(shown, isFiltered);
    }

    private static List<ResultRow> Filter(IReadOnlyList<ResultRow> rows, string filter)
    {
        var matching = new List<ResultRow>();
        foreach (var row in rows)
        {
            if (ResultRowFilter.Matches(row.Values, filter))
                matching.Add(row);
        }

        return matching;
    }

    public IReadOnlyList<ResultRow> Take(int limit)
    {
        if (Rows.Count <= limit)
            return Rows;

        var taken = new ResultRow[limit];
        for (var i = 0; i < limit; i++)
        {
            taken[i] = Rows[i];
        }

        return taken;
    }
}
