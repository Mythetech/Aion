namespace Aion.Components.Connections;

public class ConnectionModel
{
    public string Name { get; set; }
    public string ConnectionString { get; set; }
    public List<string> Tables { get; set; } = [];
}