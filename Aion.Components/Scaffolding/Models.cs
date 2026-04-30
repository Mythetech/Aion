using Aion.Contracts.Database;

namespace Aion.Components.Scaffolding;

public class SchemaWizardModel
{
    public string DatabaseName { get; set; } = string.Empty;
    public DatabaseType EngineType { get; set; }
    public List<TableDefinitionModel> Tables { get; set; } = [new()];
}

public class TableDefinitionModel
{
    public string Name { get; set; } = string.Empty;
    public List<ColumnDefinitionModel> Columns { get; set; } = [new()];

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name)
        && Columns.Count > 0
        && Columns.All(c => c.IsValid)
        && Columns.Count(c => c.IsPrimaryKey) <= 1;

    public string? ValidationError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name))
                return "Table name is required";
            if (Columns.Count == 0)
                return "At least one column is required";
            if (Columns.Count(c => c.IsPrimaryKey) > 1)
                return "Only one primary key column is allowed per table";
            var invalid = Columns.FirstOrDefault(c => !c.IsValid);
            if (invalid != null)
                return $"Column '{invalid.Name}': name and data type are required";
            return null;
        }
    }
}

public class ColumnDefinitionModel
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; } = true;
    public string? DefaultValue { get; set; }
    public bool IsPrimaryKey { get; set; }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(DataType);
}
