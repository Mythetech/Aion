using Aion.Core.Queries.Editing;

namespace Aion.Components.Querying.Editing;

/// <summary>
/// Manages the state of pending changes during edit mode.
/// </summary>
public class EditState
{
    private readonly List<PendingChange> _pendingChanges = [];
    private readonly object _lock = new();

    /// <summary>
    /// Event fired when edit state changes.
    /// </summary>
    public event Action? StateChanged;

    /// <summary>
    /// Whether edit mode is active.
    /// </summary>
    public bool IsEditMode { get; set; }

    /// <summary>
    /// All pending changes.
    /// </summary>
    public IReadOnlyList<PendingChange> PendingChanges => _pendingChanges.AsReadOnly();

    /// <summary>
    /// Count of inserted rows.
    /// </summary>
    public int InsertedRowCount => _pendingChanges.Count(c => c.Type == ChangeType.Insert);

    /// <summary>
    /// Count of updated rows.
    /// </summary>
    public int UpdatedRowCount => _pendingChanges.Count(c => c.Type == ChangeType.Update);

    /// <summary>
    /// Count of deleted rows.
    /// </summary>
    public int DeletedRowCount => _pendingChanges.Count(c => c.Type == ChangeType.Delete);

    /// <summary>
    /// Whether there are any pending changes.
    /// </summary>
    public bool HasChanges => _pendingChanges.Count > 0;

    /// <summary>
    /// Total number of pending changes.
    /// </summary>
    public int TotalChangeCount => _pendingChanges.Count;

    /// <summary>
    /// Add a new pending change.
    /// </summary>
    public void AddChange(PendingChange change)
    {
        lock (_lock)
        {
            // For updates, check if there's already a change for this row
            if (change.Type == ChangeType.Update)
            {
                var existing = _pendingChanges.FirstOrDefault(c =>
                    c.RowIndex == change.RowIndex && c.Type == ChangeType.Update);

                if (existing != null)
                {
                    _pendingChanges.Remove(existing);
                }
            }

            _pendingChanges.Add(change);
        }

        OnStateChanged();
    }

    /// <summary>
    /// Remove a pending change by ID.
    /// </summary>
    public void RemoveChange(Guid changeId)
    {
        lock (_lock)
        {
            var change = _pendingChanges.FirstOrDefault(c => c.Id == changeId);
            if (change != null)
            {
                _pendingChanges.Remove(change);
            }
        }

        OnStateChanged();
    }

    /// <summary>
    /// Discard all pending changes.
    /// </summary>
    public void DiscardAllChanges()
    {
        lock (_lock)
        {
            _pendingChanges.Clear();
        }

        OnStateChanged();
    }

    /// <summary>
    /// Check if a row is modified.
    /// </summary>
    public bool IsRowModified(int rowIndex)
    {
        return _pendingChanges.Any(c => c.RowIndex == rowIndex && c.Type == ChangeType.Update);
    }

    /// <summary>
    /// Check if a row is marked for deletion.
    /// </summary>
    public bool IsRowDeleted(int rowIndex)
    {
        return _pendingChanges.Any(c => c.RowIndex == rowIndex && c.Type == ChangeType.Delete);
    }

    /// <summary>
    /// Check if a row is newly inserted.
    /// </summary>
    public bool IsRowInserted(int rowIndex)
    {
        return _pendingChanges.Any(c => c.RowIndex == rowIndex && c.Type == ChangeType.Insert);
    }

    /// <summary>
    /// Check if a specific cell is modified.
    /// </summary>
    public bool IsCellModified(int rowIndex, string column)
    {
        var change = GetChangeForRow(rowIndex);
        if (change == null || change.Type != ChangeType.Update)
            return false;

        return change.GetModifiedColumns().Contains(column);
    }

    /// <summary>
    /// Get the change for a specific row.
    /// </summary>
    public PendingChange? GetChangeForRow(int rowIndex)
    {
        return _pendingChanges.FirstOrDefault(c => c.RowIndex == rowIndex);
    }

