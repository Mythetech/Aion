using Aion.Contracts.Database;

namespace Aion.Components.Querying.Editing;

/// <summary>
/// How edit mode treats one column of the edited table.
/// </summary>
/// <param name="IsEditable">Whether the grid lets its cells change.</param>
/// <param name="IsNullable">Whether a cell can be set to NULL.</param>
/// <param name="AcceptsEmptyText">
/// Whether empty text is a value of the column's type. Only text types take it; elsewhere engines reject it or
/// quietly turn it into 0 or a default date, so an empty editor means NULL there instead.
/// </param>
/// <param name="ReadOnlyReason">Why the cells can't change, shown on the cell, or null when they can.</param>
public sealed record EditableColumn(bool IsEditable, bool IsNullable, bool AcceptsEmptyText, string? ReadOnlyReason)
{
    private static readonly string[] TextTypeMarkers = ["char", "text", "clob", "string", "sysname"];

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

        return new EditableColumn(true, info.IsNullable, IsTextType(info.DataType), null);
    }

    // An untyped column, as SQLite allows, stores whatever it is given, empty text included.
    private static bool IsTextType(string dataType)
    {
        if (string.IsNullOrWhiteSpace(dataType))
        {
            return true;
        }

        var type = dataType.ToLowerInvariant();
        return TextTypeMarkers.Any(type.Contains);
    }

    private static EditableColumn ReadOnly(string reason) => new(false, false, false, reason);
}
