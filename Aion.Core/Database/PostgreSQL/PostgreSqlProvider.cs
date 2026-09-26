using Aion.Contracts.Database;
using Aion.Contracts.Database.Dialects;
using Aion.Contracts.Queries;
using Aion.Core.Database.PostgreSQL;
using Npgsql;
using System.Collections.Concurrent;
using System.Text;

namespace Aion.Core.Database;

public class PostgreSqlProvider : IDatabaseProvider, IDatabaseIndexProvider, IDatabaseRoutineProvider, IQueryPlanParsingProvider,
    IDatabaseRowEditingProvider, IEstimatedQueryPlanProvider, IActualQueryPlanProvider, ISqlDialectProvider, IDatabaseCreationProvider
{
    private const string TransactionNotOpenMessage = "This transaction is no longer open. Roll back to clear it.";

    private readonly ConcurrentDictionary<string, OpenTransaction> _activeTransactions = new();
    private readonly PostgreSqlPlanParser _planParser = new();
    public IStandardDatabaseCommands Commands { get; } = new PostgreSqlCommands();
    public SqlDialect Dialect => PostgreSqlDialect.Instance;
    public DatabaseType DatabaseType => DatabaseType.PostgreSQL;
    public IReadOnlyList<string> SystemSchemas { get; } = ["pg_catalog", "information_schema", "pg_toast"];

    public async Task<List<string>> GetDatabasesAsync(string connectionString)
    {
        var databases = new List<string>();

        // On Supabase and most Docker images "postgres" is the working database, so it stays in the list.
        // Users without access to it (managed hosts) can still list databases through the one they chose.
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
            builder.Database = "postgres";

        using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT datname
            FROM pg_database
            WHERE datistemplate = false
            ORDER BY datname";

        using var cmd = new NpgsqlCommand(sql, conn);
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

        using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // Row counts are estimates from the catalog, never a scan. n_live_tup follows every committed insert and
        // delete, while reltuples only moves on VACUUM, ANALYZE and CREATE INDEX (and is -1 until the first).
        // A zero n_live_tup beside a positive reltuples usually means the statistics were reset, so reltuples is
        // the better guess then. A partitioned table's rows live in its partitions, so it gets no estimate.
        const string sql = @"
            SELECT t.table_schema, t.table_name,
                CASE
                    WHEN c.relkind = 'p' THEN NULL
                    WHEN s.n_live_tup > 0 OR c.reltuples < 0 THEN s.n_live_tup
                    ELSE c.reltuples::bigint
                END AS estimated_rows
            FROM information_schema.tables t
            LEFT JOIN pg_namespace n ON n.nspname = t.table_schema
            LEFT JOIN pg_class c ON c.relnamespace = n.oid AND c.relname = t.table_name
            LEFT JOIN pg_stat_all_tables s ON s.relid = c.oid
            WHERE t.table_type = 'BASE TABLE'
            AND t.table_schema NOT LIKE 'pg_temp_%'
            AND t.table_schema NOT LIKE 'pg_toast_temp_%'
            ORDER BY t.table_schema, t.table_name";

        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            tables.Add(new TableInfo(reader.GetString(0), reader.GetString(1))
            {
                RowCount = reader.IsDBNull(2) ? null : TableRowCount.Estimated(reader.GetInt64(2))
            });
        }

        return tables;
    }

    public async Task<QueryResult> ExecuteQueryAsync(string connectionString, string query, CancellationToken cancellationToken)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (query.TrimStart().StartsWith("CREATE DATABASE", StringComparison.OrdinalIgnoreCase))
            {
                builder.Database = "postgres";
                connectionString = builder.ConnectionString;
            }

            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);

            using var cmd = new NpgsqlCommand(query, conn);
            return await ReadResultAsync(cmd, query, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var result = new QueryResult();
            result.SetError(PostgreSqlErrors.ToQueryError(ex, query));
            return result;
        }
    }

    /// <summary>
    /// Runs the command and reads its first result set. Failures are mapped here, while the command
    /// can still tell which of its statements failed.
    /// </summary>
    private static async Task<QueryResult> ReadResultAsync(NpgsqlCommand cmd, string query, CancellationToken cancellationToken)
    {
        var result = new QueryResult();

        try
        {
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            await QueryResultReader.ReadAsync(reader, result, cancellationToken);

            // RecordsAffected is only final once every result set has been consumed, and is -1 when no statement changed rows.
            await reader.CloseAsync();
            result.RowsAffected = reader.RecordsAffected >= 0 ? reader.RecordsAffected : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.SetError(PostgreSqlErrors.ToQueryError(ex, query, cmd));
        }

        return result;
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

    public async Task<QueryPlan> GetEstimatedPlanAsync(string connectionString, string query)
    {
        try
        {
            return await GetEstimatedPlanAsync(connectionString, query, CancellationToken.None);
        }
        catch (Exception ex)
        {
            return new QueryPlan { PlanType = "Estimated", PlanFormat = "TEXT", PlanContent = $"Error getting plan: {ex.Message}" };
        }
    }

    public async Task<QueryPlan> GetEstimatedPlanAsync(string connectionString, string query, CancellationToken cancellationToken)
    {
        var refusal = QueryPlanStatementGuard.RequireSingleStatement(query);
        if (refusal != null) throw new InvalidOperationException(refusal);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand($"EXPLAIN {QueryPlanStatementGuard.TrimTrailingTerminators(query)}", conn);

        return new QueryPlan
        {
            PlanType = "Estimated",
            PlanFormat = "TEXT",
            PlanContent = await ReadPlanTextAsync(cmd, cancellationToken)
        };
    }

    public async Task<QueryPlan> GetActualPlanAsync(string connectionString, string query)
    {
        try
        {
            return await GetActualPlanAsync(connectionString, query, CancellationToken.None);
        }
        catch (Exception ex)
        {
            return new QueryPlan { PlanType = "Actual", PlanFormat = "TEXT", PlanContent = $"Error getting plan: {ex.Message}" };
        }
    }

    public async Task<QueryPlan> GetActualPlanAsync(string connectionString, string query, CancellationToken cancellationToken)
    {
        var refusal = QueryPlanStatementGuard.GetActualPlanRefusal(query);
        if (refusal != null) throw new InvalidOperationException(refusal);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        // Disposing an uncommitted NpgsqlTransaction rolls it back, which covers the failure paths.
        await using var transaction = await conn.BeginTransactionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            $"EXPLAIN (ANALYZE) {QueryPlanStatementGuard.TrimTrailingTerminators(query)}", conn, transaction);

        var planText = await ReadPlanTextAsync(cmd, cancellationToken);
        await transaction.RollbackAsync(CancellationToken.None);

        return new QueryPlan
        {
            PlanType = "Actual",
            PlanFormat = "TEXT",
            PlanContent = planText
        };
    }

    private static async Task<string> ReadPlanTextAsync(NpgsqlCommand cmd, CancellationToken cancellationToken)
    {
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var planText = new StringBuilder();
        while (await reader.ReadAsync(cancellationToken))
        {
            planText.AppendLine(reader.GetString(0));
        }

        return planText.ToString();
    }

    public async Task<List<ColumnInfo>> GetColumnsAsync(string connectionString, string database, string schema, string table)
    {
        var columns = new List<ColumnInfo>();

        using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT
                c.column_name,
                c.data_type,
                c.is_nullable = 'YES' as is_nullable,
                c.column_default,
                c.character_maximum_length,
                CASE WHEN pk.constraint_type = 'PRIMARY KEY' THEN true ELSE false END as is_primary_key,
                CASE WHEN c.column_default LIKE 'nextval%' OR c.is_identity = 'YES' THEN true ELSE false END as is_identity
            FROM information_schema.columns c
            LEFT JOIN (
                SELECT ku.column_name, tc.constraint_type
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage ku
                    ON tc.constraint_name = ku.constraint_name
                WHERE tc.constraint_type = 'PRIMARY KEY'
                    AND ku.table_name = @table
                    AND ku.table_schema = @schema
            ) pk ON c.column_name = pk.column_name
            WHERE c.table_name = @table
            AND c.table_schema = @schema
            ORDER BY c.ordinal_position";

        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@table", table);
        cmd.Parameters.AddWithValue("@schema", schema);
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

        using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT
                tc.constraint_name,
                kcu.column_name,
                ccu.table_name AS referenced_table,
                ccu.column_name AS referenced_column,
                ccu.table_schema AS referenced_schema
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name
                AND tc.table_schema = kcu.table_schema
            JOIN information_schema.constraint_column_usage ccu
                ON ccu.constraint_name = tc.constraint_name
                AND ccu.table_schema = tc.table_schema
            WHERE tc.constraint_type = 'FOREIGN KEY'
                AND tc.table_name = @table
                AND tc.table_schema = @schema";

        using var cmd = new NpgsqlCommand(sql, conn);
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
        var indexes = new List<IndexInfo>();

        using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT
                n.nspname       AS schema_name,
                t.relname       AS table_name,
                i.relname       AS index_name,
                ix.indisunique  AS is_unique,
                ix.indisprimary AS is_primary,
                array_agg(a.attname ORDER BY k.ord) AS columns
            FROM pg_class t
            JOIN pg_index ix       ON t.oid = ix.indrelid
            JOIN pg_class i        ON i.oid = ix.indexrelid
            JOIN pg_namespace n    ON n.oid = t.relnamespace
            JOIN LATERAL unnest(ix.indkey) WITH ORDINALITY AS k(attnum, ord) ON TRUE
            JOIN pg_attribute a    ON a.attrelid = t.oid AND a.attnum = k.attnum
            WHERE t.relkind = 'r'
              AND n.nspname NOT IN ('pg_catalog', 'information_schema')
              AND n.nspname NOT LIKE 'pg_toast%'
              AND n.nspname NOT LIKE 'pg_temp_%'
            GROUP BY n.nspname, t.relname, i.relname, ix.indisunique, ix.indisprimary
            ORDER BY n.nspname, t.relname, i.relname";

        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var schema = reader.GetString(0);
            var tableName = reader.GetString(1);
            var indexName = reader.GetString(2);
            var isUnique = reader.GetBoolean(3);
            var isPrimary = reader.GetBoolean(4);
            var columns = (string[])reader.GetValue(5);

            indexes.Add(new IndexInfo(
                Schema: schema,
                TableSchema: schema,
                TableName: tableName,
                Name: indexName,
                IsUnique: isUnique,
                IsPrimary: isPrimary,
                Columns: columns));
        }

        return indexes;
    }

    public async Task<List<RoutineInfo>> GetRoutinesAsync(string connectionString, string database)
    {
        var routines = new List<RoutineInfo>();

        using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT
                n.nspname AS schema_name,
                p.proname AS routine_name,
                CASE p.prokind
                    WHEN 'p' THEN 'PROCEDURE'
                    ELSE 'FUNCTION'
                END AS routine_kind,
                CASE WHEN p.prokind = 'p' THEN NULL
                     ELSE pg_catalog.pg_get_function_result(p.oid)
                END AS return_type,
                pg_catalog.pg_get_function_arguments(p.oid) AS argument_signature,
                l.lanname AS language
            FROM pg_proc p
            JOIN pg_namespace n ON n.oid = p.pronamespace
            JOIN pg_language l  ON l.oid = p.prolang
            WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
              AND p.prokind IN ('f', 'p')
            ORDER BY n.nspname, p.proname";

        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var schema = reader.GetString(0);
            var name = reader.GetString(1);
            var kindText = reader.GetString(2);
            var returnType = reader.IsDBNull(3) ? null : reader.GetString(3);
            var args = reader.IsDBNull(4) ? null : reader.GetString(4);
            var language = reader.IsDBNull(5) ? null : reader.GetString(5);

            var kind = kindText == "PROCEDURE" ? RoutineKind.Procedure : RoutineKind.Function;

            routines.Add(new RoutineInfo(
                Schema: schema,
                Name: name,
                Kind: kind,
                ReturnType: returnType,
                ArgumentSignature: string.IsNullOrEmpty(args) ? "()" : $"({args})",
                Language: language));
        }

        return routines;
    }

    public async Task<TransactionInfo> BeginTransactionAsync(string connectionString)
    {
        var conn = new NpgsqlConnection(connectionString);
        try
        {
            await conn.OpenAsync();
            var dbTransaction = await conn.BeginTransactionAsync();

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
        if (!_activeTransactions.TryGetValue(transactionId, out var open))
        {
            throw new InvalidOperationException(TransactionNotOpenMessage);
        }

        await EnsureNotAbortedAsync(open);

        _activeTransactions.TryRemove(transactionId, out _);
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

    /// <summary>
    /// PostgreSQL answers COMMIT on a transaction that an earlier error aborted with a silent ROLLBACK.
    /// Probing first lets the user see that nothing would be committed and keeps the transaction open
    /// so they can roll it back deliberately.
    /// </summary>
    private static async Task EnsureNotAbortedAsync(OpenTransaction open)
    {
        try
        {
            await using var probe = new NpgsqlCommand("SELECT 1", open.Connection, open.Transaction);
            await probe.ExecuteScalarAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InFailedSqlTransaction)
        {
            throw new InvalidOperationException(
                "An earlier statement failed, so PostgreSQL aborted this transaction and nothing can be committed. Roll back to end it.", ex);
        }
    }

    public async Task<QueryResult> ExecuteInTransactionAsync(string connectionString, string query, string transactionId, CancellationToken cancellationToken)
    {
        if (!_activeTransactions.TryGetValue(transactionId, out var open))
        {
            return new QueryResult { Error = TransactionNotOpenMessage };
        }

        using var cmd = new NpgsqlCommand(query, open.Connection, open.Transaction);
        return await ReadResultAsync(cmd, query, cancellationToken);
    }

    public QueryPlanTree? ParsePlan(QueryPlan plan)
    {
        if (plan.PlanFormat != "TEXT")
            return null;

        return _planParser.Parse(plan);
    }

    private sealed record OpenTransaction(NpgsqlConnection Connection, NpgsqlTransaction Transaction) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Transaction.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
