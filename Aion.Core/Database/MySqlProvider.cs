using Aion.Core.Queries;
using MySql.Data.MySqlClient;

namespace Aion.Core.Database;

public class MySqlProvider : IDatabaseProvider
{
    public DatabaseType DatabaseType => DatabaseType.MySQL;
    
    public async Task<List<string>> GetDatabasesAsync(string connectionString)
    {
        var databases = new List<string>();
        
        using var conn = new MySqlConnection(connectionString);    
        await conn.OpenAsync();
        
        // Query to get all databases, excluding system databases
        const string sql = @"
            SELECT SCHEMA_NAME 
            FROM information_schema.SCHEMATA 
            WHERE SCHEMA_NAME NOT IN 
                ('information_schema', 'mysql', 'performance_schema', 'sys')
            ORDER BY SCHEMA_NAME";
            
        using var cmd = new MySqlCommand(sql, conn);
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
        
        using var conn = new MySqlConnection(connectionString);    
        await conn.OpenAsync();
        
        // Query to get all tables in the database
        const string sql = @"
            SELECT TABLE_NAME 
            FROM information_schema.TABLES 
            WHERE TABLE_SCHEMA = @database 
            AND TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_NAME";
            
        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@database", database);
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
            using var conn = new MySqlConnection(connectionString);    
            await conn.OpenAsync(cancellationToken);
            
            using var cmd = new MySqlCommand(query, conn);
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
        var builder = new MySqlConnectionStringBuilder(connectionString)
        {
            Database = database
        };
        return builder.ConnectionString;
    }

    public int GetDefaultPort() => 3306;

    public bool ValidateConnectionString(string connectionString, out string? error)
    {
        try
        {
            var builder = new MySqlConnectionStringBuilder(connectionString);
            
            // Check required parameters
            if (string.IsNullOrEmpty(builder.Server))
            {
                error = "Server is required";
                return false;
            }
            
            if (string.IsNullOrEmpty(builder.UserID))
            {
                error = "User ID is required";
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