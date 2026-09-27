using Aion.Components.Scaffolding;
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
/// <param name="IsBoolean">
/// Whether the column holds flags, whatever the engine calls the type, so the words true and false typed into it
/// are written as a boolean rather than as text.
/// </param>
public sealed record EditableColumn(bool IsEditable, bool IsNullable, bool AcceptsEmptyText, string? ReadOnlyReason, bool IsBoolean = false)
{
    private static readonly string[] TextTypeMarkers = ["char", "text", "clob", "string", "sysname"];

    public static EditableColumn For(string column, IReadOnlyList<ColumnInfo> metadata, DatabaseType engine)
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

        if (info.IsGenerated)
        {
            return ReadOnly("The database computes this column's values");
        }

        var isBoolean = ColumnTypeShape.Of(info, engine).Family == ColumnTypeFamily.Boolean;
        return new EditableColumn(true, info.IsNullable, IsTextType(info.DataType), null, isBoolean);
    }

    // An untyped column, as SQLite allows, stores whatever it is given, empty text included. Only the name before
    // any arguments counts, since a MySQL enum lists its values there and one of them may read "text".
    private static bool IsTextType(string dataType)
    {
        if (string.IsNullOrWhiteSpace(dataType))
        {
            return true;
        }

        var open = dataType.IndexOf('(');
        var type = (open < 0 ? dataType : dataType[..open]).ToLowerInvariant();
        return TextTypeMarkers.Any(type.Contains);
    }

    private static EditableColumn ReadOnly(string reason) => new(false, false, false, reason);
}
