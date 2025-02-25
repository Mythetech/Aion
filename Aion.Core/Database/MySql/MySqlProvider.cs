using Aion.Core.Database.MySql;
using Aion.Core.Queries;
using MySql.Data.MySqlClient;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Database;

public class MySqlProvider : IDatabaseProvider
{
    private readonly ILogger<MySqlProvider> _logger;

    public MySqlProvider(ILogger<MySqlProvider> logger)
    {
        _logger = logger;
    }
    
    public IStandardDatabaseCommands Commands { get; } = new MySqlCommands();
    public DatabaseType DatabaseType => DatabaseType.MySQL;
    
    public async Task<List<string>?> GetDatabasesAsync(string connectionString)
    {
        var databases = new List<string>();
        
        var builder = new MySqlConnectionStringBuilder(connectionString);
        builder.Database = null; 
        
        using var conn = new MySqlConnection(builder.ConnectionString);
        try
        {
            await conn.OpenAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return null;
        }

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

    public async Task<QueryPlan> GetEstimatedPlanAsync(string connectionString, string query)
    {
        var plan = new QueryPlan
        {
            PlanType = "Estimated",
            PlanFormat = "TEXT"
        };

        try
        {
            using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync();
            
            using var cmd = new MySqlCommand($"EXPLAIN FORMAT=JSON {query}", conn);
            var result = await cmd.ExecuteScalarAsync();

            plan.PlanFormat = "JSON";
            plan.PlanContent = result?.ToString() ?? string.Empty;
            return plan;
        }
        catch (Exception ex)
        {
            plan.PlanContent = $"Error getting plan: {ex.Message}";
            return plan;
        }
    }

    public async Task<QueryPlan> GetActualPlanAsync(string connectionString, string query)
    {
        var plan = new QueryPlan
        {
            PlanType = "Actual",
            PlanFormat = "TEXT"
        };

        try
        {
            using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync();
            
            // MySQL 8.0+ supports EXPLAIN ANALYZE
            using var cmd = new MySqlCommand($"EXPLAIN ANALYZE {query}", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var planText = new StringBuilder();
            while (await reader.ReadAsync())
            {
                // EXPLAIN ANALYZE returns a single column with the plan
                planText.AppendLine(reader.GetString(0));
            }

            plan.PlanContent = planText.ToString();
            return plan;
        }
        catch (MySqlException ex) when (ex.Message.Contains("ANALYZE", StringComparison.OrdinalIgnoreCase))
        {
            // Fallback for older MySQL versions that don't support EXPLAIN ANALYZE
            plan.PlanContent = "EXPLAIN ANALYZE is only supported in MySQL 8.0+";
            return plan;
        }
        catch (Exception ex)
        {
            plan.PlanContent = $"Error getting plan: {ex.Message}";
            return plan;
        }
    }

    public async Task<TransactionInfo> BeginTransactionAsync(string connectionString)
    {
        throw new NotImplementedException();
    }

    public async Task CommitTransactionAsync(string connectionString, string transactionId)
    {
        throw new NotImplementedException();
    }

    public async Task RollbackTransactionAsync(string connectionString, string transactionId)
    {
        throw new NotImplementedException();
    }

    public async Task<QueryResult> ExecuteInTransactionAsync(string connectionString, string query, string transactionId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task<List<ColumnInfo>> GetColumnsAsync(string connectionString, string database, string table)
    {
        var columns = new List<ColumnInfo>();
        
        using var conn = new MySqlConnection(connectionString);    
        await conn.OpenAsync();
        
        const string sql = @"
            SELECT 
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE = 'YES' as IS_NULLABLE,
                c.COLUMN_DEFAULT,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.COLUMN_KEY = 'PRI' as IS_PRIMARY_KEY,
                c.EXTRA = 'auto_increment' as IS_IDENTITY
            FROM information_schema.COLUMNS c
            WHERE c.TABLE_SCHEMA = @database
            AND c.TABLE_NAME = @table
            ORDER BY c.ORDINAL_POSITION";
            
        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@database", database);
        cmd.Parameters.AddWithValue("@table", table);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetBoolean(2),
                DefaultValue = reader.IsDBNull(3) ? null : reader.GetString(3),
                MaxLength = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                IsPrimaryKey = reader.GetBoolean(5),
                IsIdentity = reader.GetBoolean(6)
            });
        }

        return columns;
    }
} 