using Aion.Contracts.Queries;

namespace Aion.Contracts.Database;

public interface IDatabaseProvider
{
    IStandardDatabaseCommands Commands { get; }
    DatabaseType DatabaseType { get; }
    IReadOnlyList<string> SystemSchemas { get; }
    Task<List<string>?> GetDatabasesAsync(string connectionString);
    Task<List<TableInfo>> GetTablesAsync(string connectionString, string database);
    Task<List<ColumnInfo>> GetColumnsAsync(string connectionString, string database, string schema, string table);
    Task<List<ForeignKeyInfo>> GetForeignKeysAsync(string connectionString, string database, string schema, string table);
    Task<QueryResult> ExecuteQueryAsync(string connectionString, string query, CancellationToken cancellationToken);
    string UpdateConnectionString(string connectionString, string database);
    int GetDefaultPort();
    bool ValidateConnectionString(string connectionString, out string? error);
    Task<QueryPlan> GetEstimatedPlanAsync(string connectionString, string query);
    Task<QueryPlan> GetActualPlanAsync(string connectionString, string query);
    Task<TransactionInfo> BeginTransactionAsync(string connectionString);
    Task CommitTransactionAsync(string connectionString, string transactionId);
    Task RollbackTransactionAsync(string connectionString, string transactionId);
    Task<QueryResult> ExecuteInTransactionAsync(string connectionString, string query, string transactionId, CancellationToken cancellationToken);
}
