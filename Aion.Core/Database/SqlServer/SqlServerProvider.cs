using Aion.Contracts.Database;
using Aion.Contracts.Metrics;
using Aion.Contracts.Queries;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Aion.Core.Database.SqlServer;

public class SqlServerProvider : IDatabaseProvider, IDatabaseIndexProvider, IDatabaseRoutineProvider, IQueryPlanParsingProvider,
    IDatabaseConnectionMetrics, IDatabaseServerHealthMetrics, IDatabaseStorageMetrics, IDatabasePerformanceMetrics
{
    private readonly ILogger<SqlServerProvider> _logger;

    public SqlServerProvider(ILogger<SqlServerProvider> logger)
    {
        _logger = logger;
    }

    public IStandardDatabaseCommands Commands { get; } = new SqlServerCommands();
    public DatabaseType DatabaseType => DatabaseType.SQLServer;
    public IReadOnlyList<string> SystemSchemas { get; } = ["sys", "INFORMATION_SCHEMA", "guest"];

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
            _logger.LogWarning(ex, "Failed to connect to SQL Server");
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

    public async Task<List<TableInfo>> GetTablesAsync(string connectionString, string database)
    {
        var tables = new List<TableInfo>();

        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_SCHEMA, TABLE_NAME";

        using var cmd = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            tables.Add(new TableInfo(reader.GetString(0), reader.GetString(1)));
        }

        return tables;
    }

    public async Task<List<ColumnInfo>> GetColumnsAsync(string connectionString, string database, string schema, string table)
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
                            AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                        WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                            AND ku.TABLE_NAME = @table
                            AND ku.TABLE_SCHEMA = @schema
                            AND ku.COLUMN_NAME = c.COLUMN_NAME
                    ) THEN 1 ELSE 0
                END as IS_PRIMARY_KEY,
                COLUMNPROPERTY(OBJECT_ID(@schemaTable), c.COLUMN_NAME, 'IsIdentity') as IS_IDENTITY
            FROM INFORMATION_SCHEMA.COLUMNS c
            WHERE c.TABLE_NAME = @table
            AND c.TABLE_SCHEMA = @schema
            ORDER BY c.ORDINAL_POSITION";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@table", table);
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@schemaTable", $"{schema}.{table}");
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

        var foreignKeys = await GetForeignKeysAsync(connectionString, database, schema, table);
        foreach (var fk in foreignKeys)
        {
            var column = columns.FirstOrDefault(c => c.Name == fk.ColumnName);
            if (column != null)
            {
                column.ForeignKey = fk;
            }
        }

        return columns;
    }

    public async Task<List<ForeignKeyInfo>> GetForeignKeysAsync(string connectionString, string database, string schema, string table)
    {
        var foreignKeys = new List<ForeignKeyInfo>();

        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT
                fk.name AS constraint_name,
                COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS column_name,
                OBJECT_NAME(fkc.referenced_object_id) AS referenced_table,
                COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS referenced_column,
                SCHEMA_NAME(rt.schema_id) AS referenced_schema
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc
                ON fk.object_id = fkc.constraint_object_id
            INNER JOIN sys.tables rt
                ON fkc.referenced_object_id = rt.object_id
            WHERE OBJECT_NAME(fk.parent_object_id) = @table
                AND OBJECT_SCHEMA_NAME(fk.parent_object_id) = @schema";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@table", table);
        cmd.Parameters.AddWithValue("@schema", schema);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            foreignKeys.Add(new ForeignKeyInfo
            {
                ConstraintName = reader.GetString(0),
                ColumnName = reader.GetString(1),
                ReferencedTable = reader.GetString(2),
                ReferencedColumn = reader.GetString(3),
                ReferencedSchema = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }

        return foreignKeys;
    }

    public async Task<List<IndexInfo>> GetIndexesAsync(string connectionString, string database)
    {
        var rows = new List<(string Schema, string Table, string IndexName, bool IsUnique, bool IsPrimary, string Column, int KeyOrdinal)>();

        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT
                s.name            AS schema_name,
                t.name            AS table_name,
                i.name            AS index_name,
                i.is_unique,
                i.is_primary_key,
                c.name            AS column_name,
                ic.key_ordinal
            FROM sys.indexes i
            INNER JOIN sys.tables t         ON i.object_id = t.object_id
            INNER JOIN sys.schemas s        ON t.schema_id = s.schema_id
            INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            INNER JOIN sys.columns c        ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE i.type > 0
              AND i.name IS NOT NULL
              AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA', 'guest')
              AND ic.is_included_column = 0
            ORDER BY s.name, t.name, i.name, ic.key_ordinal";

        using var cmd = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                reader.GetBoolean(4),
                reader.GetString(5),
                reader.GetByte(6)));
        }

        return rows
            .GroupBy(r => new { r.Schema, r.Table, r.IndexName, r.IsUnique, r.IsPrimary })
            .Select(g => new IndexInfo(
                Schema: g.Key.Schema,
                TableSchema: g.Key.Schema,
                TableName: g.Key.Table,
                Name: g.Key.IndexName,
                IsUnique: g.Key.IsUnique,
                IsPrimary: g.Key.IsPrimary,
                Columns: g.OrderBy(r => r.KeyOrdinal).Select(r => r.Column).ToList()))
            .OrderBy(i => i.TableSchema)
            .ThenBy(i => i.TableName)
            .ThenBy(i => i.Name)
            .ToList();
    }

    public async Task<List<RoutineInfo>> GetRoutinesAsync(string connectionString, string database)
    {
        var routines = new List<RoutineInfo>();

        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        // sys.objects.type: FN = scalar fn, IF = inline TVF, TF = multi-statement TVF, P = stored procedure
        const string sql = @"
            SELECT
                s.name        AS schema_name,
                o.name        AS routine_name,
                o.type        AS routine_type
            FROM sys.objects o
            INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
            WHERE o.type IN ('FN', 'IF', 'TF', 'P')
              AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA', 'guest')
            ORDER BY s.name, o.name";

        using var cmd = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var schema = reader.GetString(0);
            var name = reader.GetString(1);
            var type = reader.GetString(2).Trim();

            var kind = type == "P" ? RoutineKind.Procedure : RoutineKind.Function;

            routines.Add(new RoutineInfo(
                Schema: schema,
                Name: name,
                Kind: kind,
                ReturnType: null,
                ArgumentSignature: null,
                Language: "T-SQL"));
        }

        return routines;
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

    public async Task<int> GetActiveConnectionCountAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        using var cmd = new SqlCommand("SELECT COUNT(*) FROM sys.dm_exec_connections", conn);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    public async Task<int> GetMaxConnectionsAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        using var cmd = new SqlCommand("SELECT @@MAX_CONNECTIONS", conn);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    public async Task<TimeSpan> GetServerUptimeAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        using var cmd = new SqlCommand("SELECT DATEDIFF(SECOND, sqlserver_start_time, GETDATE()) FROM sys.dm_os_sys_info", conn);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return TimeSpan.FromSeconds(Convert.ToDouble(result));
    }

    public async Task<string> GetServerVersionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        using var cmd = new SqlCommand("SELECT @@VERSION", conn);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        var fullVersion = result?.ToString() ?? string.Empty;
        return fullVersion.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? fullVersion;
    }

    public async Task<DatabaseSizeInfo> GetDatabaseSizeAsync(string connectionString, string databaseName, CancellationToken cancellationToken = default)
    {
        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        using var sizeCmd = new SqlCommand("SELECT SUM(size) * 8192 FROM sys.master_files WHERE database_id = DB_ID(@db)", conn);
        sizeCmd.Parameters.AddWithValue("@db", databaseName);
        var sizeResult = await sizeCmd.ExecuteScalarAsync(cancellationToken);
        var sizeBytes = sizeResult == null || sizeResult == DBNull.Value ? 0L : Convert.ToInt64(sizeResult);

        const string countSql = @"
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'";
        using var countCmd = new SqlCommand(countSql, conn);
        var countResult = await countCmd.ExecuteScalarAsync(cancellationToken);
        var tableCount = Convert.ToInt32(countResult);

        return new DatabaseSizeInfo(databaseName, sizeBytes, tableCount);
    }

    public async Task<IReadOnlyList<TableSizeInfo>> GetTableSizesAsync(string connectionString, string databaseName, CancellationToken cancellationToken = default)
    {
        var tables = new List<TableSizeInfo>();

        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT
                s.name + '.' + t.name AS table_name,
                SUM(a.total_pages) * 8192 AS size_bytes,
                SUM(p.rows) AS row_count
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            INNER JOIN sys.indexes i ON t.object_id = i.object_id
            INNER JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id
            INNER JOIN sys.allocation_units a ON p.partition_id = a.container_id
            WHERE i.index_id <= 1
              AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA', 'guest')
            GROUP BY s.name, t.name
            ORDER BY size_bytes DESC";

        using var cmd = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(new TableSizeInfo(
                TableName: reader.GetString(0),
                SizeBytes: reader.GetInt64(1),
                RowCount: reader.GetInt64(2)));
        }

        return tables;
    }

    public async Task<double?> GetCacheHitRatioAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT
                (SELECT cntr_value FROM sys.dm_os_performance_counters
                 WHERE counter_name = 'Buffer cache hit ratio'
                   AND object_name LIKE '%Buffer Manager%') * 1.0
                /
                NULLIF(
                    (SELECT cntr_value FROM sys.dm_os_performance_counters
                     WHERE counter_name = 'Buffer cache hit ratio base'
                       AND object_name LIKE '%Buffer Manager%'),
                0)";

        using var cmd = new SqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);

        if (result == null || result == DBNull.Value)
            return null;

        return Convert.ToDouble(result);
    }

    public async Task<IReadOnlyList<SlowQueryInfo>> GetSlowQueriesAsync(string connectionString, TimeSpan threshold, CancellationToken cancellationToken = default)
    {
        var queries = new List<SlowQueryInfo>();

        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT
                st.text AS query,
                r.start_time,
                DATEDIFF(MILLISECOND, r.start_time, GETDATE()) AS elapsed_ms,
                l.name AS username
            FROM sys.dm_exec_requests r
            CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) st
            LEFT JOIN sys.syslogins l ON r.login_name = l.name
            WHERE r.session_id != @@SPID
              AND r.start_time IS NOT NULL
              AND DATEDIFF(MILLISECOND, r.start_time, GETDATE()) > @thresholdMs
            ORDER BY elapsed_ms DESC";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@thresholdMs", (long)threshold.TotalMilliseconds);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var startedAt = reader.GetDateTime(1);
            var elapsedMs = reader.GetInt32(2);

            queries.Add(new SlowQueryInfo(
                Query: reader.GetString(0),
                Duration: TimeSpan.FromMilliseconds(elapsedMs),
                StartedAt: startedAt,
                Username: reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return queries;
    }

    public async Task<IReadOnlyList<ActiveQueryInfo>> GetActiveQueriesAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var queries = new List<ActiveQueryInfo>();

        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT
                st.text AS query,
                DATEDIFF(MILLISECOND, r.start_time, GETDATE()) AS elapsed_ms,
                r.login_name AS username,
                r.status AS state
            FROM sys.dm_exec_requests r
            CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) st
            WHERE r.session_id != @@SPID
              AND r.start_time IS NOT NULL
            ORDER BY elapsed_ms DESC";

        using var cmd = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            queries.Add(new ActiveQueryInfo(
                Query: reader.GetString(0),
                ElapsedTime: TimeSpan.FromMilliseconds(reader.GetInt32(1)),
                Username: reader.IsDBNull(2) ? null : reader.GetString(2),
                State: reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return queries;
    }
}
