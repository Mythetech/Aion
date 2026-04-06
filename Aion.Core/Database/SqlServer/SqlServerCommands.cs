namespace Aion.Core.Database.SqlServer;

public class SqlServerCommands : IStandardDatabaseCommands
{
    public Task<string> GenerateCreateDatabaseScript(string name)
    {
        return Task.FromResult($@"
CREATE DATABASE [{name}]
COLLATE Latin1_General_CI_AS;");
    }

    public Task<string> GenerateDropDatabaseScript(string name)
    {
        return Task.FromResult($@"
IF EXISTS (SELECT * FROM sys.databases WHERE name = '{name}')
BEGIN
    ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{name}];
END");
    }

    public Task<string> GenerateBackupDatabaseScript(string name, string location)
    {
        return Task.FromResult($@"
BACKUP DATABASE [{name}]
TO DISK = '{location}'
WITH FORMAT, MEDIANAME = 'AionBackup',
NAME = 'Full Backup of {name}';");
    }

    public Task<string> GenerateCreateTableScript(string database, string schema, string name, IEnumerable<ColumnDefinition> columns)
    {
        var columnDefs = columns.Select(c =>
            $"[{c.Name}] {c.DataType} {(c.IsNullable ? "NULL" : "NOT NULL")} {(c.DefaultValue != null ? $"DEFAULT {c.DefaultValue}" : "")}");

        return Task.FromResult($@"
CREATE TABLE [{schema}].[{name}] (
    {string.Join(",\n    ", columnDefs)}
);");
    }

    public Task<string> GenerateDropTableScript(string database, string schema, string name)
    {
        return Task.FromResult($@"
IF OBJECT_ID('{schema}.{name}', 'U') IS NOT NULL
    DROP TABLE [{schema}].[{name}];");
    }

    public Task<string> GenerateAlterTableScript(string database, string schema, string name, IEnumerable<TableModification> modifications)
    {
        var alterStatements = modifications.Select(mod => mod.Type switch
        {
            ModificationType.AddColumn =>
                $"ADD [{mod.NewColumn!.Name}] {mod.NewColumn.DataType} {(mod.NewColumn.IsNullable ? "NULL" : "NOT NULL")} {(mod.NewColumn.DefaultValue != null ? $"DEFAULT {mod.NewColumn.DefaultValue}" : "")}",
            ModificationType.DropColumn =>
                $"DROP COLUMN [{mod.ColumnName}]",
            ModificationType.AlterColumn =>
                $"ALTER COLUMN [{mod.ColumnName}] {mod.NewColumn!.DataType} {(mod.NewColumn.IsNullable ? "NULL" : "NOT NULL")}",
            _ => throw new ArgumentOutOfRangeException()
        });

        return Task.FromResult($@"
ALTER TABLE [{schema}].[{name}]
{string.Join(",\n", alterStatements)};");
    }

    public Task<string> GenerateInsertScript(string database, string schema, string table, IEnumerable<ColumnValue> values)
    {
        var columns = values.Select(v => $"[{v.Column}]");
        var vals = values.Select(v => v.Value == null ? "NULL" : $"'{v.Value}'");

        return Task.FromResult($@"
INSERT INTO [{schema}].[{table}]
({string.Join(", ", columns)})
VALUES ({string.Join(", ", vals)});");
    }

    public Task<string> GenerateUpdateScript(string database, string schema, string table, IEnumerable<ColumnValue> values, string whereClause)
    {
        var setStatements = values.Select(v =>
            $"[{v.Column}] = {(v.Value == null ? "NULL" : $"'{v.Value}'")}");

        return Task.FromResult($@"
UPDATE [{schema}].[{table}]
SET {string.Join(",\n    ", setStatements)}
WHERE {whereClause};");
    }

    public Task<string> GenerateDeleteScript(string database, string schema, string table, string whereClause)
    {
        return Task.FromResult($@"
DELETE FROM [{schema}].[{table}]
WHERE {whereClause};");
    }

    public Task<string> GenerateSelectTopScript(string database, string schema, string table, int count)
    {
        return Task.FromResult($@"
SELECT TOP {count} *
FROM [{schema}].[{table}];");
    }

    public Task<string> GenerateCountScript(string database, string schema, string table)
    {
        return Task.FromResult($@"
SELECT COUNT(*) FROM [{schema}].[{table}];");
    }
}
