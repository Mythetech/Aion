namespace Aion.Components.Querying.Results;

public enum ResultSortDirection
{
    Ascending,
    Descending
}

/// <summary>
/// The column the results grid is sorted by, and which way.
/// </summary>
public sealed record ResultSort(string Key, ResultSortDirection Direction)
{
    /// <summary>
    /// The sort after a click on a column's header: ascending first, then descending, then back to the fetched order.
    /// </summary>
    public static ResultSort? Next(ResultSort? current, string key)
    {
        if (current is null || current.Key != key)
            return new ResultSort(key, ResultSortDirection.Ascending);

        return current.Direction == ResultSortDirection.Ascending
            ? current with { Direction = ResultSortDirection.Descending }
            : null;
    }
}
