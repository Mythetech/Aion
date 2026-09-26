using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;
using System.Text;

namespace Aion.Core.Database.SqlServer;

public class SqlServerProvider : IDatabaseProvider, IDatabaseIndexProvider, IDatabaseRoutineProvider, IQueryPlanParsingProvider,
    IDatabaseRowEditingProvider, IEstimatedQueryPlanProvider, IActualQueryPlanProvider
{
    private const string TransactionNotOpenMessage = "This transaction is no longer open. Roll back to clear it.";
    private const string ShowplanColumnName = "Microsoft SQL Server 2005 XML Showplan";

    private readonly ILogger<SqlServerProvider> _logger;
    private readonly ConcurrentDictionary<string, OpenTransaction> _activeTransactions = new();

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

        // Contained users (Azure SQL) can only open their own database, so keep the one the user chose.
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
            builder.InitialCatalog = "master";

        using var conn = new SqlConnection(builder.ConnectionString);
        await conn.OpenAsync();

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

            // RecordsAffected is only final once every result set has been consumed, and is -1 when no statement changed rows.
            await reader.CloseAsync();
            result.RowsAffected = reader.RecordsAffected >= 0 ? reader.RecordsAffected : null;

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.SetError(SqlServerErrors.ToQueryError(ex, query));
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
        try
        {
            return await GetEstimatedPlanAsync(connectionString, query, CancellationToken.None);
        }
        catch (Exception ex)
        {
            return new QueryPlan { PlanType = "Estimated", PlanFormat = "XML", PlanContent = $"Error getting plan: {ex.Message}" };
        }
    }

    public async Task<QueryPlan> GetEstimatedPlanAsync(string connectionString, string query, CancellationToken cancellationToken)
    {
        await using var conn = await OpenPlanConnectionAsync(connectionString, cancellationToken);

        // SET SHOWPLAN_XML must be the only statement in its batch, so it gets its own command on the
        // same session. While it is on, the query is compiled but never executed.
        await ExecuteNonQueryAsync(conn, null, "SET SHOWPLAN_XML ON", cancellationToken);
        try
        {
            await using var cmd = new SqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            var planXml = await ReadShowplanXmlAsync(reader, cancellationToken)
                          ?? throw new InvalidOperationException("SQL Server returned no plan for this statement.");

            return new QueryPlan { PlanType = "Estimated", PlanFormat = "XML", PlanContent = planXml };
        }
        finally
        {
            if (conn.State == ConnectionState.Open)
            {
                await ExecuteNonQueryAsync(conn, null, "SET SHOWPLAN_XML OFF", CancellationToken.None);
            }
        }
    }

    public async Task<QueryPlan> GetActualPlanAsync(string connectionString, string query)
    {
        try
        {
            return await GetActualPlanAsync(connectionString, query, CancellationToken.None);
        }
        catch (Exception ex)
        {
            return new QueryPlan { PlanType = "Actual", PlanFormat = "XML", PlanContent = $"Error getting plan: {ex.Message}" };
        }
    }

    public async Task<QueryPlan> GetActualPlanAsync(string connectionString, string query, CancellationToken cancellationToken)
    {
        var refusal = QueryPlanStatementGuard.RejectTransactionControl(query);
        if (refusal != null) throw new InvalidOperationException(refusal);

        await using var conn = await OpenPlanConnectionAsync(connectionString, cancellationToken);

        // Disposing an uncommitted SqlTransaction (and closing the unpooled session) rolls it back,
        // which covers the failure paths.
        await using var transaction = (SqlTransaction)await conn.BeginTransactionAsync(cancellationToken);
        await ExecuteNonQueryAsync(conn, transaction, "SET STATISTICS XML ON", cancellationToken);

        string? planXml;
        await using (var cmd = new SqlCommand(query, conn, transaction))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            planXml = await ReadShowplanXmlAsync(reader, cancellationToken);
        }

        await transaction.RollbackAsync(CancellationToken.None);

        return new QueryPlan
        {
            PlanType = "Actual",
            PlanFormat = "XML",
            PlanContent = planXml ?? throw new InvalidOperationException("SQL Server returned no plan for this statement.")
        };
    }

    /// <summary>
    /// Plan sessions change SET options, so they bypass the pool: a session that failed before the
    /// options were reset must never be handed to a later query, where SHOWPLAN_XML would silently
    /// stop statements from executing.
    /// </summary>
    private static async Task<SqlConnection> OpenPlanConnectionAsync(string connectionString, CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(connectionString) { Pooling = false };
        var conn = new SqlConnection(builder.ConnectionString);
        try
        {
            await conn.OpenAsync(cancellationToken);
            return conn;
        }
        catch
        {
            await conn.DisposeAsync();
            throw;
        }
    }

    private static async Task ExecuteNonQueryAsync(SqlConnection conn, SqlTransaction? transaction, string sql, CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(sql, conn, transaction);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string?> ReadShowplanXmlAsync(SqlDataReader reader, CancellationToken cancellationToken)
    {
        string? planXml = null;
        do
        {
            if (planXml == null && reader.FieldCount == 1 && reader.GetName(0) == ShowplanColumnName
                && await reader.ReadAsync(cancellationToken))
            {
                planXml = reader.GetString(0);
            }
        } while (await reader.NextResultAsync(cancellationToken));

        return planXml;
    }

    public async Task<TransactionInfo> BeginTransactionAsync(string connectionString)
    {
        var conn = new SqlConnection(connectionString);
        try
        {
            await conn.OpenAsync();
            var dbTransaction = (SqlTransaction)await conn.BeginTransactionAsync();

            var transaction = new TransactionInfo();
            _activeTransactions[transaction.Id] = new OpenTransaction(conn, dbTransaction);
            return transaction;
        }
        catch
        {
            await conn.DisposeAsync();
            throw;
        }
    }

    public async Task CommitTransactionAsync(string connectionString, string transactionId)
    {
        if (!_activeTransactions.TryRemove(transactionId, out var open))
        {
            throw new InvalidOperationException(TransactionNotOpenMessage);
        }

        await using (open)
        {
            await open.Transaction.CommitAsync();
        }
    }

    public async Task RollbackTransactionAsync(string connectionString, string transactionId)
    {
        if (!_activeTransactions.TryRemove(transactionId, out var open)) return;

        await using (open)
        {
            await open.Transaction.RollbackAsync();
        }
    }

    public async Task<QueryResult> ExecuteInTransactionAsync(string connectionString, string query, string transactionId,
        CancellationToken cancellationToken)
    {
        if (!_activeTransactions.TryGetValue(transactionId, out var open))
        {
            return new QueryResult { Error = TransactionNotOpenMessage };
        }

        var result = new QueryResult();

        try
        {
            await using var cmd = new SqlCommand(query, open.Connection, open.Transaction);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

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

            // Grid edits apply multi-row changes in a transaction and check each statement changed exactly one row.
            await reader.CloseAsync();
            result.RowsAffected = reader.RecordsAffected >= 0 ? reader.RecordsAffected : null;

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.SetError(SqlServerErrors.ToQueryError(ex, query));
            return result;
        }
    }

    private sealed record OpenTransaction(SqlConnection Connection, SqlTransaction Transaction) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Transaction.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
