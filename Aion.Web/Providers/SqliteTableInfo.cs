using Aion.Contracts.Database;

namespace Aion.Web.Providers;

/// <summary>
/// One row of SQLite's PRAGMA table_info. <see cref="PrimaryKeyPosition"/> is the column's 1-based
/// position in the primary key, or 0 when it is not part of it.
/// </summary>
public sealed record SqliteTableInfoRow(string Name, string DeclaredType, bool NotNull, string? DefaultValue, int PrimaryKeyPosition);

public static class SqliteTableInfo
{
    public static List<ColumnInfo> ToColumns(IReadOnlyList<SqliteTableInfoRow> rows)
    {
        var hasSingleColumnKey = rows.Count(row => row.PrimaryKeyPosition > 0) == 1;

        return rows.Select(row => new ColumnInfo
        {
            Name = row.Name,
            DataType = row.DeclaredType,
            IsNullable = !row.NotNull && !(hasSingleColumnKey && IsRowidAlias(row)),
            DefaultValue = row.DefaultValue,
            IsPrimaryKey = row.PrimaryKeyPosition > 0,
            IsIdentity = false
        }).ToList();
    }

    // PRAGMA table_info reports notnull = 0 for an INTEGER PRIMARY KEY, yet that column is the table's rowid and
    // SQLite fills in a value when NULL is inserted, so it never holds NULL. Only the exact type name INTEGER
    // makes the alias; INT or BIGINT primary keys, like other primary keys of a rowid table, really accept NULL.
    private static bool IsRowidAlias(SqliteTableInfoRow row) =>
        row.PrimaryKeyPosition > 0 && string.Equals(row.DeclaredType.Trim(), "INTEGER", StringComparison.OrdinalIgnoreCase);
}
