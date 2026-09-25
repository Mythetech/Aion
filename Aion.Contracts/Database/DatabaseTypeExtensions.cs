namespace Aion.Contracts.Database;

public static class DatabaseTypeExtensions
{
    /// <summary>
    /// Engines that run inside the Aion process (browser WASM or embedded files). There is no server
    /// to lose contact with, so background health polling only adds noise for them.
    /// </summary>
    public static bool IsInProcess(this DatabaseType type) => type is
        DatabaseType.WasmSQLite or
        DatabaseType.WasmPostgreSQL or
        DatabaseType.LiteDB or
        DatabaseType.SQLite;
}