    /// <summary>
    /// Get the effective value for a cell (considering pending changes).
    /// </summary>
    public object? GetEffectiveValue(int rowIndex, string column, Dictionary<string, object> originalRow)
    {
        var change = GetChangeForRow(rowIndex);

        if (change?.Type == ChangeType.Update && change.NewValues != null)
        {
            if (change.NewValues.TryGetValue(column, out var newValue))
            {
                return newValue;
            }
        }
        else if (change?.Type == ChangeType.Insert && change.NewValues != null)
        {
            if (change.NewValues.TryGetValue(column, out var newValue))
            {
                return newValue;
            }
        }

        return originalRow.GetValueOrDefault(column);
    }

    /// <summary>
    /// Get the effective row (considering pending changes).
    /// </summary>
    public Dictionary<string, object?> GetEffectiveRow(int rowIndex, Dictionary<string, object> originalRow)
    {
        var result = new Dictionary<string, object?>(originalRow.Count);

        foreach (var kvp in originalRow)
        {
            result[kvp.Key] = GetEffectiveValue(rowIndex, kvp.Key, originalRow);
        }

        return result;
    }

    /// <summary>
    /// Update a cell value.
    /// </summary>
    public void UpdateCell(int rowIndex, string column, object? newValue, Dictionary<string, object> originalRow)
    {
        var existingChange = GetChangeForRow(rowIndex);

        if (existingChange?.Type == ChangeType.Insert)
        {
            // Update the insert change
            var newValues = new Dictionary<string, object?>(existingChange.NewValues ?? []);
            newValues[column] = newValue;

            RemoveChange(existingChange.Id);
            AddChange(PendingChange.CreateInsert(rowIndex, newValues));
        }
        else if (existingChange?.Type == ChangeType.Update)
        {
            // Merge with existing update
            var newValues = new Dictionary<string, object?>(existingChange.NewValues ?? []);
            newValues[column] = newValue;

            // Check if we've reverted to original
            var originalValues = new Dictionary<string, object?>(
                originalRow.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value));

            RemoveChange(existingChange.Id);

            // Only add if there are still differences
            if (newValues.Any(kvp => !Equals(kvp.Value, originalValues.GetValueOrDefault(kvp.Key))))
            {
                AddChange(PendingChange.CreateUpdate(rowIndex, originalValues, newValues));
            }
        }
        else if (existingChange?.Type != ChangeType.Delete)
        {
            // Create new update change
            var originalValues = new Dictionary<string, object?>(
                originalRow.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value));
            var newValues = new Dictionary<string, object?>(originalValues) { [column] = newValue };

            // Only add if different from original
            if (!Equals(newValue, originalRow.GetValueOrDefault(column)))
            {
                AddChange(PendingChange.CreateUpdate(rowIndex, originalValues, newValues));
            }
        }
    }

    /// <summary>
    /// Mark a row for deletion.
    /// </summary>
    public void DeleteRow(int rowIndex, Dictionary<string, object> originalRow)
    {
        var existingChange = GetChangeForRow(rowIndex);

        if (existingChange?.Type == ChangeType.Insert)
        {
            // Just remove the insert - it was never committed
            RemoveChange(existingChange.Id);
            return;
        }

        if (existingChange != null)
        {
            RemoveChange(existingChange.Id);
        }

        var originalValues = new Dictionary<string, object?>(
            originalRow.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value));
        AddChange(PendingChange.CreateDelete(rowIndex, originalValues));
    }

    /// <summary>
    /// Undelete a row (remove delete change).
    /// </summary>
    public void UndeleteRow(int rowIndex)
    {
        var change = _pendingChanges.FirstOrDefault(c =>
            c.RowIndex == rowIndex && c.Type == ChangeType.Delete);

        if (change != null)
        {
            RemoveChange(change.Id);
        }
    }

    private void OnStateChanged() => StateChanged?.Invoke();
}
