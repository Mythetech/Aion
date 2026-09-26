namespace Aion.Components.Querying.Results;

/// <summary>
/// The rows the results grid shows, in the order it shows them. It is built once whenever the result, the
/// find-in-results text or the sort changes, so rendering a row never has to search the whole result.
/// </summary>
public sealed class ResultGridView
{
    private int[]? _order;
    private int[]? _positions;

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

    /// <summary>
    /// Each shown row's place in the result, from top to bottom, which is what selection ranges follow.
    /// </summary>
    public IReadOnlyList<int> Order => _order ??= Rows.Select(row => row.Index).ToArray();

    /// <summary>
    /// Where a result row appears in the grid, counting from 0, or -1 when it is not shown.
    /// </summary>
    public int PositionOf(int rowIndex)
    {
        _positions ??= BuildPositions();
        return rowIndex >= 0 && rowIndex < _positions.Length ? _positions[rowIndex] : -1;
    }

    private int[] BuildPositions()
    {
        var positions = new int[Rows.Count == 0 ? 0 : Rows.Max(row => row.Index) + 1];
        Array.Fill(positions, -1);
        for (var position = 0; position < Rows.Count; position++)
        {
            positions[Rows[position].Index] = position;
        }

        return positions;
    }

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
            if (row.SearchText.Contains(filter, StringComparison.OrdinalIgnoreCase))
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
