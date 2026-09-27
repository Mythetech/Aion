using System.Data.Common;
using Aion.Contracts.Database;

namespace Aion.Components.Connections;

/// <summary>
/// Splits a connection's password from the rest of its connection string, so the string can be saved while the
/// password goes to a secret manager. The password is the value under the provider's password key, in any of the
/// spellings <see cref="ConnectionStringComposer"/> knows, and for LiteDB that is the file's encryption password.
/// </summary>
public static class ConnectionPasswords
{
    /// <summary>
    /// The connection string's password, or null when it has none or cannot be parsed.
    /// </summary>
    public static string? GetPassword(DatabaseType type, string? connectionString)
    {
        var builder = ConnectionStringComposer.TryParse(connectionString);
        if (builder == null)
            return null;

        var password = PasswordKeys(type, builder).Select(key => builder[key]?.ToString()).FirstOrDefault();
        return string.IsNullOrEmpty(password) ? null : password;
    }

    /// <summary>
    /// The connection string with every password key removed. A string without one, or one that cannot be parsed,
    /// is returned as it was, since it has no password that could be told apart from the rest.
    /// </summary>
    public static string WithoutPassword(DatabaseType type, string connectionString)
    {
        var builder = ConnectionStringComposer.TryParse(connectionString);
        if (builder == null)
            return connectionString;

        var keys = PasswordKeys(type, builder);
        if (keys.Count == 0)
            return connectionString;

        foreach (var key in keys)
        {
            builder.Remove(key);
        }

        return builder.ConnectionString;
    }

    /// <summary>
    /// The connection string with <paramref name="password"/> under the provider's password key, replacing any
    /// password it already had.
    /// </summary>
    public static string WithPassword(DatabaseType type, string connectionString, string password)
    {
        var passwordKey = ConnectionStringComposer.KeysFor(type).Password;
        if (passwordKey == null)
            return connectionString;

        var builder = ConnectionStringComposer.TryParse(connectionString) ?? new DbConnectionStringBuilder();
        foreach (var key in PasswordKeys(type, builder))
        {
            builder.Remove(key);
        }

        builder[passwordKey.Canonical] = password;
        return builder.ConnectionString;
    }

    private static List<string> PasswordKeys(DatabaseType type, DbConnectionStringBuilder builder)
    {
        var passwordKey = ConnectionStringComposer.KeysFor(type).Password;
        return passwordKey == null ? [] : builder.Keys.Cast<string>().Where(passwordKey.Matches).ToList();
    }
}
