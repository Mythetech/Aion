namespace Aion.Components.Connections;

public class ConnectionModel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; }
    public string ConnectionString { get; set; }
    public List<DatabaseModel> Databases { get; set; } = [];
    public bool Active { get; set; }
}

public class DatabaseModel
{
    public string Name { get; set; }
    public List<string> Tables { get; set; } = [];
    public bool TablesLoaded { get; set; }
}