using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Aion.Core.Database.MySql;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System.Collections.Concurrent;
using System.Text;

namespace Aion.Core.Database;

public class MySqlProvider : IDatabaseProvider, IDatabaseIndexProvider, IDatabaseRoutineProvider, IQueryPlanParsingProvider,
    IDatabaseRowEditingProvider, IEstimatedQueryPlanProvider, IActualQueryPlanProvider
{
    private const string TransactionNotOpenMessage = "This transaction is no longer open. Roll back to clear it.";
    private const int DeadlockErrorNumber = 1213;

    private readonly ILogger<MySqlProvider> _logger;
    private readonly ConcurrentDictionary<string, OpenTransaction> _activeTransactions = new();

    public MySqlProvider(ILogger<MySqlProvider> logger)
    {
        _logger = logger;
    }

    public IStandardDatabaseCommands Commands { get; } = new MySqlCommands();
    public DatabaseType DatabaseType => DatabaseType.MySQL;
    public IReadOnlyList<string> SystemSchemas { get; } = [];

    public async Task<List<string>?> GetDatabasesAsync(string connectionString)
    {
        var databases = new List<string>();

        var builder = new MySqlConnectionStringBuilder(connectionString);
        builder.Database = null;

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

    public async Task<List<TableInfo>> GetTablesAsync(string connectionString, string database)
    {
        var tables = new List<TableInfo>();

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
            tables.Add(new TableInfo("", reader.GetString(0)));
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

            // RecordsAffected is only final once every result set has been consumed, and is -1 when no statement changed rows.
            await reader.CloseAsync();
            result.RowsAffected = reader.RecordsAffected >= 0 ? reader.RecordsAffected : null;

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.SetError(MySqlErrors.ToQueryError(ex, query));
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

        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new MySqlCommand($"EXPLAIN FORMAT=JSON {QueryPlanStatementGuard.TrimTrailingTerminators(query)}", conn);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);

        return new QueryPlan
        {
            PlanType = "Estimated",
            PlanFormat = "JSON",
            PlanContent = result?.ToString() ?? string.Empty
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
        var refusal = MySqlStatementGuard.GetActualPlanRefusal(query);
        if (refusal != null) throw new InvalidOperationException(refusal);

        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        // Disposing an uncommitted MySqlTransaction rolls it back, which covers the failure paths.
        await using var transaction = await conn.BeginTransactionAsync(cancellationToken);
        await using var cmd = new MySqlCommand(
            $"EXPLAIN ANALYZE {QueryPlanStatementGuard.TrimTrailingTerminators(query)}", conn, transaction);

        var planText = new StringBuilder();
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                planText.AppendLine(reader.GetString(0));
            }
        }

        await transaction.RollbackAsync(CancellationToken.None);

        return new QueryPlan
        {
            PlanType = "Actual",
            PlanFormat = "TEXT",
            PlanContent = planText.ToString()
        };
    }

    public async Task<TransactionInfo> BeginTransactionAsync(string connectionString)
    {
        var conn = new MySqlConnection(connectionString);
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

        var refusal = MySqlStatementGuard.GetTransactionRefusal(query);
        if (refusal != null)
        {
            return new QueryResult { Error = refusal };
        }

        var result = new QueryResult();

        try
        {
            await using var cmd = new MySqlCommand(query, open.Connection, open.Transaction);
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
        catch (MySqlException ex) when (ex.Number == DeadlockErrorNumber)
        {
            // MySQL rolls back the whole transaction on a deadlock and later statements would autocommit,
            // so the connection is released rather than left looking like an open transaction.
            if (_activeTransactions.TryRemove(transactionId, out var aborted))
            {
                await aborted.DisposeAsync();
            }

            result.SetError(MySqlErrors.ToQueryError(ex, query,
                $"{ex.Message} MySQL rolled back the whole transaction, so none of its changes were kept."));
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.SetError(MySqlErrors.ToQueryError(ex, query));
            return result;
        }
    }

    public async Task<List<ColumnInfo>> GetColumnsAsync(string connectionString, string database, string schema, string table)
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

    public async Task<List<IndexInfo>> GetIndexesAsync(string connectionString, string database)
    {
        var rows = new List<(string TableName, string IndexName, bool NonUnique, string ColumnName, int SeqInIndex)>();

        using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT table_name, index_name, non_unique, column_name, seq_in_index
            FROM information_schema.statistics
            WHERE table_schema = @database
            ORDER BY table_name, index_name, seq_in_index";

        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@database", database);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2) != 0,
                reader.GetString(3),
                reader.GetInt32(4)));
        }

        return rows
            .GroupBy(r => new { r.TableName, r.IndexName })
            .Select(g =>
            {
                var ordered = g.OrderBy(r => r.SeqInIndex).ToList();
                var isUnique = !ordered[0].NonUnique;
                var isPrimary = string.Equals(g.Key.IndexName, "PRIMARY", StringComparison.OrdinalIgnoreCase);
                return new IndexInfo(
                    Schema: database,
                    TableSchema: database,
                    TableName: g.Key.TableName,
                    Name: g.Key.IndexName,
                    IsUnique: isUnique,
                    IsPrimary: isPrimary,
                    Columns: ordered.Select(r => r.ColumnName).ToList());
            })
            .OrderBy(i => i.TableName)
            .ThenBy(i => i.Name)
            .ToList();
    }

    public async Task<List<RoutineInfo>> GetRoutinesAsync(string connectionString, string database)
    {
        using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();

        // Pull routine metadata + parameters in a single query, then group client-side so we
        // only open one reader against the connection (MySQL client doesn't support MARS).
        const string sql = @"
            SELECT
                r.routine_schema,
                r.routine_name,
                r.routine_type,
                r.data_type,
                r.external_language,
                p.ordinal_position,
                p.parameter_mode,
                p.parameter_name,
                p.dtd_identifier
            FROM information_schema.routines r
            LEFT JOIN information_schema.parameters p
                ON p.specific_schema = r.routine_schema
                AND p.specific_name   = r.routine_name
                AND p.parameter_mode IS NOT NULL
            WHERE r.routine_schema = @database
            ORDER BY r.routine_name, p.ordinal_position";

        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@database", database);

        var rows = new List<(string Schema, string Name, string Type, string? DataType, string? Language,
            int? Ord, string? ParamName, string? ParamType)>();

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                rows.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? (int?)null : Convert.ToInt32(reader.GetValue(5)),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8)));
            }
        }

        return rows
            .GroupBy(r => new { r.Schema, r.Name, r.Type, r.DataType, r.Language })
            .Select(g =>
            {
                var kind = string.Equals(g.Key.Type, "PROCEDURE", StringComparison.OrdinalIgnoreCase)
                    ? RoutineKind.Procedure
                    : RoutineKind.Function;

                var parameters = g
                    .Where(r => r.Ord.HasValue)
                    .OrderBy(r => r.Ord!.Value)
                    .Select(r => string.IsNullOrEmpty(r.ParamName)
                        ? r.ParamType ?? ""
                        : $"{r.ParamName} {r.ParamType}")
                    .ToList();

                return new RoutineInfo(
                    Schema: g.Key.Schema,
                    Name: g.Key.Name,
                    Kind: kind,
                    ReturnType: kind == RoutineKind.Procedure ? null : g.Key.DataType,
                    ArgumentSignature: $"({string.Join(", ", parameters)})",
                    Language: g.Key.Language ?? "SQL");
            })
            .OrderBy(r => r.Name)
            .ToList();
    }

    public async Task<List<ForeignKeyInfo>> GetForeignKeysAsync(string connectionString, string database, string schema, string table)
    {
        var foreignKeys = new List<ForeignKeyInfo>();

        using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT
                kcu.CONSTRAINT_NAME,
                kcu.COLUMN_NAME,
                kcu.REFERENCED_TABLE_NAME,
                kcu.REFERENCED_COLUMN_NAME,
                kcu.REFERENCED_TABLE_SCHEMA
            FROM information_schema.KEY_COLUMN_USAGE kcu
            WHERE kcu.TABLE_SCHEMA = @database
                AND kcu.TABLE_NAME = @table
                AND kcu.REFERENCED_TABLE_NAME IS NOT NULL";

        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@database", database);
        cmd.Parameters.AddWithValue("@table", table);
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

    private sealed record OpenTransaction(MySqlConnection Connection, MySqlTransaction Transaction) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Transaction.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
