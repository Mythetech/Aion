using Aion.Contracts.Database;

namespace Aion.Web.Providers;

public class SqliteWasmCommands : IStandardDatabaseCommands
{
    public Task<string> GenerateCreateDatabaseScript(string name)
    {
        return Task.FromResult($"-- Database '{name}' created (in-memory)");
    }

    public Task<string> GenerateDropDatabaseScript(string name)
    {
        return Task.FromResult($"-- Database '{name}' dropped");
    }

    public Task<string> GenerateBackupDatabaseScript(string name, string location)
    {
        return Task.FromResult("-- Backup not supported for in-memory WASM databases");
    }

    public Task<string> GenerateCreateTableScript(string database, string schema, string name, IEnumerable<ColumnDefinition> columns)
    {
        var columnDefs = columns.Select(c =>
        {
            var def = $"\"{c.Name}\" {c.DataType}";
            if (!c.IsNullable)
                def += " NOT NULL";
            if (c.DefaultValue != null)
                def += $" DEFAULT {c.DefaultValue}";
            return def;
        });

        return Task.FromResult($"CREATE TABLE \"{name}\" (\n    {string.Join(",\n    ", columnDefs)}\n);");
    }

    public Task<string> GenerateDropTableScript(string database, string schema, string name)
    {
        return Task.FromResult($"DROP TABLE IF EXISTS \"{name}\";");
    }

    public Task<string> GenerateAlterTableScript(string database, string schema, string name, IEnumerable<TableModification> modifications)
    {
        var statements = modifications.Select(mod => mod.Type switch
        {
            ModificationType.AddColumn =>
                $"ALTER TABLE \"{name}\" ADD COLUMN \"{mod.NewColumn!.Name}\" {mod.NewColumn.DataType}{(!mod.NewColumn.IsNullable ? " NOT NULL" : "")}{(mod.NewColumn.DefaultValue != null ? $" DEFAULT {mod.NewColumn.DefaultValue}" : "")};",
            ModificationType.DropColumn =>
                $"ALTER TABLE \"{name}\" DROP COLUMN \"{mod.ColumnName}\";",
            ModificationType.AlterColumn =>
                $"-- SQLite does not support ALTER COLUMN directly. Recreate the table to change column '{mod.ColumnName}'.",
            _ => throw new ArgumentOutOfRangeException()
        });

        return Task.FromResult(string.Join("\n", statements));
    }

    public Task<string> GenerateInsertScript(string database, string schema, string table, IEnumerable<ColumnValue> values)
    {
        var columns = values.Select(v => $"\"{v.Column}\"");
        var vals = values.Select(v => v.Value == null ? "NULL" : $"'{v.Value}'");

        return Task.FromResult($"INSERT INTO \"{table}\" ({string.Join(", ", columns)})\nVALUES ({string.Join(", ", vals)});");
    }

    public Task<string> GenerateUpdateScript(string database, string schema, string table, IEnumerable<ColumnValue> values, string whereClause)
    {
        var setStatements = values.Select(v =>
            $"\"{v.Column}\" = {(v.Value == null ? "NULL" : $"'{v.Value}'")}");

        return Task.FromResult($"UPDATE \"{table}\"\nSET {string.Join(",\n    ", setStatements)}\nWHERE {whereClause};");
    }

    public Task<string> GenerateDeleteScript(string database, string schema, string table, string whereClause)
    {
        return Task.FromResult($"DELETE FROM \"{table}\"\nWHERE {whereClause};");
    }

    public Task<string> GenerateSelectTopScript(string database, string schema, string table, int count)
    {
        return Task.FromResult($"SELECT * FROM \"{table}\"\nLIMIT {count};");
    }

    public Task<string> GenerateCountScript(string database, string schema, string table)
    {
        return Task.FromResult($"SELECT COUNT(*) FROM \"{table}\";");
    }
}
