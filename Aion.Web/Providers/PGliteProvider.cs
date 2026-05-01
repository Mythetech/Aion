using System.Text;
using System.Text.Json;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Microsoft.JSInterop;

namespace Aion.Web.Providers;

public class PGliteProvider : IDatabaseProvider, IDatabaseIndexProvider
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private readonly HashSet<string> _databases = new();

    public IStandardDatabaseCommands Commands { get; } = new PGliteCommands();
    public DatabaseType DatabaseType => DatabaseType.WasmPostgreSQL;
    public IReadOnlyList<string> SystemSchemas { get; } = ["pg_catalog", "information_schema", "pg_toast"];

    public PGliteProvider(IJSRuntime js)
    {
        _js = js;
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/pglite-interop.js");
        return _module;
    }

    public async Task EnsureDatabaseAsync(string name)
    {
        if (_databases.Contains(name))
            return;

        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("create", name);
        _databases.Add(name);
    }

    public async Task DestroyDatabaseAsync(string name)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("destroy", name);
        _databases.Remove(name);
    }

    public Task<List<string>?> GetDatabasesAsync(string connectionString)
    {
        return Task.FromResult<List<string>?>(_databases.ToList());
    }

    public async Task<List<TableInfo>> GetTablesAsync(string connectionString, string database)
    {
        await EnsureDatabaseAsync(database);
        var module = await GetModuleAsync();

        var result = await module.InvokeAsync<JsonElement>("query", database,
            "SELECT schemaname, tablename FROM pg_tables WHERE schemaname NOT IN ('pg_catalog', 'information_schema') ORDER BY schemaname, tablename");

        var tables = new List<TableInfo>();
        foreach (var row in result.GetProperty("rows").EnumerateArray())
        {
            var schema = row.GetProperty("schemaname").GetString() ?? "";
            var name = row.GetProperty("tablename").GetString() ?? "";
            tables.Add(new TableInfo(schema, name));
        }
        return tables;
    }

    public async Task<List<ColumnInfo>> GetColumnsAsync(string connectionString, string database, string schema, string table)
    {
        var module = await GetModuleAsync();

        var sql = $@"
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
                    AND ku.table_name = '{table}'
                    AND ku.table_schema = '{schema}'
            ) pk ON c.column_name = pk.column_name
            WHERE c.table_name = '{table}'
            AND c.table_schema = '{schema}'
            ORDER BY c.ordinal_position";

        var result = await module.InvokeAsync<JsonElement>("query", database, sql);

        var columns = new List<ColumnInfo>();
        foreach (var row in result.GetProperty("rows").EnumerateArray())
        {
            columns.Add(new ColumnInfo
            {
                Name = row.GetProperty("column_name").GetString() ?? "",
                DataType = row.GetProperty("data_type").GetString() ?? "",
                IsNullable = row.GetProperty("is_nullable").GetBoolean(),
                DefaultValue = row.TryGetProperty("column_default", out var def) && def.ValueKind != JsonValueKind.Null
                    ? def.GetString() : null,
                MaxLength = row.TryGetProperty("character_maximum_length", out var ml) && ml.ValueKind != JsonValueKind.Null
                    ? ml.GetInt32() : null,
                IsPrimaryKey = row.GetProperty("is_primary_key").GetBoolean(),
                IsIdentity = row.GetProperty("is_identity").GetBoolean()
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
        var module = await GetModuleAsync();

        var sql = $@"
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
                AND tc.table_name = '{table}'
                AND tc.table_schema = '{schema}'";

        var result = await module.InvokeAsync<JsonElement>("query", database, sql);

        var foreignKeys = new List<ForeignKeyInfo>();
        foreach (var row in result.GetProperty("rows").EnumerateArray())
        {
            foreignKeys.Add(new ForeignKeyInfo
            {
                ConstraintName = row.GetProperty("constraint_name").GetString() ?? "",
                ColumnName = row.GetProperty("column_name").GetString() ?? "",
                ReferencedTable = row.GetProperty("referenced_table").GetString() ?? "",
                ReferencedColumn = row.GetProperty("referenced_column").GetString() ?? "",
                ReferencedSchema = row.TryGetProperty("referenced_schema", out var rs) && rs.ValueKind != JsonValueKind.Null
                    ? rs.GetString() : null
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

            var module = await GetModuleAsync();
            var jsResult = await module.InvokeAsync<JsonElement>("query", dbName, query);

            var columns = jsResult.GetProperty("columns");
            foreach (var col in columns.EnumerateArray())
            {
                result.Columns.Add(col.GetString() ?? "");
            }

            foreach (var row in jsResult.GetProperty("rows").EnumerateArray())
            {
                var dict = new Dictionary<string, object>();
                foreach (var colName in result.Columns)
                {
                    if (row.TryGetProperty(colName, out var val))
                    {
                        dict[colName] = val.ValueKind switch
                        {
                            JsonValueKind.Null => null!,
                            JsonValueKind.Number => val.TryGetInt64(out var l) ? l : val.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => val.GetString() ?? ""
                        };
                    }
                    else
                    {
                        dict[colName] = null!;
                    }
                }
                result.Rows.Add(dict);
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
        => $"pglite://{database}";

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
            var dbName = ExtractDatabaseName(connectionString);
            var module = await GetModuleAsync();
            var result = await module.InvokeAsync<JsonElement>("query", dbName, $"EXPLAIN {query}");

            var sb = new StringBuilder();
            foreach (var row in result.GetProperty("rows").EnumerateArray())
            {
                var planLine = row.GetProperty("QUERY PLAN").GetString();
                if (planLine != null) sb.AppendLine(planLine);
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
        var plan = new QueryPlan { PlanType = "Actual", PlanFormat = "TEXT" };

        try
        {
            var dbName = ExtractDatabaseName(connectionString);
            var module = await GetModuleAsync();
            var result = await module.InvokeAsync<JsonElement>("query", dbName, $"EXPLAIN ANALYZE {query}");

            var sb = new StringBuilder();
            foreach (var row in result.GetProperty("rows").EnumerateArray())
            {
                var planLine = row.GetProperty("QUERY PLAN").GetString();
                if (planLine != null) sb.AppendLine(planLine);
            }
            plan.PlanContent = sb.ToString();
        }
        catch (Exception ex)
        {
            plan.PlanContent = $"Error getting plan: {ex.Message}";
        }

        return plan;
    }

    public async Task<TransactionInfo> BeginTransactionAsync(string connectionString)
    {
        var transaction = new TransactionInfo();
        var dbName = ExtractDatabaseName(connectionString);
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("exec", dbName, "BEGIN");
        return transaction;
    }

    public async Task CommitTransactionAsync(string connectionString, string transactionId)
    {
        var dbName = ExtractDatabaseName(connectionString);
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("exec", dbName, "COMMIT");
    }

    public async Task RollbackTransactionAsync(string connectionString, string transactionId)
    {
        var dbName = ExtractDatabaseName(connectionString);
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("exec", dbName, "ROLLBACK");
    }

    public async Task<QueryResult> ExecuteInTransactionAsync(string connectionString, string query, string transactionId, CancellationToken cancellationToken)
    {
        return await ExecuteQueryAsync(connectionString, query, cancellationToken);
    }

    public async Task<List<IndexInfo>> GetIndexesAsync(string connectionString, string database)
    {
        var module = await GetModuleAsync();

        var sql = @"
            SELECT
                schemaname,
                tablename,
                indexname,
                indexdef LIKE '%UNIQUE%' as is_unique,
                indexname LIKE '%pkey' as is_primary
            FROM pg_indexes
            WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
            ORDER BY schemaname, tablename, indexname";

        var result = await module.InvokeAsync<JsonElement>("query", database, sql);
        var indexes = new List<IndexInfo>();

        foreach (var row in result.GetProperty("rows").EnumerateArray())
        {
            indexes.Add(new IndexInfo(
                Schema: row.GetProperty("schemaname").GetString() ?? "",
                TableSchema: row.GetProperty("schemaname").GetString() ?? "",
                TableName: row.GetProperty("tablename").GetString() ?? "",
                Name: row.GetProperty("indexname").GetString() ?? "",
                IsUnique: row.GetProperty("is_unique").GetBoolean(),
                IsPrimary: row.GetProperty("is_primary").GetBoolean(),
                Columns: []));
        }

        return indexes;
    }

    private static string? ExtractDatabaseName(string connectionString)
    {
        if (connectionString.StartsWith("pglite://"))
            return connectionString["pglite://".Length..];
        return connectionString;
    }
}
