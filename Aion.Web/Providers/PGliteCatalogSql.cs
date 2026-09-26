using System.Globalization;
using Aion.Contracts.Database;
using Aion.Contracts.Database.Dialects;

namespace Aion.Web.Providers;

/// <summary>
/// Catalog statements for the PGlite provider. The interop runs plain SQL text, so names of tables and schemas
/// go in as quoted identifiers or string literals under PostgreSQL's rules instead of being pasted in.
/// </summary>
public static class PGliteCatalogSql
{
    /// <summary>
    /// One statement counting the rows of every table. Each row holds the table's position in
    /// <paramref name="tables"/> as <c>i</c> and its count as <c>n</c>.
    /// </summary>
    public static string CountRows(IReadOnlyList<TableInfo> tables) =>
        string.Join("\nUNION ALL\n", tables.Select((table, index) =>
            $"SELECT {index.ToString(CultureInfo.InvariantCulture)} AS i, count(*) AS n FROM {Dialect.QualifyTable(table.Schema, table.Name)}"));

    /// <summary>
    /// A name as a string literal, for catalog columns such as information_schema.columns.table_name.
    /// </summary>
    public static string Literal(string name) => Dialect.FormatLiteral(name);

    private static PostgreSqlDialect Dialect => PostgreSqlDialect.Instance;
}
