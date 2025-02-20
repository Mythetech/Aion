using Aion.Core.Queries;
using Npgsql;

namespace Aion.Core.Database;

public class PostgreSqlProvider : IDatabaseProvider
{
    public DatabaseType DatabaseType => DatabaseType.PostgreSQL;

    public async Task<List<string>> GetDatabasesAsync(string connectionString)
    {
        var databases = new List<string>();
        
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        builder.Database = "postgres"; // Connect to default postgres database
        
        using var conn = new NpgsqlConnection(builder.ConnectionString);    
        await conn.OpenAsync();
        
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
        var result = new QueryResult();
        
        try 
        {
            using var conn = new NpgsqlConnection(connectionString);    
            await conn.OpenAsync(cancellationToken);
            
            using var cmd = new NpgsqlCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            // Get column names
            for (int i = 0; i < reader.FieldCount; i++)
            {
                result.Columns.Add(reader.GetName(i));
            }

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

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.Error = ex.Message;
            return result;
        }
    }

    public string UpdateConnectionString(string connectionString, string database)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = database
        };
        return builder.ConnectionString;
    }

    public int GetDefaultPort() => 5432;

    public bool ValidateConnectionString(string connectionString, out string? error)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            
            // Check required parameters
            if (string.IsNullOrEmpty(builder.Host))
            {
                error = "Host is required";
                return false;
            }
            
            if (string.IsNullOrEmpty(builder.Username))
            {
                error = "Username is required";
                return false;
            }
            
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
} 