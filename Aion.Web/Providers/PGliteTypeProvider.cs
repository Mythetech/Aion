using Aion.Contracts.Database;

namespace Aion.Web.Providers;

public class PGliteTypeProvider : ISupportedTypeProvider
{
    public DatabaseType DatabaseType => DatabaseType.WasmPostgreSQL;

    public IReadOnlyList<string> SupportedTypes { get; } =
    [
        "integer",
        "bigint",
        "smallint",
        "serial",
        "bigserial",
        "boolean",
        "text",
        "varchar",
        "char",
        "numeric",
        "decimal",
        "real",
        "double precision",
        "date",
        "timestamp",
        "timestamptz",
        "time",
        "interval",
        "uuid",
        "json",
        "jsonb",
        "bytea"
    ];
}
