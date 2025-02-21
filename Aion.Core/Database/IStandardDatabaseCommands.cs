public interface IStandardDatabaseCommands
{
    // Database operations
    Task<string> GenerateCreateDatabaseScript(string name);
    Task<string> GenerateDropDatabaseScript(string name);
    Task<string> GenerateBackupDatabaseScript(string name, string location);
    
    // Table operations
    Task<string> GenerateCreateTableScript(string database, string name, IEnumerable<ColumnDefinition> columns);
    Task<string> GenerateDropTableScript(string database, string name);
    Task<string> GenerateAlterTableScript(string database, string name, IEnumerable<TableModification> modifications);
    
    // Data operations
    Task<string> GenerateInsertScript(string database, string table, IEnumerable<ColumnValue> values);
    Task<string> GenerateUpdateScript(string database, string table, IEnumerable<ColumnValue> values, string whereClause);
    Task<string> GenerateDeleteScript(string database, string table, string whereClause);
    
    // Query templates
    Task<string> GenerateSelectTopScript(string database, string table, int count);
    Task<string> GenerateCountScript(string database, string table);
}

public record ColumnDefinition(
    string Name, 
    string DataType,
    bool IsNullable,
    string? DefaultValue = null
);

public record ColumnValue(
    string Column,
    object? Value
);

public record TableModification(
    ModificationType Type,
    string? ColumnName = null,
    ColumnDefinition? NewColumn = null
);

public enum ModificationType
{
    AddColumn,
    DropColumn,
    AlterColumn
} 