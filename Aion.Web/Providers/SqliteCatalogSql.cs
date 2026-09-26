using System.Globalization;
using Aion.Contracts.Database.Dialects;

namespace Aion.Web.Providers;

/// <summary>
/// Catalog statements for the in-browser SQLite provider. Table names are user data, so every one is quoted as
/// an identifier with SQLite's own rules rather than pasted into the text.
/// </summary>
public static class SqliteCatalogSql
{
    // SQLite refuses a compound SELECT of more than 500 terms by default (SQLITE_MAX_COMPOUND_SELECT).
    public const int CountBatchSize = 400;

    public static string TableInfo(string table) => $"PRAGMA table_info({Quote(table)})";

    public static string ForeignKeyList(string table) => $"PRAGMA foreign_key_list({Quote(table)})";

    /// <summary>
    /// Statements that count the rows of every table, a batch of tables each. Each row holds the table's
    /// position in <paramref name="tables"/> and its count, so no table name has to be read back.
    /// </summary>
    public static List<string> CountRows(IReadOnlyList<string> tables) =>
        tables.Chunk(CountBatchSize)
            .Select((batch, index) => string.Join("\nUNION ALL\n", batch.Select((table, offset) =>
                $"SELECT {(index * CountBatchSize + offset).ToString(CultureInfo.InvariantCulture)}, COUNT(*) FROM {Quote(table)}")))
            .ToList();

    private static string Quote(string identifier) => SqliteDialect.Instance.QuoteIdentifier(identifier);
}
