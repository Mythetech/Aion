using System.Text;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Microsoft.Data.Sqlite;

namespace Aion.Web.Providers;

public class SqliteWasmProvider : IDatabaseProvider, IDatabaseIndexProvider
{
    private readonly Dictionary<string, SqliteConnection> _sentinelConnections = new();
    private readonly Dictionary<string, SqliteTransaction> _activeTransactions = new();
    private readonly Dictionary<string, SqliteConnection> _transactionConnections = new();

    public IStandardDatabaseCommands Commands { get; } = new SqliteWasmCommands();
    public DatabaseType DatabaseType => DatabaseType.WasmSQLite;
    public IReadOnlyList<string> SystemSchemas { get; } = [];

    public Task<List<string>?> GetDatabasesAsync(string connectionString)
    {
        return Task.FromResult<List<string>?>(_sentinelConnections.Keys.ToList());
    }

    public Task EnsureDatabaseAsync(string name)
    {
        if (_sentinelConnections.ContainsKey(name))
            return Task.CompletedTask;

        var connStr = BuildConnectionString(name);
        var sentinel = new SqliteConnection(connStr);
        sentinel.Open();
        _sentinelConnections[name] = sentinel;
        return Task.CompletedTask;
    }

    public async Task<List<TableInfo>> GetTablesAsync(string connectionString, string database)
    {
        await EnsureDatabaseAsync(database);
        var tables = new List<TableInfo>();
        var connStr = BuildConnectionString(database);

        using var conn = new SqliteConnection(connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(new TableInfo("", reader.GetString(0)));
        }

        return tables;
    }

    public async Task<List<ColumnInfo>> GetColumnsAsync(string connectionString, string database, string schema, string table)
    {
        var columns = new List<ColumnInfo>();
        var connStr = BuildConnectionString(database);

        using var conn = new SqliteConnection(connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\")";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo
            {
                Name = reader.GetString(1),
                DataType = reader.GetString(2),
                IsNullable = reader.GetInt32(3) == 0,
                DefaultValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                IsPrimaryKey = reader.GetInt32(5) > 0,
                IsIdentity = false
            });
        }

        var foreignKeys = await GetForeignKeysAsync(connectionString, database, schema, table);
        foreach (var fk in foreignKeys)
        {
            var column = columns.FirstOrDefault(c => c.Name == fk.ColumnName);
            if (column != null)
                column.ForeignKey = fk;
        }

