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

    /// <summary>
    /// Generates an UPDATE that targets only the row matching every key value. The implementation owns all
    /// identifier quoting and literal formatting for its engine.
    /// </summary>
    Task<string> GenerateUpdateScript(string database, string schema, string table, IEnumerable<ColumnValue> values, IEnumerable<ColumnValue> keyValues);

    /// <summary>
    /// Generates a DELETE that targets only the row matching every key value.
    /// </summary>
    Task<string> GenerateDeleteScript(string database, string schema, string table, IEnumerable<ColumnValue> keyValues);

    Task<string> GenerateSelectTopScript(string database, string schema, string table, int count);
    Task<string> GenerateCountScript(string database, string schema, string table);
}

public record ColumnDefinition(
    string Name,
    string DataType,
    bool IsNullable,
    string? DefaultValue = null,
    bool IsPrimaryKey = false
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
