using Aion.Components.Connections;
using Aion.Components.Querying;
using Npgsql;

namespace Aion.Desktop;

public class ConnectionService : IConnectionService
{
    public async Task<List<string>> ConnectAsync(string connectionString)
    {
        var tables = new List<string>();
        using var conn = new NpgsqlConnection(connectionString);    
        await conn.OpenAsync();
        
        using var cmd = new NpgsqlCommand("select table_name from information_schema.tables where table_schema = 'public'", conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    public async Task<QueryResult> ExecuteQueryAsync(string connectionString, string query)
    {
        Console.WriteLine($"ConnectionService executing query: {query}");
        var result = new QueryResult();
        
        try 
        {
            using var conn = new NpgsqlConnection(connectionString);    
            Console.WriteLine("Opening connection...");
            await conn.OpenAsync();
            
            using var cmd = new NpgsqlCommand(query, conn);
            Console.WriteLine("Executing reader...");
            using var reader = await cmd.ExecuteReaderAsync();
            Console.WriteLine($"Reader executed, has rows: {reader.HasRows}");

            // Get column names
            for (int i = 0; i < reader.FieldCount; i++)
            {
                result.Columns.Add(reader.GetName(i));
            }
            Console.WriteLine($"Found {result.Columns.Count} columns");

            // Read rows
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    row[result.Columns[i]] = value == DBNull.Value ? null : value;
                }
                result.Rows.Add(row);
            }
            Console.WriteLine($"Read {result.Rows.Count} rows");

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing query: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            result.Error = ex.Message;
            return result;
        }
    }
}