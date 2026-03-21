using Aion.Components.Connections;
using Aion.Core.Database;

namespace Aion.Components.ForeignKeys;

public class ForeignKeyService : IForeignKeyService
{
    private readonly ConnectionState _connectionState;

    public ForeignKeyService(ConnectionState connectionState)
    {
        _connectionState = connectionState;
    }

    public async Task<Dictionary<string, object>?> FetchReferencedRowAsync(
        Guid connectionId,
        string database,
        string referencedTable,
        string referencedColumn,
        object foreignKeyValue,
        CancellationToken cancellationToken = default)
    {
        var connection = _connectionState.Connections.FirstOrDefault(c => c.Id == connectionId);
        if (connection == null) return null;

        var provider = _connectionState.GetProvider(connection.Type);
        var connectionString = provider.UpdateConnectionString(connection.ConnectionString, database);

        var query = BuildSelectQuery(provider.DatabaseType, referencedTable, referencedColumn, foreignKeyValue);

        var result = await provider.ExecuteQueryAsync(connectionString, query, cancellationToken);

        if (!result.Success || result.Rows.Count == 0)
        {
            return null;
        }

        return result.Rows.FirstOrDefault();
    }

    private static string BuildSelectQuery(DatabaseType dbType, string table, string column, object value)
    {
        var quotedTable = QuoteIdentifier(dbType, table);
        var quotedColumn = QuoteIdentifier(dbType, column);
        var formattedValue = FormatValue(value);

        return dbType switch
        {
            DatabaseType.PostgreSQL => $"SELECT * FROM {quotedTable} WHERE {quotedColumn} = {formattedValue} LIMIT 1",
            DatabaseType.MySQL => $"SELECT * FROM {quotedTable} WHERE {quotedColumn} = {formattedValue} LIMIT 1",
            DatabaseType.SQLServer => $"SELECT TOP 1 * FROM {quotedTable} WHERE {quotedColumn} = {formattedValue}",
            DatabaseType.LiteDB => $"SELECT $ FROM {table} WHERE {column} = {formattedValue} LIMIT 1",
            _ => throw new NotSupportedException($"Database type {dbType} is not supported for foreign key navigation")
        };
    }

    private static string QuoteIdentifier(DatabaseType dbType, string identifier)
    {
        return dbType switch
        {
            DatabaseType.PostgreSQL => $"\"{identifier}\"",
            DatabaseType.MySQL => $"`{identifier}`",
            DatabaseType.SQLServer => $"[{identifier}]",
            DatabaseType.LiteDB => identifier,
            _ => identifier
        };
    }

    private static string FormatValue(object value)
    {
        return value switch
        {
            null => "NULL",
            string s => $"'{EscapeString(s)}'",
            bool b => b ? "1" : "0",
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            DateTimeOffset dto => $"'{dto:yyyy-MM-dd HH:mm:ss}'",
            Guid g => $"'{g}'",
            _ when IsNumeric(value) => value.ToString()!,
            _ => $"'{EscapeString(value.ToString()!)}'"
        };
    }

    private static string EscapeString(string value)
    {
        return value.Replace("'", "''");
    }

    private static bool IsNumeric(object value)
    {
        return value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
    }
}
