using Aion.Contracts.Connections;
using Aion.Contracts.Database;

namespace Aion.Components.Connections;

public class ConnectionDialogModel
{
    public string? Name { get; set; } = null;
    public DatabaseType Type { get; set; } = DatabaseType.PostgreSQL;
    public string Host { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string? Instance { get; set; }
    public bool UseWindowsAuth { get; set; }
    public bool Encrypt { get; set; } = true;
    public bool TrustServerCertificate { get; set; }
    public bool SaveCredentials { get; set; }
    public Guid? EditingConnectionId { get; set; }

    /// <summary>
    /// Dialog values for editing an existing connection. The Basic fields are filled in by the dialog,
    /// which parses <see cref="ConnectionString"/> for the connection's type.
    /// </summary>
    public static ConnectionDialogModel ForEdit(ConnectionModel connection) => new()
    {
        EditingConnectionId = connection.Id,
        Name = connection.Name,
        Type = connection.Type,
        ConnectionString = connection.ConnectionString,
        SaveCredentials = connection.SaveCredentials
    };
}
