namespace Aion.Contracts.Database;

public class DatabaseModel
{
    public string Name { get; set; }

    public List<TableInfo> Tables { get; set; } = [];
    public SchemaLoadState TablesState { get; set; } = SchemaLoadState.NotLoaded;
    public bool TablesLoaded
    {
        get => TablesState.IsLoaded;
        set => TablesState = value ? SchemaLoadState.Loaded : SchemaLoadState.NotLoaded;
    }

    public Dictionary<string, List<ColumnInfo>> TableColumns { get; set; } = [];
    public HashSet<string> LoadedColumnTables { get; set; } = [];

    /// <summary>
    /// Tables whose columns are loading or failed to load, by display name. Tables with loaded columns are in
    /// <see cref="LoadedColumnTables"/> instead.
    /// </summary>
    public Dictionary<string, SchemaLoadState> ColumnStates { get; set; } = [];

    public List<IndexInfo> Indexes { get; set; } = [];
    public SchemaLoadState IndexesState { get; set; } = SchemaLoadState.NotLoaded;
    public bool IndexesLoaded
    {
        get => IndexesState.IsLoaded;
        set => IndexesState = value ? SchemaLoadState.Loaded : SchemaLoadState.NotLoaded;
    }

    public List<RoutineInfo> Routines { get; set; } = [];
    public SchemaLoadState RoutinesState { get; set; } = SchemaLoadState.NotLoaded;
    public bool RoutinesLoaded
    {
        get => RoutinesState.IsLoaded;
        set => RoutinesState = value ? SchemaLoadState.Loaded : SchemaLoadState.NotLoaded;
    }

    public SchemaLoadState ColumnsState(string tableDisplayName) =>
        LoadedColumnTables.Contains(tableDisplayName)
            ? SchemaLoadState.Loaded
            : ColumnStates.GetValueOrDefault(tableDisplayName, SchemaLoadState.NotLoaded);
}

public record TableInfo(string Schema, string Name)
{
    public string DisplayName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
}
