namespace Aion.Contracts.Database;

public interface IStandardDatabaseCommands
{
    Task<string> GenerateCreateDatabaseScript(string name);
    Task<string> GenerateDropDatabaseScript(string name);
    Task<string> GenerateBackupDatabaseScript(string name, string location);

    Task<string> GenerateCreateTableScript(string database, string schema, string name, IEnumerable<ColumnDefinition> columns);
    Task<string> GenerateDropTableScript(string database, string schema, string name);
    Task<string> GenerateAlterTableScript(string database, string schema, string name, IEnumerable<TableModification> modifications);

    Task<string> GenerateInsertScript(string database, string schema, string table, IEnumerable<ColumnValue> values);
    Task<string> GenerateUpdateScript(string database, string schema, string table, IEnumerable<ColumnValue> values, string whereClause);
    Task<string> GenerateDeleteScript(string database, string schema, string table, string whereClause);

    Task<string> GenerateSelectTopScript(string database, string schema, string table, int count);
    Task<string> GenerateCountScript(string database, string schema, string table);
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
