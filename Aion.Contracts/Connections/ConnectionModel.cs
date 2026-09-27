using System.Text.Json.Serialization;
using Aion.Contracts.Database;

namespace Aion.Contracts.Connections;

public class ConnectionModel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; }
    public string ConnectionString { get; set; }
    public DatabaseType Type { get; set; }
    public bool IsSavedConnection { get; set; }

    /// <summary>
    /// Whether the connection logs in with a password. Saved connection strings never hold the password, so this
    /// is what tells a connection waiting for one apart from one that needs none (trust or Windows authentication).
    /// </summary>
    public bool UsesPassword { get; set; }

    /// <summary>
    /// The name of the secret manager holding the password, or null when it isn't stored and Aion asks for it.
    /// </summary>
    public string? PasswordStore { get; set; }

    // Runtime state is ignored so a saved profile never restores a stale "Connected" status or an old database tree.
    [JsonIgnore]
    public List<DatabaseModel> Databases { get; set; } = [];

    [JsonIgnore]
    public bool Active { get; set; }

    [JsonIgnore]
    public DateTime? LastActivityTime { get; set; }

    [JsonIgnore]
    public DateTime? LastHealthCheckTime { get; set; }

    [JsonIgnore]
    public ConnectionHealthStatus HealthStatus { get; set; } = ConnectionHealthStatus.Unknown;

    [JsonIgnore]
    public string? LastError { get; set; }
}
