namespace Aion.Core.Database;

public class DatabaseModel
{
    public string Name { get; set; }
    public List<TableInfo> Tables { get; set; } = [];
    public bool TablesLoaded { get; set; }
    public Dictionary<string, List<ColumnInfo>> TableColumns { get; set; } = [];
    public HashSet<string> LoadedColumnTables { get; set; } = [];

    public List<IndexInfo> Indexes { get; set; } = [];
    public bool IndexesLoaded { get; set; }

    public List<RoutineInfo> Routines { get; set; } = [];
    public bool RoutinesLoaded { get; set; }
}

public record TableInfo(string Schema, string Name)
{
    public string DisplayName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
}
