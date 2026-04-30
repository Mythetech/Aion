using Aion.Contracts.Database;

namespace Aion.Web.Providers;

public class PGliteCommands : IStandardDatabaseCommands
{
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
        var schemaPrefix = string.IsNullOrEmpty(schema) || schema == "public" ? "" : $"\"{schema}\".";
        var columns = values.Select(v => $"\"{v.Column}\"");
        var vals = values.Select(v => v.Value == null ? "NULL" : $"'{v.Value}'");

        return Task.FromResult($"INSERT INTO {schemaPrefix}\"{table}\"\n({string.Join(", ", columns)})\nVALUES ({string.Join(", ", vals)});");
    }

    public Task<string> GenerateUpdateScript(string database, string schema, string table, IEnumerable<ColumnValue> values, string whereClause)
    {
        var schemaPrefix = string.IsNullOrEmpty(schema) || schema == "public" ? "" : $"\"{schema}\".";
        var setStatements = values.Select(v =>
            $"\"{v.Column}\" = {(v.Value == null ? "NULL" : $"'{v.Value}'")}");

        return Task.FromResult($"UPDATE {schemaPrefix}\"{table}\"\nSET {string.Join(",\n    ", setStatements)}\nWHERE {whereClause};");
    }

    public Task<string> GenerateDeleteScript(string database, string schema, string table, string whereClause)
    {
        var schemaPrefix = string.IsNullOrEmpty(schema) || schema == "public" ? "" : $"\"{schema}\".";
        return Task.FromResult($"DELETE FROM {schemaPrefix}\"{table}\"\nWHERE {whereClause};");
    }

    public Task<string> GenerateSelectTopScript(string database, string schema, string table, int count)
    {
        var schemaPrefix = string.IsNullOrEmpty(schema) || schema == "public" ? "" : $"\"{schema}\".";
        return Task.FromResult($"SELECT * FROM {schemaPrefix}\"{table}\"\nLIMIT {count};");
    }

    public Task<string> GenerateCountScript(string database, string schema, string table)
    {
        var schemaPrefix = string.IsNullOrEmpty(schema) || schema == "public" ? "" : $"\"{schema}\".";
        return Task.FromResult($"SELECT COUNT(*) FROM {schemaPrefix}\"{table}\";");
    }
}
