using System.Text.Json;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;

namespace Aion.Components.Connections;

/// <summary>
/// The saved connections file. Passwords are taken out here, as the connections are written, so no caller can put
/// one on disk: a connection records only that it uses a password and which secret manager holds it.
/// </summary>
public static class ConnectionsFile
{
    /// <param name="WipedPasswords">
    /// True when the file still had passwords in it, written before they moved to secret managers. They were
    /// dropped from the loaded connections, and the file should be written again so they are gone from disk too.
    /// </param>
    public sealed record Contents(IReadOnlyList<ConnectionModel> Connections, bool WipedPasswords);

    public static string Serialize(IEnumerable<ConnectionModel> connections) =>
        JsonSerializer.Serialize(connections.Select(SavedConnection.From).ToList());

    public static Contents Deserialize(string json)
    {
        var saved = JsonSerializer.Deserialize<List<SavedConnection>>(json) ?? [];
        var wiped = false;

        var connections = saved.Select(record =>
        {
            var connection = record.ToConnectionModel();
            if (ConnectionPasswords.GetPassword(connection.Type, connection.ConnectionString) != null)
            {
                connection.ConnectionString = ConnectionPasswords.WithoutPassword(connection.Type, connection.ConnectionString);
                connection.UsesPassword = true;
                wiped = true;
            }

            return connection;
        }).ToList();

        return new Contents(connections, wiped);
    }

    // Property names match the ConnectionModel fields earlier versions wrote, so their files load unchanged.
    // Fields those versions saved that no longer matter, such as SaveCredentials, are ignored.
    private sealed record SavedConnection
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = "";
        public string ConnectionString { get; init; } = "";
        public DatabaseType Type { get; init; }
        public bool UsesPassword { get; init; }
        public string? PasswordStore { get; init; }

        public static SavedConnection From(ConnectionModel connection) => new()
        {
            Id = connection.Id,
            Name = connection.Name,
            ConnectionString = ConnectionPasswords.WithoutPassword(connection.Type, connection.ConnectionString),
            Type = connection.Type,
            UsesPassword = connection.UsesPassword
                           || ConnectionPasswords.GetPassword(connection.Type, connection.ConnectionString) != null,
            PasswordStore = connection.PasswordStore
        };

        public ConnectionModel ToConnectionModel() => new()
        {
            Id = Id,
            Name = Name,
            ConnectionString = ConnectionString,
            Type = Type,
            UsesPassword = UsesPassword,
            PasswordStore = PasswordStore,
            IsSavedConnection = true
        };
    }
}
