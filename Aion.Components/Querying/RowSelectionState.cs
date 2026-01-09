namespace Aion.Components.Querying;

/// <summary>
/// Tracks row selection state for the query result table.
/// Supports single selection, Ctrl+click toggle, and Shift+click range selection.
/// </summary>
public class RowSelectionState
{
    public HashSet<int> SelectedIndices { get; } = [];
    public int? LastClickedIndex { get; set; }
    public bool SelectAllChecked { get; set; }

    public event Action? SelectionChanged;

    /// <summary>
    /// Toggle selection for a single row, with support for Ctrl and Shift modifiers.
    /// </summary>
    public void ToggleSelection(int index, bool ctrlKey, bool shiftKey, int totalRows)
    {
        if (shiftKey && LastClickedIndex.HasValue)
        {
            // Range selection
            var start = Math.Min(LastClickedIndex.Value, index);
            var end = Math.Max(LastClickedIndex.Value, index);

            if (!ctrlKey)
                SelectedIndices.Clear();

            for (var i = start; i <= end; i++)
            {
                SelectedIndices.Add(i);
            }
        }
        else if (ctrlKey)
        {
            // Toggle individual selection
            if (!SelectedIndices.Remove(index))
            {
                SelectedIndices.Add(index);
            }
        }
        else
        {
            // Single selection (replace)
            SelectedIndices.Clear();
            SelectedIndices.Add(index);
        }

        LastClickedIndex = index;
        UpdateSelectAllState(totalRows);
        OnSelectionChanged();
    }

    /// <summary>
    /// Select all rows.
    /// </summary>
    public void SelectAll(int totalRows)
    {
        SelectedIndices.Clear();
        for (var i = 0; i < totalRows; i++)
        {
            SelectedIndices.Add(i);
        }
        SelectAllChecked = true;
        OnSelectionChanged();
    }

    /// <summary>
    /// Clear all selections.
    /// </summary>
    public void ClearSelection()
    {
        SelectedIndices.Clear();
        LastClickedIndex = null;
        SelectAllChecked = false;
        OnSelectionChanged();
    }

    /// <summary>
    /// Check if a row is selected.
    /// </summary>
    public bool IsSelected(int index) => SelectedIndices.Contains(index);

    /// <summary>
    /// Get selected rows from the data source.
    /// </summary>
    public List<Dictionary<string, object>> GetSelectedRows(List<Dictionary<string, object>> allRows)
    {
        return SelectedIndices
            .Where(i => i >= 0 && i < allRows.Count)
            .OrderBy(i => i)
            .Select(i => allRows[i])
            .ToList();
    }

    /// <summary>
    /// Number of selected rows.
    /// </summary>
    public int SelectedCount => SelectedIndices.Count;

    /// <summary>
    /// Whether any rows are selected.
    /// </summary>
    public bool HasSelection => SelectedIndices.Count > 0;

    private void UpdateSelectAllState(int totalRows)
    {
        SelectAllChecked = SelectedIndices.Count == totalRows && totalRows > 0;
    }

    private void OnSelectionChanged() => SelectionChanged?.Invoke();
}
