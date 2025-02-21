namespace Aion.Core.Database;

public class DatabaseModel
{
    public string Name { get; set; }
    public List<string> Tables { get; set; } = [];
    public bool TablesLoaded { get; set; }
}