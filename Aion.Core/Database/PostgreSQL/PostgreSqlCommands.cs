
namespace Aion.Core.Database.PostgreSQL;

public class PostgreSqlCommands : IStandardDatabaseCommands
{
    public Task<string> GenerateCreateDatabaseScript(string name)
    {
        return Task.FromResult($@"
CREATE DATABASE ""{name}""
    WITH 
    OWNER = postgres
    ENCODING = 'UTF8'
    LC_COLLATE = 'en_US.utf8'
    LC_CTYPE = 'en_US.utf8'
    TABLESPACE = pg_default
    CONNECTION LIMIT = -1;");
    }

    public Task<string> GenerateDropDatabaseScript(string name)
    {
        return Task.FromResult($@"
DROP DATABASE IF EXISTS ""{name}"";");
    }

    public Task<string> GenerateBackupDatabaseScript(string name, string location)
    {
        return Task.FromResult($@"
pg_dump ""{name}"" > ""{location}"";");
    }

    public Task<string> GenerateCreateTableScript(string database, string name, IEnumerable<ColumnDefinition> columns)
    {
        var columnDefs = columns.Select(c => 
            $"{c.Name} {c.DataType} {(c.IsNullable ? "NULL" : "NOT NULL")} {(c.DefaultValue != null ? $"DEFAULT {c.DefaultValue}" : "")}");
            
        return Task.FromResult($@"
CREATE TABLE ""{name}"" (
    {string.Join(",\n    ", columnDefs)}
);");
    }

    public Task<string> GenerateDropTableScript(string database, string name)
    {
        return Task.FromResult($@"
DROP TABLE IF EXISTS ""{name}"";");
    }

    public Task<string> GenerateAlterTableScript(string database, string name, IEnumerable<TableModification> modifications)
    {
        var alterStatements = modifications.Select(mod => mod.Type switch
        {
            ModificationType.AddColumn => 
                $"ADD COLUMN {mod.NewColumn!.Name} {mod.NewColumn.DataType} {(mod.NewColumn.IsNullable ? "NULL" : "NOT NULL")} {(mod.NewColumn.DefaultValue != null ? $"DEFAULT {mod.NewColumn.DefaultValue}" : "")}",
            ModificationType.DropColumn => 
                $"DROP COLUMN \"{mod.ColumnName}\"",
            ModificationType.AlterColumn => 
                $"ALTER COLUMN \"{mod.ColumnName}\" TYPE {mod.NewColumn!.DataType} {(mod.NewColumn.IsNullable ? "DROP NOT NULL" : "SET NOT NULL")}",
            _ => throw new ArgumentOutOfRangeException()
        });

        return Task.FromResult($@"
ALTER TABLE ""{name}""
{string.Join(",\n", alterStatements)};");
    }

    public Task<string> GenerateInsertScript(string database, string table, IEnumerable<ColumnValue> values)
    {
        var columns = values.Select(v => $"\"{v.Column}\"");
        var vals = values.Select(v => v.Value == null ? "NULL" : $"'{v.Value}'");

        return Task.FromResult($@"
INSERT INTO ""{table}""
({string.Join(", ", columns)})
VALUES ({string.Join(", ", vals)});");
    }

    public Task<string> GenerateUpdateScript(string database, string table, IEnumerable<ColumnValue> values, string whereClause)
    {
        var setStatements = values.Select(v =>
            $"\"{v.Column}\" = {(v.Value == null ? "NULL" : $"'{v.Value}'")}");

        return Task.FromResult($@"
UPDATE ""{table}""
SET {string.Join(",\n    ", setStatements)}
WHERE {whereClause};");
    }

    public Task<string> GenerateDeleteScript(string database, string table, string whereClause)
    {
        return Task.FromResult($@"
DELETE FROM ""{table}""
WHERE {whereClause};");
    }

    public Task<string> GenerateSelectTopScript(string database, string table, int count)
    {
        return Task.FromResult($@"
SELECT * FROM ""{table}""
LIMIT {count};");
    }

    public Task<string> GenerateCountScript(string database, string table)
    {
        return Task.FromResult($@"
SELECT COUNT(*) FROM ""{table}"";");
    }
}