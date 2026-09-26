namespace Aion.Components.Querying;

/// <summary>
/// Tracks which result rows are selected in the query result table. Rows are identified by their place in
/// <c>QueryResult.Rows</c>, so a selection survives sorting and filtering. Ranges follow the order the grid
/// shows the rows in, which callers pass as the row places from top to bottom.
/// </summary>
public class RowSelectionState
{
    public HashSet<int> SelectedIndices { get; } = [];

    /// <summary>
    /// The row a Shift click extends a range from.
    /// </summary>
    public int? LastClickedIndex { get; private set; }

    public event Action? SelectionChanged;

    /// <summary>
    /// A click on a row's cell: selects only that row, Ctrl toggles it and keeps the rest, and Shift selects
    /// the rows shown between the last clicked row and this one.
    /// </summary>
    public void ToggleSelection(int index, bool ctrlKey, bool shiftKey, IReadOnlyList<int> order)
    {
        var range = shiftKey ? RangeTo(index, order) : null;
        if (range != null)
        {
            if (!ctrlKey)
                SelectedIndices.Clear();

            SelectedIndices.UnionWith(range);
        }
        else if (ctrlKey)
        {
            Toggle(index);
        }
        else
        {
            SelectedIndices.Clear();
            SelectedIndices.Add(index);
        }

        LastClickedIndex = index;
        OnSelectionChanged();
    }

    /// <summary>
    /// A click on a row's number: toggles that row and keeps the rest, and Shift adds the rows shown between
    /// the last clicked row and this one.
    /// </summary>
    public void ToggleRow(int index, bool shiftKey, IReadOnlyList<int> order)
    {
        var range = shiftKey ? RangeTo(index, order) : null;
        if (range != null)
        {
            SelectedIndices.UnionWith(range);
        }
        else
        {
            Toggle(index);
        }

        LastClickedIndex = index;
        OnSelectionChanged();
    }

    /// <summary>
    /// Selects exactly the given rows.
    /// </summary>
    public void SelectAll(IEnumerable<int> indices)
    {
        SelectedIndices.Clear();
        SelectedIndices.UnionWith(indices);
        OnSelectionChanged();
    }

    public void ClearSelection()
    {
        SelectedIndices.Clear();
        LastClickedIndex = null;
        OnSelectionChanged();
    }

    public bool IsSelected(int index) => SelectedIndices.Contains(index);

    /// <summary>
    /// Whether every one of the given rows is selected, and there is at least one.
    /// </summary>
    public bool AreAllSelected(IReadOnlyCollection<int> indices)
    {
        if (indices.Count == 0 || SelectedIndices.Count < indices.Count)
            return false;

        foreach (var index in indices)
        {
            if (!SelectedIndices.Contains(index))
                return false;
        }

        return true;
    }

    /// <summary>
    /// The selected rows in the order they were fetched.
    /// </summary>
    public List<Dictionary<string, object>> GetSelectedRows(List<Dictionary<string, object>> allRows)
    {
        return SelectedIndices
            .Where(i => i >= 0 && i < allRows.Count)
            .OrderBy(i => i)
            .Select(i => allRows[i])
            .ToList();
    }

    public int SelectedCount => SelectedIndices.Count;

    public bool HasSelection => SelectedIndices.Count > 0;

    private void Toggle(int index)
    {
        if (!SelectedIndices.Remove(index))
            SelectedIndices.Add(index);
    }

    // Null when there is no row to extend from among the rows shown, so the click acts on the one row.
    private List<int>? RangeTo(int index, IReadOnlyList<int> order)
    {
        if (LastClickedIndex is not { } anchor)
            return null;

        var from = PositionOf(anchor, order);
        var to = PositionOf(index, order);
        if (from < 0 || to < 0)
            return null;

        var range = new List<int>(Math.Abs(to - from) + 1);
        for (var position = Math.Min(from, to); position <= Math.Max(from, to); position++)
        {
            range.Add(order[position]);
        }

        return range;
    }

    private static int PositionOf(int index, IReadOnlyList<int> order)
    {
        for (var position = 0; position < order.Count; position++)
        {
            if (order[position] == index)
                return position;
        }

        return -1;
    }

    private void OnSelectionChanged() => SelectionChanged?.Invoke();
}
