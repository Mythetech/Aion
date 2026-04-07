using Aion.Core.Database.PostgreSQL;
using Aion.Core.Queries;
using Npgsql;
using System.Text;

namespace Aion.Core.Database;

public class PostgreSqlProvider : IDatabaseProvider
{
    private readonly Dictionary<string, NpgsqlTransaction> _activeTransactions = new();
    public IStandardDatabaseCommands Commands { get; } = new PostgreSqlCommands();
    public DatabaseType DatabaseType => DatabaseType.PostgreSQL;
    public IReadOnlyList<string> SystemSchemas { get; } = ["pg_catalog", "information_schema", "pg_toast"];

    public async Task<List<string>> GetDatabasesAsync(string connectionString)
    {
        var databases = new List<string>();

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        builder.Database = "postgres";

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

    public async Task<List<TableInfo>> GetTablesAsync(string connectionString, string database)
    {
        var tables = new List<TableInfo>();

        using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_type = 'BASE TABLE'
            AND table_schema NOT LIKE 'pg_temp_%'
            AND table_schema NOT LIKE 'pg_toast_temp_%'
            ORDER BY table_schema, table_name";

        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            tables.Add(new TableInfo(reader.GetString(0), reader.GetString(1)));
        }

        return tables;
    }

    public async Task<QueryResult> ExecuteQueryAsync(string connectionString, string query, CancellationToken cancellationToken)
    {
        var result = new QueryResult();

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
        var plan = new QueryPlan
        {
            PlanType = "Estimated",
            PlanFormat = "TEXT"
        };

        try
        {
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand($"EXPLAIN {query}", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var planText = new StringBuilder();
            while (await reader.ReadAsync())
            {
                planText.AppendLine(reader.GetString(0));
            }

            plan.PlanContent = planText.ToString();
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
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand($"EXPLAIN ANALYZE {query}", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var planText = new StringBuilder();
            while (await reader.ReadAsync())
            {
                planText.AppendLine(reader.GetString(0));
            }

            plan.PlanContent = planText.ToString();
            return plan;
        }
        catch (Exception ex)
        {
            plan.PlanContent = $"Error getting plan: {ex.Message}";
            return plan;
        }
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
                CASE WHEN c.column_default LIKE 'nextval%' THEN true ELSE false END as is_identity
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

    public async Task<TransactionInfo> BeginTransactionAsync(string connectionString)
    {
        var transaction = new TransactionInfo();

        var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        var dbTransaction = await conn.BeginTransactionAsync();

        _activeTransactions[transaction.Id] = dbTransaction;

        return transaction;
    }

    public async Task CommitTransactionAsync(string connectionString, string transactionId)
    {
        if (_activeTransactions.TryGetValue(transactionId, out var transaction))
        {
            await transaction.CommitAsync();
            await transaction.Connection!.DisposeAsync();
            _activeTransactions.Remove(transactionId);
        }
    }

    public async Task RollbackTransactionAsync(string connectionString, string transactionId)
    {
        if (_activeTransactions.TryGetValue(transactionId, out var transaction))
        {
            await transaction.RollbackAsync();
            await transaction.Connection!.DisposeAsync();
            _activeTransactions.Remove(transactionId);
        }
    }

    public async Task<QueryResult> ExecuteInTransactionAsync(string connectionString, string query, string transactionId, CancellationToken cancellationToken)
    {
        if (!_activeTransactions.TryGetValue(transactionId, out var transaction))
        {
            return new QueryResult { Error = "Transaction not found" };
        }

        var result = new QueryResult();
        try
        {
            using var cmd = new NpgsqlCommand(query, transaction.Connection, transaction);
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
}
