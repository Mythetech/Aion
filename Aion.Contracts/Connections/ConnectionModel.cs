using System.Text.Json.Serialization;
using Aion.Contracts.Database;

namespace Aion.Contracts.Connections;

public class ConnectionModel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; }
    public string ConnectionString { get; set; }
    public DatabaseType Type { get; set; }
    public bool SaveCredentials { get; set; }
    public bool IsSavedConnection { get; set; }

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
