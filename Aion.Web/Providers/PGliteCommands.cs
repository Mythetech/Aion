using Aion.Contracts.Database;
using Aion.Contracts.Database.Dialects;

namespace Aion.Web.Providers;

public class PGliteCommands : IStandardDatabaseCommands
{
    private static readonly PostgreSqlDialect Dialect = PostgreSqlDialect.Instance;

    public Task<string> GenerateCreateDatabaseScript(string name)
    {
        return Task.FromResult($"-- PGlite database '{name}' created (in-browser)");
    }

    public Task<string> GenerateDropDatabaseScript(string name)
    {
        return Task.FromResult($"-- PGlite database '{name}' dropped");
    }

    public Task<string> GenerateBackupDatabaseScript(string name, string location)
    {
        return Task.FromResult("-- Backup not supported for in-browser PGlite databases");
    }

    public Task<string> GenerateCreateTableScript(string database, string schema, string name, IEnumerable<ColumnDefinition> columns)
    {
        var columnDefs = columns.Select(c =>
            $"\"{c.Name}\" {c.DataType}{(c.IsPrimaryKey ? " PRIMARY KEY" : "")}{(!c.IsNullable && !c.IsPrimaryKey ? " NOT NULL" : "")}{(c.DefaultValue != null ? $" DEFAULT {c.DefaultValue}" : "")}");

        var schemaPrefix = string.IsNullOrEmpty(schema) || schema == "public" ? "" : $"\"{schema}\".";
        return Task.FromResult($"CREATE TABLE {schemaPrefix}\"{name}\" (\n    {string.Join(",\n    ", columnDefs)}\n);");
    }

    public Task<string> GenerateDropTableScript(string database, string schema, string name)
    {
        var schemaPrefix = string.IsNullOrEmpty(schema) || schema == "public" ? "" : $"\"{schema}\".";
        return Task.FromResult($"DROP TABLE IF EXISTS {schemaPrefix}\"{name}\";");
    }

    public Task<string> GenerateAlterTableScript(string database, string schema, string name, IEnumerable<TableModification> modifications)
    {
        var schemaPrefix = string.IsNullOrEmpty(schema) || schema == "public" ? "" : $"\"{schema}\".";

        var alterStatements = modifications.Select(mod => mod.Type switch
        {
            ModificationType.AddColumn =>
                $"ADD COLUMN \"{mod.NewColumn!.Name}\" {mod.NewColumn.DataType}{(!mod.NewColumn.IsNullable ? " NOT NULL" : "")}{(mod.NewColumn.DefaultValue != null ? $" DEFAULT {mod.NewColumn.DefaultValue}" : "")}",
            ModificationType.DropColumn =>
                $"DROP COLUMN \"{mod.ColumnName}\"",
            ModificationType.AlterColumn =>
                $"ALTER COLUMN \"{mod.ColumnName}\" TYPE {mod.NewColumn!.DataType}",
            _ => throw new ArgumentOutOfRangeException()
        });

        return Task.FromResult($"ALTER TABLE {schemaPrefix}\"{name}\"\n{string.Join(",\n", alterStatements)};");
    }

    public Task<string> GenerateInsertScript(string database, string schema, string table, IEnumerable<ColumnValue> values)
    {
        var columns = values.ToList();
        if (columns.Count == 0)
        {
            return Task.FromResult($"INSERT INTO {TableName(schema, table)}\nDEFAULT VALUES;");
        }

        return Task.FromResult(
            $"INSERT INTO {TableName(schema, table)}\n({Dialect.BuildColumnList(columns)})\nVALUES ({Dialect.BuildValueList(columns)});");
    }

    public Task<string> GenerateUpdateScript(string database, string schema, string table, IEnumerable<ColumnValue> values, IEnumerable<ColumnValue> keyValues)
    {
        return Task.FromResult(
            $"UPDATE {TableName(schema, table)}\nSET {Dialect.BuildAssignments(values)}\nWHERE {Dialect.BuildKeyPredicate(keyValues)};");
    }

    public Task<string> GenerateDeleteScript(string database, string schema, string table, IEnumerable<ColumnValue> keyValues)
    {
        return Task.FromResult(
            $"DELETE FROM {TableName(schema, table)}\nWHERE {Dialect.BuildKeyPredicate(keyValues)};");
    }

    public Task<string> GenerateSelectTopScript(string database, string schema, string table, int count)
    {
        return Task.FromResult(Dialect.SelectRows(TableName(schema, table), limit: count));
    }

    public Task<string> GenerateCountScript(string database, string schema, string table)
    {
        return Task.FromResult($"SELECT COUNT(*) FROM {TableName(schema, table)};");
    }

    private static string TableName(string schema, string table) =>
        string.IsNullOrEmpty(schema) || schema == "public"
            ? Dialect.QuoteIdentifier(table)
            : $"{Dialect.QuoteIdentifier(schema)}.{Dialect.QuoteIdentifier(table)}";
}
