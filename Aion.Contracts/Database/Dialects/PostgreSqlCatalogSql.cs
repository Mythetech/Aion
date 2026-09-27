namespace Aion.Contracts.Database.Dialects;

/// <summary>
/// Catalog statements PostgreSQL servers and PGlite share, so the server's integration tests cover the in-browser
/// engine's reads too. Each takes the schema and table as SQL text: parameters such as <c>@schema</c> for the
/// server, string literals for PGlite, whose interop runs plain SQL.
/// </summary>
public static class PostgreSqlCatalogSql
{
    /// <summary>
    /// One row per column of the table, in column order. The primary key flag is a lookup per column rather than a
    /// join: constraint names repeat across schemas (every schema created from one script has its own
    /// <c>queue_pkey</c>), and joining on them listed a key column once for each of those schemas.
    /// numeric_precision is also filled for integer and floating types, in bits, so only numeric's declared precision
    /// is read. An identity column reports is_generated = 'NEVER'; only computed columns are 'ALWAYS'.
    /// </summary>
    public static string Columns(string schema, string table) => $"""
        SELECT
            c.column_name,
            c.data_type,
            c.is_nullable = 'YES' AS is_nullable,
            c.column_default,
            c.character_maximum_length,
            EXISTS (
                SELECT 1
                FROM pg_catalog.pg_constraint con
                JOIN pg_catalog.pg_class t ON t.oid = con.conrelid
                JOIN pg_catalog.pg_namespace n ON n.oid = t.relnamespace
                JOIN pg_catalog.pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = ANY (con.conkey)
                WHERE con.contype = 'p'
                    AND n.nspname = c.table_schema
                    AND t.relname = c.table_name
                    AND a.attname = c.column_name
            ) AS is_primary_key,
            CASE WHEN c.column_default LIKE 'nextval%' OR c.is_identity = 'YES' THEN true ELSE false END AS is_identity,
            c.udt_name,
            CASE WHEN c.data_type = 'numeric' THEN c.numeric_precision END AS numeric_precision,
            CASE WHEN c.data_type = 'numeric' THEN c.numeric_scale END AS numeric_scale,
            c.is_generated = 'ALWAYS' AS is_generated
        FROM information_schema.columns c
        WHERE c.table_schema = {schema}
            AND c.table_name = {table}
        ORDER BY c.ordinal_position
        """;

    /// <summary>
    /// One row per column of each foreign key on the table, paired with the column it references by position. The
    /// information_schema views pair them only by constraint name, which is unique per table rather than per schema,
    /// and listed every column of a composite key against every column it references.
    /// </summary>
    public static string ForeignKeys(string schema, string table) => $"""
        SELECT
            con.conname AS constraint_name,
            a.attname AS column_name,
            rt.relname AS referenced_table,
            ra.attname AS referenced_column,
            rn.nspname AS referenced_schema
        FROM pg_catalog.pg_constraint con
        JOIN pg_catalog.pg_class t ON t.oid = con.conrelid
        JOIN pg_catalog.pg_namespace n ON n.oid = t.relnamespace
        JOIN pg_catalog.pg_class rt ON rt.oid = con.confrelid
        JOIN pg_catalog.pg_namespace rn ON rn.oid = rt.relnamespace
        CROSS JOIN LATERAL unnest(con.conkey, con.confkey) WITH ORDINALITY AS k(attnum, referenced_attnum, ord)
        JOIN pg_catalog.pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = k.attnum
        JOIN pg_catalog.pg_attribute ra ON ra.attrelid = con.confrelid AND ra.attnum = k.referenced_attnum
        WHERE con.contype = 'f'
            AND n.nspname = {schema}
            AND t.relname = {table}
        ORDER BY con.conname, k.ord
        """;
}
