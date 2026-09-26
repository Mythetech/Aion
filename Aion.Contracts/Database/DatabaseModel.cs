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

    public List<TableInfo> Views { get; set; } = [];
    public SchemaLoadState ViewsState { get; set; } = SchemaLoadState.NotLoaded;

    /// <summary>
    /// Columns of tables and views by display name. The two share a namespace in every engine Aion supports,
    /// so a name never means both.
    /// </summary>
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

    /// <summary>
    /// How many rows the table held when it was listed, when the engine could say cheaply. Null when it could not.
    /// </summary>
    public TableRowCount? RowCount { get; init; }
}

/// <summary>
/// A table's row count as the schema tree shows it: counted exactly where counting is cheap (the in-browser
/// engines and LiteDB), otherwise estimated from the statistics the engine keeps in its catalog.
/// </summary>
public sealed record TableRowCount(long Rows, bool IsEstimate)
{
    public static TableRowCount Exact(long rows) => new(rows, false);

    public static TableRowCount Estimated(long rows) => new(rows, true);
}
