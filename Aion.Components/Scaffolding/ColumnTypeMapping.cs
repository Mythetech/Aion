using Aion.Contracts.Database;

namespace Aion.Components.Scaffolding;

/// <summary>
/// Carries a column's type across engines when the schema wizard's engine changes, so the user keeps their
/// columns instead of re-picking every type. Types are matched by the kind of value they hold, not by name:
/// SQLite's REAL is PostgreSQL's double precision, not its four-byte real.
/// </summary>
public static class ColumnTypeMapping
{
    private static readonly Dictionary<ColumnTypeFamily, string> SqliteTypes = new()
    {
        [ColumnTypeFamily.Integer] = "INTEGER",
        [ColumnTypeFamily.Boolean] = "INTEGER",
        [ColumnTypeFamily.Decimal] = "NUMERIC",
        [ColumnTypeFamily.Float] = "REAL",
        [ColumnTypeFamily.Text] = "TEXT",
        [ColumnTypeFamily.Date] = "TEXT",
        [ColumnTypeFamily.DateTime] = "TEXT",
        [ColumnTypeFamily.DateTimeOffset] = "TEXT",
        [ColumnTypeFamily.Time] = "TEXT",
        [ColumnTypeFamily.Interval] = "TEXT",
        [ColumnTypeFamily.Uuid] = "TEXT",
        [ColumnTypeFamily.Json] = "TEXT",
        [ColumnTypeFamily.Binary] = "BLOB"
    };

    private static readonly Dictionary<ColumnTypeFamily, string> PostgresTypes = new()
    {
        [ColumnTypeFamily.Integer] = "integer",
        [ColumnTypeFamily.Boolean] = "boolean",
        [ColumnTypeFamily.Decimal] = "numeric",
        [ColumnTypeFamily.Float] = "double precision",
        [ColumnTypeFamily.Text] = "text",
        [ColumnTypeFamily.Date] = "date",
        [ColumnTypeFamily.DateTime] = "timestamp",
        [ColumnTypeFamily.DateTimeOffset] = "timestamptz",
        [ColumnTypeFamily.Time] = "time",
        [ColumnTypeFamily.Interval] = "interval",
        [ColumnTypeFamily.Uuid] = "uuid",
        [ColumnTypeFamily.Json] = "jsonb",
        [ColumnTypeFamily.Binary] = "bytea"
    };

    /// <summary>
    /// The type in <paramref name="targetTypes"/> that holds the same kind of value as <paramref name="type"/>
    /// does on <paramref name="from"/>, or null when the target engine has nothing equivalent.
    /// </summary>
    public static string? Map(string type, DatabaseType from, DatabaseType to, IReadOnlyList<string> targetTypes)
    {
        if (targetTypes.Contains(type, StringComparer.Ordinal) && from == to)
            return type;

        var family = ColumnTypeShape.Of(type, from).Family;
        if (family is ColumnTypeFamily.Unknown or ColumnTypeFamily.RowVersion)
            return null;

        if (Preferred(to, family) is { } preferred && Offered(targetTypes, preferred) is { } offered)
            return offered;

        return targetTypes.FirstOrDefault(candidate => ColumnTypeShape.Of(candidate, to).Family == family);
    }

    private static string? Preferred(DatabaseType engine, ColumnTypeFamily family)
    {
        var types = engine switch
        {
            DatabaseType.WasmSQLite => SqliteTypes,
            DatabaseType.WasmPostgreSQL or DatabaseType.PostgreSQL => PostgresTypes,
            _ => null
        };

        return types?.GetValueOrDefault(family);
    }

    private static string? Offered(IReadOnlyList<string> targetTypes, string type) =>
        targetTypes.FirstOrDefault(candidate => string.Equals(candidate, type, StringComparison.OrdinalIgnoreCase));
}
