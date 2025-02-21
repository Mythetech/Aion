using Aion.Core.Database.MySql;
using Aion.Core.Queries;
using MySql.Data.MySqlClient;

namespace Aion.Core.Database;

public class MySqlProvider : IDatabaseProvider
{
    public IStandardDatabaseCommands Commands { get; } = new MySqlCommands();
    public DatabaseType DatabaseType => DatabaseType.MySQL;
    
    public async Task<List<string>> GetDatabasesAsync(string connectionString)
    {
        var databases = new List<string>();
        
        var builder = new MySqlConnectionStringBuilder(connectionString);
        builder.Database = null; // Don't specify a database when listing databases
        
        using var conn = new MySqlConnection(builder.ConnectionString);    
        await conn.OpenAsync();
        
        const string sql = @"
            SHOW DATABASES 
            WHERE `Database` NOT IN ('information_schema', 'mysql', 'performance_schema', 'sys');";
            
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
        
        const string sql = @"
            SELECT table_name 
            FROM information_schema.tables 
            WHERE table_schema = @database 
            AND table_type = 'BASE TABLE'
            ORDER BY table_name";
            
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
            // For CREATE DATABASE, we need to connect without a database specified
            var builder = new MySqlConnectionStringBuilder(connectionString);
            if (query.TrimStart().StartsWith("CREATE DATABASE", StringComparison.OrdinalIgnoreCase))
            {
                builder.Database = null;
            }
            
            using var conn = new MySqlConnection(builder.ConnectionString);    
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

            // Add default authentication if not specified
            if (!connectionString.Contains("AllowPublicKeyRetrieval"))
            {
                builder.AllowPublicKeyRetrieval = true;
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