using Aion.Core.Database;

namespace Aion.Core.Connections;

public class ConnectionModel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; }
    public string ConnectionString { get; set; }
    public DatabaseType Type { get; set; }
    public List<DatabaseModel> Databases { get; set; } = [];
    public bool Active { get; set; }
    public bool SaveCredentials { get; set; }
    public bool IsSavedConnection { get; set; }
}

