using Microsoft.Data.SqlClient;
using System.Text;
using Aion.Core.Queries;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Database.SqlServer;

public class SqlServerProvider : IDatabaseProvider
{
    private readonly ILogger<SqlServerProvider> _logger;

    public SqlServerProvider(ILogger<SqlServerProvider> logger)
    {
        _logger = logger;
    }
    
    public IStandardDatabaseCommands Commands { get; } = new SqlServerCommands();
    public DatabaseType DatabaseType => DatabaseType.SQLServer;

    public async Task<List<string>?> GetDatabasesAsync(string connectionString)
    {
        var databases = new List<string>();
        
        var builder = new SqlConnectionStringBuilder(connectionString);
        builder.InitialCatalog = "master";
        
        using var conn = new SqlConnection(builder.ConnectionString);
        try
        {
            await conn.OpenAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SQL Server");
            return null;
        }

        const string sql = @"
            SELECT name 
            FROM sys.databases 
            WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb')
            ORDER BY name";
            
        using var cmd = new SqlCommand(sql, conn);
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
        
        using var conn = new SqlConnection(connectionString);    
        await conn.OpenAsync();
        
        const string sql = @"
            SELECT TABLE_NAME 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_TYPE = 'BASE TABLE' 
            AND TABLE_SCHEMA = 'dbo'
            ORDER BY TABLE_NAME";
            
        using var cmd = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    public async Task<List<ColumnInfo>> GetColumnsAsync(string connectionString, string database, string table)
    {
        var columns = new List<ColumnInfo>();
        
        using var conn = new SqlConnection(connectionString);    
        await conn.OpenAsync();
        
        const string sql = @"
            SELECT 
                c.COLUMN_NAME,
                c.DATA_TYPE,
                CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END as IS_NULLABLE,
                c.COLUMN_DEFAULT,
                c.CHARACTER_MAXIMUM_LENGTH,
                CASE 
                    WHEN EXISTS (
                        SELECT 1 FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                        JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc 
                            ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                        WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                            AND ku.TABLE_NAME = @table
                            AND ku.COLUMN_NAME = c.COLUMN_NAME
                    ) THEN 1 ELSE 0 
                END as IS_PRIMARY_KEY,
                COLUMNPROPERTY(OBJECT_ID(@table), c.COLUMN_NAME, 'IsIdentity') as IS_IDENTITY
            FROM INFORMATION_SCHEMA.COLUMNS c
            WHERE c.TABLE_NAME = @table
            ORDER BY c.ORDINAL_POSITION";
            
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@table", table);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetInt32(2) == 1,
                DefaultValue = reader.IsDBNull(3) ? null : reader.GetString(3),
                MaxLength = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                IsPrimaryKey = reader.GetInt32(5) == 1,
                IsIdentity = reader.GetInt32(6) == 1
            });
        }

        return columns;
    }

    public async Task<QueryResult> ExecuteQueryAsync(string connectionString, string query, CancellationToken cancellationToken)
    {
        var result = new QueryResult();
        
        try 
        {
            using var conn = new SqlConnection(connectionString);    
            await conn.OpenAsync(cancellationToken);
            
            using var cmd = new SqlCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            for (int i = 0; i < reader.FieldCount; i++)
            {
                result.Columns.Add(reader.GetName(i));
            }

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
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = database
        };
        return builder.ConnectionString;
    }

    public int GetDefaultPort() => 1433;

    public bool ValidateConnectionString(string connectionString, out string? error)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            
            if (string.IsNullOrEmpty(builder.DataSource))
            {
                error = "Server is required";
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

    public async Task<QueryPlan> GetEstimatedPlanAsync(string connectionString, string query)
    {
        var plan = new QueryPlan
        {
            PlanType = "Estimated",
            PlanFormat = "XML"
        };

        try
        {
            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            
            using var cmd = new SqlCommand($"SET SHOWPLAN_XML ON; {query}", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                plan.PlanContent = reader.GetString(0);
            }

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
            PlanFormat = "XML"
        };

        try
        {
            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            
            using var cmd = new SqlCommand($"SET STATISTICS XML ON; {query}", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            // Skip the result set
            while (await reader.NextResultAsync())
            {
                if (reader.GetName(0) == "Microsoft SQL Server 2005 XML Showplan")
                {
                    await reader.ReadAsync();
                    plan.PlanContent = reader.GetString(0);
                    break;
                }
            }

            return plan;
        }
        catch (Exception ex)
        {
            plan.PlanContent = $"Error getting plan: {ex.Message}";
            return plan;
        }
    }
} 