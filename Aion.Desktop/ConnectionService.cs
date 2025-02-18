using Aion.Components.Connections;
using Aion.Components.Querying;
using Npgsql;

namespace Aion.Desktop;

public class ConnectionService : IConnectionService
{
    public async Task<List<string>> GetDatabasesAsync(string connectionString)
    {
        var databases = new List<string>();
        
        // For Postgres, we need to connect to the 'postgres' database to list other databases
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        builder.Database = "postgres";
        
        using var conn = new NpgsqlConnection(builder.ConnectionString);    
        await conn.OpenAsync();
        
        // Query to get all databases, excluding system databases
        const string sql = @"
            SELECT datname 
            FROM pg_database 
            WHERE datistemplate = false 
            AND datname NOT IN ('postgres')
            ORDER BY datname";
            
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            databases.Add(reader.GetString(0));
        }

        return databases;
    }

    public async Task<List<string>> GetTablesAsync(string connectionString, string database)
    {
        var tables = new List<string>();
        
        using var conn = new NpgsqlConnection(connectionString);    
        await conn.OpenAsync();
        
        // Query to get all user tables in the public schema
        const string sql = @"
            SELECT table_name 
            FROM information_schema.tables 
            WHERE table_schema = 'public' 
            AND table_type = 'BASE TABLE'
            ORDER BY table_name";
            
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    public async Task<QueryResult> ExecuteQueryAsync(string connectionString, string query, CancellationToken cancellationToken)
    {
        Console.WriteLine($"ConnectionService executing query: {query}");
        var result = new QueryResult();
        
        try 
        {
            using var conn = new NpgsqlConnection(connectionString);    
            Console.WriteLine("Opening connection...");
            await conn.OpenAsync(cancellationToken);
            
            using var cmd = new NpgsqlCommand(query, conn);
            Console.WriteLine("Executing reader...");
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            Console.WriteLine($"Reader executed, has rows: {reader.HasRows}");

            // Get column names
            for (int i = 0; i < reader.FieldCount; i++)
            {
                result.Columns.Add(reader.GetName(i));
            }
            Console.WriteLine($"Found {result.Columns.Count} columns");

            // Read rows
            while (await reader.ReadAsync(cancellationToken))
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"Error executing query: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            result.Error = ex.Message;
            return result;
        }
    }
}