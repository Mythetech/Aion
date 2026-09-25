using Aion.Contracts.Database;
using Aion.Contracts.Database.Dialects;

namespace Aion.Core.Database.MySql;

public class MySqlCommands : IStandardDatabaseCommands
{
    private static readonly MySqlDialect Dialect = MySqlDialect.Instance;

    public Task<string> GenerateCreateDatabaseScript(string name)
    {
        return Task.FromResult($@"
CREATE DATABASE `{name}`
    DEFAULT CHARACTER SET utf8mb4
    DEFAULT COLLATE utf8mb4_unicode_ci;");
    }

    public Task<string> GenerateDropDatabaseScript(string name)
    {
        return Task.FromResult($@"
DROP DATABASE IF EXISTS `{name}`;");
    }

    public Task<string> GenerateBackupDatabaseScript(string name, string location)
    {
        return Task.FromResult($@"
mysqldump `{name}` > ""{location}"";");
    }

    public Task<string> GenerateCreateTableScript(string database, string schema, string name, IEnumerable<ColumnDefinition> columns)
    {
        var columnDefs = columns.Select(c =>
            $"`{c.Name}` {c.DataType} {(c.IsNullable ? "NULL" : "NOT NULL")} {(c.DefaultValue != null ? $"DEFAULT {c.DefaultValue}" : "")}");

        return Task.FromResult($@"
CREATE TABLE `{name}` (
    {string.Join(",\n    ", columnDefs)}
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;");
    }

    public Task<string> GenerateDropTableScript(string database, string schema, string name)
    {
        return Task.FromResult($@"
DROP TABLE IF EXISTS `{name}`;");
    }

    public Task<string> GenerateAlterTableScript(string database, string schema, string name, IEnumerable<TableModification> modifications)
    {
        var alterStatements = modifications.Select(mod => mod.Type switch
        {
            ModificationType.AddColumn =>
                $"ADD COLUMN `{mod.NewColumn!.Name}` {mod.NewColumn.DataType} {(mod.NewColumn.IsNullable ? "NULL" : "NOT NULL")} {(mod.NewColumn.DefaultValue != null ? $"DEFAULT {mod.NewColumn.DefaultValue}" : "")}",
            ModificationType.DropColumn =>
                $"DROP COLUMN `{mod.ColumnName}`",
            ModificationType.AlterColumn =>
                $"MODIFY COLUMN `{mod.ColumnName}` {mod.NewColumn!.DataType} {(mod.NewColumn.IsNullable ? "NULL" : "NOT NULL")}",
            _ => throw new ArgumentOutOfRangeException()
        });

        return Task.FromResult($@"
ALTER TABLE `{name}`
{string.Join(",\n", alterStatements)};");
    }

    public Task<string> GenerateInsertScript(string database, string schema, string table, IEnumerable<ColumnValue> values)
    {
        var columns = values.ToList();

        return Task.FromResult(
            $"INSERT INTO {TableName(table)}\n({Dialect.BuildColumnList(columns)})\nVALUES ({Dialect.BuildValueList(columns)});");
    }

    public Task<string> GenerateUpdateScript(string database, string schema, string table, IEnumerable<ColumnValue> values, IEnumerable<ColumnValue> keyValues)
    {
        return Task.FromResult(
            $"UPDATE {TableName(table)}\nSET {Dialect.BuildAssignments(values)}\nWHERE {Dialect.BuildKeyPredicate(keyValues)};");
    }

    public Task<string> GenerateDeleteScript(string database, string schema, string table, IEnumerable<ColumnValue> keyValues)
    {
        return Task.FromResult(
            $"DELETE FROM {TableName(table)}\nWHERE {Dialect.BuildKeyPredicate(keyValues)};");
    }

    public Task<string> GenerateSelectTopScript(string database, string schema, string table, int count)
    {
        return Task.FromResult($"SELECT * FROM {TableName(table)}\nLIMIT {count};");
    }

    public Task<string> GenerateCountScript(string database, string schema, string table)
    {
        return Task.FromResult($"SELECT COUNT(*) FROM {TableName(table)};");
    }

    // The connection string already selects the database, and MySQL has no schema level below it.
    private static string TableName(string table) => Dialect.QuoteIdentifier(table);
}
