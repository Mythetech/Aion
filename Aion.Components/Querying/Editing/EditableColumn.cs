using Aion.Contracts.Database;

namespace Aion.Components.Querying.Editing;

/// <summary>
/// How edit mode treats one column of the edited table.
/// </summary>
/// <param name="IsEditable">Whether the grid lets its cells change.</param>
/// <param name="ReadOnlyReason">Why the cells can't change, shown on the cell, or null when they can.</param>
public sealed record EditableColumn(bool IsEditable, string? ReadOnlyReason)
{
    public static EditableColumn For(string column, IReadOnlyList<ColumnInfo> metadata)
    {
        var info = metadata.FirstOrDefault(c => c.Name.Equals(column, StringComparison.OrdinalIgnoreCase));

        if (info is null)
        {
            return ReadOnly("Not a column of the edited table");
        }

        if (info.IsPrimaryKey)
        {
            return ReadOnly("Primary key columns identify the row, so they can't be edited");
        }

        if (info.IsIdentity)
        {
            return ReadOnly("The database generates this column's values");
        }

        return new EditableColumn(true, null);
    }

    private static EditableColumn ReadOnly(string reason) => new(false, reason);
}
