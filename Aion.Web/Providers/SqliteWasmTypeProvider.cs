using Aion.Contracts.Database;

namespace Aion.Web.Providers;

public class SqliteWasmTypeProvider : ISupportedTypeProvider
{
    public DatabaseType DatabaseType => DatabaseType.WasmSQLite;

    public IReadOnlyList<string> SupportedTypes { get; } =
    [
        "INTEGER",
        "TEXT",
        "REAL",
        "BLOB",
        "NUMERIC"
    ];
}
