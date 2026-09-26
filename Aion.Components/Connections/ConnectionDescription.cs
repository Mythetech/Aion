using System.Data.Common;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;

namespace Aion.Components.Connections;

/// <summary>
/// Describes a connection for display without exposing its connection string,
/// which can carry passwords, tokens and other credentials.
/// </summary>
public static class ConnectionDescription
{
    private static readonly string[] HostKeys =
        ["Host", "Server", "Data Source", "DataSource", "Address", "Addr", "Network Address", "Filename"];

    public static string Describe(ConnectionModel connection)
    {
        var engine = EngineName(connection.Type);
        var host = Host(connection);

        return string.IsNullOrWhiteSpace(host) ? engine : $"{engine} on {host}";
    }

    public static string EngineName(DatabaseType type) => type switch
    {
        DatabaseType.PostgreSQL => "PostgreSQL",
        DatabaseType.SQLServer => "SQL Server",
        DatabaseType.MySQL => "MySQL",
        DatabaseType.SQLite => "SQLite",
        DatabaseType.LiteDB => "LiteDB",
        DatabaseType.WasmSQLite => "SQLite (In-Browser)",
        DatabaseType.WasmPostgreSQL => "PostgreSQL (PGlite)",
        _ => type.ToString()
    };

    /// <summary>
    /// A compact engine name for narrow places such as the schema panel header, where the in-browser engines
    /// read better as "SQLite · in-browser" than as their longer <see cref="EngineName"/>.
    /// </summary>
    public static string EngineLabel(DatabaseType type) => type switch
    {
        DatabaseType.WasmSQLite => "SQLite · in-browser",
        DatabaseType.WasmPostgreSQL => "PGlite · in-browser",
        _ => EngineName(type)
    };

    /// <summary>
    /// The engine in a word or two, for a badge beside a connection's name such as the editor's connection
    /// picker. Where the engine runs is left to the longer names.
    /// </summary>
    public static string EngineBadge(DatabaseType type) => type switch
    {
        DatabaseType.WasmSQLite => "SQLite",
        DatabaseType.WasmPostgreSQL => "PGlite",
        _ => EngineName(type)
    };

    public static string? Host(ConnectionModel connection)
    {
        var connectionString = connection.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
            return uri.Host;

        string? value;
        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            value = HostKeys
                .Select(key => builder.TryGetValue(key, out var v) ? v?.ToString() : null)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
            return null;

        return IsFileBased(connection.Type) ? Path.GetFileName(value) : StripPort(value);
    }

    private static bool IsFileBased(DatabaseType type) =>
        type is DatabaseType.SQLite or DatabaseType.LiteDB or DatabaseType.WasmSQLite;

    // SQL Server writes the port after a comma ("host,1433") and some drivers prefix the protocol ("tcp:host").
    private static string StripPort(string server)
    {
        var host = server.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase) ? server[4..] : server;
        var comma = host.IndexOf(',');
        return comma > 0 ? host[..comma] : host;
    }
}
