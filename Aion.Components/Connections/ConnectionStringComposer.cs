using System.Data.Common;
using Aion.Contracts.Database;

namespace Aion.Components.Connections;

/// <summary>
/// Builds and parses connection strings for the connection dialog. <see cref="DbConnectionStringBuilder"/> quotes
/// values the same way the ADO.NET drivers parse them, so passwords containing ';', '=' or quotes survive intact,
/// and it works in Aion.Components without referencing any driver package.
/// </summary>
public static class ConnectionStringComposer
{
    private sealed class Key
    {
        private readonly HashSet<string> _names;

        public Key(string canonical, params string[] synonyms)
        {
            Canonical = canonical;
            _names = synonyms.Append(canonical).Select(Normalize).ToHashSet();
        }

        public string Canonical { get; }

        public bool Matches(string key) => _names.Contains(Normalize(key));
    }

    private sealed record Keys(
        Key Host,
        Key? Port,
        Key? Database,
        Key? Username,
        Key? Password,
        Key? IntegratedSecurity = null,
        Key? Encrypt = null,
        Key? TrustServerCertificate = null,
        Key? ConnectTimeout = null)
    {
        private IEnumerable<Key> Managed =>
            new[] { Host, Port, Database, Username, Password, IntegratedSecurity, Encrypt, TrustServerCertificate }.OfType<Key>();

        public bool IsManaged(string key) => Managed.Any(k => k.Matches(key));
    }

    // Synonyms are matched ignoring case, spaces and underscores, so "User Id", "UserID" and "userid" are one entry.
    private static readonly Keys PostgreSql = new(
        Host: new Key("Host", "Server"),
        Port: new Key("Port"),
        Database: new Key("Database", "DB"),
        Username: new Key("Username", "User Id", "UID", "User"),
        Password: new Key("Password", "PWD", "PSW"),
        ConnectTimeout: new Key("Timeout"));

    private static readonly Keys MySql = new(
        Host: new Key("Server", "Host", "Data Source", "Address", "Addr", "Network Address"),
        Port: new Key("Port"),
        Database: new Key("Database", "Initial Catalog"),
        Username: new Key("User Id", "Uid", "Username", "User"),
        Password: new Key("Password", "Pwd"),
        ConnectTimeout: new Key("Connect Timeout", "Connection Timeout"));

    private static readonly Keys SqlServer = new(
        Host: new Key("Server", "Data Source", "Address", "Addr", "Network Address"),
        Port: null,
        Database: new Key("Initial Catalog", "Database"),
        Username: new Key("User Id", "UID", "User"),
        Password: new Key("Password", "PWD"),
        IntegratedSecurity: new Key("Integrated Security", "Trusted_Connection"),
        Encrypt: new Key("Encrypt"),
        TrustServerCertificate: new Key("TrustServerCertificate"),
        ConnectTimeout: new Key("Connect Timeout", "Connection Timeout", "Timeout"));

    private static readonly Keys LiteDb = new(
        Host: new Key("Filename"),
        Port: null,
        Database: null,
        Username: null,
        Password: new Key("Password"));

    private static readonly Keys FileBased = new(
        Host: new Key("Data Source", "Filename"),
        Port: null,
        Database: null,
        Username: null,
        Password: new Key("Password"));

    /// <summary>
    /// Builds a connection string from the dialog's Basic fields. Options the dialog does not manage (SSL mode,
    /// application name, pooling and so on) are carried over from <paramref name="preserveOptionsFrom"/>.
    /// </summary>
    public static string Build(ConnectionDialogModel model, string? preserveOptionsFrom = null)
    {
        var keys = KeysFor(model.Type);
        var original = TryParse(preserveOptionsFrom);
        var builder = new DbConnectionStringBuilder();

        builder[keys.Host.Canonical] = model.Type == DatabaseType.SQLServer
            ? BuildSqlServerDataSource(model)
            : model.Host.Trim();

        SetIfPresent(builder, keys.Port, model.Port.Trim());
        SetIfPresent(builder, keys.Database, model.Database.Trim());

        if (keys.IntegratedSecurity != null && model.UseWindowsAuth)
        {
            builder[keys.IntegratedSecurity.Canonical] = "True";
        }
        else
        {
            SetIfPresent(builder, keys.Username, model.Username.Trim());
            SetIfPresent(builder, keys.Password, model.Password);
        }

        if (keys.Encrypt != null)
        {
            builder[keys.Encrypt.Canonical] = GetEncryptValue(model.Encrypt, Find(original, keys.Encrypt));
        }

        if (keys.TrustServerCertificate != null && model.TrustServerCertificate)
        {
            builder[keys.TrustServerCertificate.Canonical] = "True";
        }

        if (original != null)
        {
            foreach (string key in original.Keys)
            {
                if (!keys.IsManaged(key))
                    builder[key] = original[key];
            }
        }

        return builder.ConnectionString;
    }

