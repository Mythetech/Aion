using Aion.Contracts.Database;

namespace Aion.Components.Scaffolding;

public class SchemaWizardModel
{
    public string DatabaseName { get; set; } = string.Empty;
    public DatabaseType EngineType { get; set; }
    public List<TableDefinitionModel> Tables { get; set; } = [new()];

    /// <summary>
    /// Switches to another engine and carries each column's type over to that engine's equivalent. A type
    /// with no equivalent is cleared so it has to be picked again rather than failing when the table is created.
    /// </summary>
    /// <returns>Every column whose type changed, with its old and new type (null when it was cleared).</returns>
    public IReadOnlyList<ColumnTypeChange> ChangeEngine(DatabaseType engine, IReadOnlyList<string> supportedTypes)
    {
        var from = EngineType;
        EngineType = engine;

        if (from == engine)
            return [];

        var changes = new List<ColumnTypeChange>();
        for (var t = 0; t < Tables.Count; t++)
        {
            var table = Tables[t];
            for (var c = 0; c < table.Columns.Count; c++)
            {
                var column = table.Columns[c];
                if (string.IsNullOrWhiteSpace(column.DataType))
                    continue;

                var mapped = ColumnTypeMapping.Map(column.DataType, from, engine, supportedTypes);
                if (mapped == column.DataType)
                    continue;

                changes.Add(new ColumnTypeChange(table.Label(t), column.Label(c), column.DataType, mapped));
                column.DataType = mapped ?? string.Empty;
            }
        }

        return changes;
    }
}

/// <param name="To">The new type, or null when the new engine had no equivalent and the type was cleared.</param>
public sealed record ColumnTypeChange(string Table, string Column, string From, string? To);

public class TableDefinitionModel
{
    public string Name { get; set; } = string.Empty;
    public List<ColumnDefinitionModel> Columns { get; set; } = [new()];

    /// <summary>How messages refer to the table: its name, or its position until it has one.</summary>
    public string Label(int index) => string.IsNullOrWhiteSpace(Name) ? $"Table {index + 1}" : Name;
}

public class ColumnDefinitionModel
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; } = true;
    public string? DefaultValue { get; set; }
    public bool IsPrimaryKey { get; set; }

    public string Label(int index) => string.IsNullOrWhiteSpace(Name) ? $"column {index + 1}" : Name;
}
