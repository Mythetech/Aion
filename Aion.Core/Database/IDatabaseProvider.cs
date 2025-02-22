using Aion.Core.Queries;

namespace Aion.Core.Database;

public interface IDatabaseProvider
{
    IStandardDatabaseCommands Commands { get; }
    DatabaseType DatabaseType { get; }
    Task<List<string>> GetDatabasesAsync(string connectionString);
    Task<List<string>> GetTablesAsync(string connectionString, string database);
    Task<List<ColumnInfo>> GetColumnsAsync(string connectionString, string database, string table);
    Task<QueryResult> ExecuteQueryAsync(string connectionString, string query, CancellationToken cancellationToken);
    string UpdateConnectionString(string connectionString, string database);
    int GetDefaultPort();
    bool ValidateConnectionString(string connectionString, out string? error);
    Task<QueryPlan> GetEstimatedPlanAsync(string connectionString, string query);
    Task<QueryPlan> GetActualPlanAsync(string connectionString, string query);
} 