    /// <summary>
    /// Fills the dialog's Basic fields from an existing connection string for <see cref="ConnectionDialogModel.Type"/>.
    /// Returns false, leaving the model untouched, when the string cannot be parsed.
    /// </summary>
    public static bool TryPopulate(ConnectionDialogModel model, string? connectionString)
    {
        var parsed = TryParse(connectionString);
        if (parsed == null)
            return false;

        var keys = KeysFor(model.Type);
        var host = Find(parsed, keys.Host) ?? string.Empty;

        if (model.Type == DatabaseType.SQLServer)
        {
            var (server, port, instance) = SplitSqlServerDataSource(host);
            model.Host = server;
            model.Port = port;
            model.Instance = instance;
        }
        else
        {
            model.Host = host;
            model.Port = Find(parsed, keys.Port) ?? string.Empty;
        }

        model.Database = Find(parsed, keys.Database) ?? string.Empty;
        model.Username = Find(parsed, keys.Username) ?? string.Empty;
        model.Password = Find(parsed, keys.Password) ?? string.Empty;
        model.UseWindowsAuth = IsTrue(Find(parsed, keys.IntegratedSecurity));
        model.Encrypt = keys.Encrypt == null || !IsFalse(Find(parsed, keys.Encrypt));
        model.TrustServerCertificate = IsTrue(Find(parsed, keys.TrustServerCertificate));

        return true;
    }

    /// <summary>
    /// The server or file a connection string points at, for naming a connection the user did not name.
    /// </summary>
    public static string? DescribeTarget(DatabaseType type, string? connectionString)
    {
        var value = Find(TryParse(connectionString), KeysFor(type).Host);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return type is DatabaseType.LiteDB or DatabaseType.SQLite ? Path.GetFileName(value) : value;
    }

    /// <summary>
    /// Returns the connection string with the driver's connect timeout set, replacing any timeout already present.
    /// Types without a connect timeout, or strings that cannot be parsed, are returned unchanged.
    /// </summary>
    public static string WithConnectTimeout(string connectionString, DatabaseType type, TimeSpan timeout)
    {
        var timeoutKey = KeysFor(type).ConnectTimeout;
        var builder = TryParse(connectionString);
        if (timeoutKey == null || builder == null)
            return connectionString;

        foreach (var key in builder.Keys.Cast<string>().Where(timeoutKey.Matches).ToList())
        {
            builder.Remove(key);
        }

        builder[timeoutKey.Canonical] = Math.Max(1, (int)Math.Ceiling(timeout.TotalSeconds));
        return builder.ConnectionString;
    }

    private static Keys KeysFor(DatabaseType type) => type switch
    {
        DatabaseType.PostgreSQL => PostgreSql,
        DatabaseType.MySQL => MySql,
        DatabaseType.SQLServer => SqlServer,
        DatabaseType.LiteDB => LiteDb,
        _ => FileBased
    };

    private static DbConnectionStringBuilder? TryParse(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        try
        {
            return new DbConnectionStringBuilder { ConnectionString = connectionString };
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? Find(DbConnectionStringBuilder? builder, Key? key)
    {
        if (builder == null || key == null)
            return null;

        return builder.Keys.Cast<string>()
            .Where(key.Matches)
            .Select(k => builder[k]?.ToString())
            .FirstOrDefault();
    }

    private static void SetIfPresent(DbConnectionStringBuilder builder, Key? key, string value)
    {
        if (key != null && !string.IsNullOrEmpty(value))
            builder[key.Canonical] = value;
    }

    private static string BuildSqlServerDataSource(ConnectionDialogModel model)
    {
        var host = model.Host.Trim();

        if (!string.IsNullOrWhiteSpace(model.Instance))
            return $"{host}\\{model.Instance.Trim()}";

        return string.IsNullOrWhiteSpace(model.Port) ? host : $"{host},{model.Port.Trim()}";
    }

    private static (string Host, string Port, string? Instance) SplitSqlServerDataSource(string dataSource)
    {
        var port = string.Empty;
        var commaIndex = dataSource.LastIndexOf(',');
        if (commaIndex >= 0)
        {
            port = dataSource[(commaIndex + 1)..].Trim();
            dataSource = dataSource[..commaIndex];
        }

        var slashIndex = dataSource.IndexOf('\\');
        if (slashIndex >= 0)
            return (dataSource[..slashIndex].Trim(), port, dataSource[(slashIndex + 1)..].Trim());

        return (dataSource.Trim(), port, null);
    }

    private static string GetEncryptValue(bool encrypt, string? existing)
    {
        // Keep values like "Strict" or "Optional" when they already say what the switch says.
        if (encrypt && existing != null && !IsFalse(existing))
            return existing;

        if (!encrypt && IsFalse(existing))
            return existing!;

        return encrypt ? "True" : "False";
    }

    private static bool IsTrue(string? value) =>
        value != null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                          value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                          value.Equals("sspi", StringComparison.OrdinalIgnoreCase));

    private static bool IsFalse(string? value) =>
        value != null && (value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                          value.Equals("no", StringComparison.OrdinalIgnoreCase) ||
                          value.Equals("optional", StringComparison.OrdinalIgnoreCase));

    private static string Normalize(string key) =>
        new string(key.Where(c => !char.IsWhiteSpace(c) && c != '_').ToArray()).ToLowerInvariant();
}
