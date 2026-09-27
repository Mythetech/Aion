using Aion.Contracts.Database;

namespace Aion.Web.Providers;

/// <summary>
/// One row of SQLite's PRAGMA table_xinfo. <see cref="PrimaryKeyPosition"/> is the column's 1-based
/// position in the primary key, or 0 when it is not part of it. <see cref="Hidden"/> is 0 for an ordinary
/// column, 1 for a hidden column of a virtual table, 2 for a VIRTUAL generated column and 3 for a STORED one.
/// </summary>
public sealed record SqliteTableInfoRow(string Name, string DeclaredType, bool NotNull, string? DefaultValue, int PrimaryKeyPosition, int Hidden);

public static class SqliteTableInfo
{
    private const int HiddenVirtualTableColumn = 1;
    private const int VirtualGeneratedColumn = 2;
    private const int StoredGeneratedColumn = 3;

    // PRAGMA table_info leaves generated columns out entirely, so the catalog reads table_xinfo instead. That also
    // lists a virtual table's hidden columns, which SELECT * skips and table_info never showed, so they stay out.
    public static List<ColumnInfo> ToColumns(IReadOnlyList<SqliteTableInfoRow> rows)
    {
        var hasSingleColumnKey = rows.Count(row => row.PrimaryKeyPosition > 0) == 1;

        return rows.Where(row => row.Hidden != HiddenVirtualTableColumn).Select(row => new ColumnInfo
        {
            Name = row.Name,
            DataType = row.DeclaredType,
            IsNullable = !row.NotNull && !(hasSingleColumnKey && IsRowidAlias(row)),
            DefaultValue = row.DefaultValue,
            IsPrimaryKey = row.PrimaryKeyPosition > 0,
            IsIdentity = false,
            IsGenerated = row.Hidden is VirtualGeneratedColumn or StoredGeneratedColumn
        }).ToList();
    }

    // PRAGMA table_info reports notnull = 0 for an INTEGER PRIMARY KEY, yet that column is the table's rowid and
    // SQLite fills in a value when NULL is inserted, so it never holds NULL. Only the exact type name INTEGER
    // makes the alias; INT or BIGINT primary keys, like other primary keys of a rowid table, really accept NULL.
    private static bool IsRowidAlias(SqliteTableInfoRow row) =>
        row.PrimaryKeyPosition > 0 && string.Equals(row.DeclaredType.Trim(), "INTEGER", StringComparison.OrdinalIgnoreCase);
}
