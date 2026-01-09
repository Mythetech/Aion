namespace Aion.Core.Queries.Editing;

/// <summary>
/// Represents a pending change to a row in the query result.
/// </summary>
public record PendingChange
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public ChangeType Type { get; init; }
    public int RowIndex { get; init; }
    public Dictionary<string, object?> OriginalValues { get; init; } = [];
    public Dictionary<string, object?>? NewValues { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Get the columns that were modified in this change.
    /// </summary>
    public IEnumerable<string> GetModifiedColumns()
    {
        if (Type != ChangeType.Update || NewValues == null)
            return [];

        return NewValues.Keys.Where(key =>
        {
            var original = OriginalValues.GetValueOrDefault(key);
            var updated = NewValues.GetValueOrDefault(key);
            return !Equals(original, updated);
        });
    }

    /// <summary>
    /// Create an insert change for a new row.
    /// </summary>
    public static PendingChange CreateInsert(int rowIndex, Dictionary<string, object?> values)
    {
        return new PendingChange
        {
            Type = ChangeType.Insert,
            RowIndex = rowIndex,
            OriginalValues = [],
            NewValues = values
        };
    }

    /// <summary>
    /// Create an update change for an existing row.
    /// </summary>
    public static PendingChange CreateUpdate(int rowIndex, Dictionary<string, object?> originalValues, Dictionary<string, object?> newValues)
    {
        return new PendingChange
        {
            Type = ChangeType.Update,
            RowIndex = rowIndex,
            OriginalValues = originalValues,
            NewValues = newValues
        };
    }

    /// <summary>
    /// Create a delete change for an existing row.
    /// </summary>
    public static PendingChange CreateDelete(int rowIndex, Dictionary<string, object?> originalValues)
    {
        return new PendingChange
        {
            Type = ChangeType.Delete,
            RowIndex = rowIndex,
            OriginalValues = originalValues
        };
    }
}