        return columns;
    }

    public async Task<List<ForeignKeyInfo>> GetForeignKeysAsync(string connectionString, string database, string schema, string table)
    {
        var foreignKeys = new List<ForeignKeyInfo>();
        var connStr = BuildConnectionString(database);

        using var conn = new SqliteConnection(connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA foreign_key_list(\"{table}\")";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            foreignKeys.Add(new ForeignKeyInfo
            {
                ConstraintName = $"fk_{table}_{reader.GetString(3)}",
                ColumnName = reader.GetString(3),
                ReferencedTable = reader.GetString(2),
                ReferencedColumn = reader.GetString(4)
            });
        }

        return foreignKeys;
    }

    public async Task<QueryResult> ExecuteQueryAsync(string connectionString, string query, CancellationToken cancellationToken)
    {
        var result = new QueryResult();

        try
        {
            var dbName = ExtractDatabaseName(connectionString);
            if (!string.IsNullOrEmpty(dbName))
                await EnsureDatabaseAsync(dbName);

            using var conn = new SqliteConnection(connectionString);
            await conn.OpenAsync(cancellationToken);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = query;

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            for (int i = 0; i < reader.FieldCount; i++)
                result.Columns.Add(reader.GetName(i));

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    row[result.Columns[i]] = value == DBNull.Value ? null! : value;
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
        => BuildConnectionString(database);

    public int GetDefaultPort() => 0;

    public bool ValidateConnectionString(string connectionString, out string? error)
    {
        error = null;
        return true;
    }

    public async Task<QueryPlan> GetEstimatedPlanAsync(string connectionString, string query)
    {
        var plan = new QueryPlan { PlanType = "Estimated", PlanFormat = "TEXT" };

        try
        {
            using var conn = new SqliteConnection(connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"EXPLAIN QUERY PLAN {query}";

            using var reader = await cmd.ExecuteReaderAsync();
            var sb = new StringBuilder();
            while (await reader.ReadAsync())
            {
                sb.AppendLine(reader.GetString(3));
            }
            plan.PlanContent = sb.ToString();
        }
        catch (Exception ex)
        {
            plan.PlanContent = $"Error getting plan: {ex.Message}";
        }

        return plan;
    }

    public async Task<QueryPlan> GetActualPlanAsync(string connectionString, string query)
    {
        return await GetEstimatedPlanAsync(connectionString, query);
    }

    public async Task<TransactionInfo> BeginTransactionAsync(string connectionString)
    {
        var transaction = new TransactionInfo();

        var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync();
        var dbTransaction = conn.BeginTransaction();

        _transactionConnections[transaction.Id] = conn;
        _activeTransactions[transaction.Id] = dbTransaction;

        return transaction;
    }

    public async Task CommitTransactionAsync(string connectionString, string transactionId)
    {
        if (_activeTransactions.TryGetValue(transactionId, out var transaction))
        {
            await transaction.CommitAsync();
            transaction.Dispose();
            _activeTransactions.Remove(transactionId);
        }

        if (_transactionConnections.TryGetValue(transactionId, out var conn))
        {
            await conn.DisposeAsync();
            _transactionConnections.Remove(transactionId);
        }
    }

    public async Task RollbackTransactionAsync(string connectionString, string transactionId)
    {
        if (_activeTransactions.TryGetValue(transactionId, out var transaction))
        {
            await transaction.RollbackAsync();
            transaction.Dispose();
            _activeTransactions.Remove(transactionId);
        }

        if (_transactionConnections.TryGetValue(transactionId, out var conn))
        {
            await conn.DisposeAsync();
            _transactionConnections.Remove(transactionId);
        }
    }

    public async Task<QueryResult> ExecuteInTransactionAsync(string connectionString, string query, string transactionId, CancellationToken cancellationToken)
    {
        if (!_activeTransactions.TryGetValue(transactionId, out var transaction))
            return new QueryResult { Error = "Transaction not found" };

        var result = new QueryResult();
        try
        {
            var conn = _transactionConnections[transactionId];
            using var cmd = conn.CreateCommand();
            cmd.CommandText = query;
            cmd.Transaction = transaction;

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            for (int i = 0; i < reader.FieldCount; i++)
                result.Columns.Add(reader.GetName(i));

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    row[result.Columns[i]] = value == DBNull.Value ? null! : value;
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

    public async Task<List<IndexInfo>> GetIndexesAsync(string connectionString, string database)
    {
        var indexes = new List<IndexInfo>();
        var connStr = BuildConnectionString(database);

        using var conn = new SqliteConnection(connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT il.name, il.""unique"", il.origin = 'pk', ii.name as col_name, m.name as table_name
            FROM sqlite_master m
            JOIN pragma_index_list(m.name) il
            JOIN pragma_index_info(il.name) ii
            WHERE m.type = 'table' AND m.name NOT LIKE 'sqlite_%'
            ORDER BY m.name, il.name, ii.seqno";

        var indexMap = new Dictionary<string, (string Table, bool IsUnique, bool IsPrimary, List<string> Columns)>();

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var indexName = reader.GetString(0);
            var isUnique = reader.GetInt64(1) == 1;
            var isPrimary = reader.GetInt64(2) == 1;
            var colName = reader.GetString(3);
            var tableName = reader.GetString(4);

            if (!indexMap.TryGetValue(indexName, out var entry))
            {
                entry = (tableName, isUnique, isPrimary, new List<string>());
                indexMap[indexName] = entry;
            }
            entry.Columns.Add(colName);
        }

        foreach (var (name, (table, isUnique, isPrimary, columns)) in indexMap)
        {
            indexes.Add(new IndexInfo(
                Schema: "",
                TableSchema: "",
                TableName: table,
                Name: name,
                IsUnique: isUnique,
                IsPrimary: isPrimary,
                Columns: columns));
        }

        return indexes;
    }

    private static string BuildConnectionString(string database)
        => $"Data Source={database};Mode=Memory;Cache=Shared";

    private static string? ExtractDatabaseName(string connectionString)
    {
        try
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);
            return builder.DataSource;
        }
        catch
        {
            return null;
        }
    }
}
