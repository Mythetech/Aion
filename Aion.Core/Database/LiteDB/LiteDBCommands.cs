namespace Aion.Core.Database.LiteDB;

public class LiteDBCommands : IStandardDatabaseCommands
{
    public Task<string> GenerateCreateDatabaseScript(string name)
    {
        return Task.FromResult($@"-- LiteDB creates databases automatically when opening a file
-- To create a new database, simply connect with a new filename: {name}.db");
    }

    public Task<string> GenerateDropDatabaseScript(string name)
    {
        return Task.FromResult($@"-- To drop a LiteDB database, delete the database file: {name}.db
-- This cannot be done via SQL commands");
    }

    public Task<string> GenerateBackupDatabaseScript(string name, string location)
    {
        return Task.FromResult($@"-- To backup a LiteDB database, copy the file to: {location}
-- Ensure the database is not in use when copying");
    }

    public Task<string> GenerateCreateTableScript(string database, string collection, IEnumerable<ColumnDefinition> columns)
    {
        var fields = columns.Select(c => $"    {c.Name}: <{c.DataType}>");

        return Task.FromResult($@"-- LiteDB collections are created automatically on first insert
-- Sample document structure for '{collection}':
-- {{
{string.Join(",\n", fields)}
-- }}

INSERT INTO {collection} VALUES {{ _id: OBJECTID() }}");
    }

    public Task<string> GenerateDropTableScript(string database, string collection)
    {
        return Task.FromResult($"DROP COLLECTION {collection}");
    }

    public Task<string> GenerateAlterTableScript(string database, string name, IEnumerable<TableModification> modifications)
    {
        return Task.FromResult($@"-- LiteDB is schemaless - documents can have any structure
-- To add/modify fields, simply insert/update documents with the new structure
-- Existing documents are not affected

-- Example: Add a new field to all documents
UPDATE {name} SET newField = 'default value'");
    }

    public Task<string> GenerateInsertScript(string database, string collection, IEnumerable<ColumnValue> values)
    {
        var fields = values.Select(v => $"    {v.Column}: {FormatBsonValue(v.Value)}");

        return Task.FromResult($@"INSERT INTO {collection} VALUES {{
{string.Join(",\n", fields)}
}}");
    }

    public Task<string> GenerateUpdateScript(string database, string collection, IEnumerable<ColumnValue> values, string whereClause)
    {
        var setStatements = values.Select(v => $"{v.Column} = {FormatBsonValue(v.Value)}");

        return Task.FromResult($@"UPDATE {collection}
SET {string.Join(", ", setStatements)}
WHERE {whereClause}");
    }

    public Task<string> GenerateDeleteScript(string database, string collection, string whereClause)
    {
        return Task.FromResult($"DELETE {collection} WHERE {whereClause}");
    }

    public Task<string> GenerateSelectTopScript(string database, string collection, int count)
    {
        return Task.FromResult($"SELECT $ FROM {collection} LIMIT {count}");
    }

    public Task<string> GenerateCountScript(string database, string collection)
    {
        return Task.FromResult($"SELECT COUNT(*) FROM {collection}");
    }

    private static string FormatBsonValue(object? value)
    {
        return value switch
        {
            null => "null",
            string s => $"'{s.Replace("'", "\\'")}'",
            bool b => b.ToString().ToLower(),
            DateTime dt => $"DATETIME('{dt:yyyy-MM-ddTHH:mm:ss}')",
            int or long or double or decimal or float => value.ToString()!,
            Guid g => $"GUID('{g}')",
            _ => $"'{value}'"
        };
    }
}
