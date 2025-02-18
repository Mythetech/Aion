using Aion.Components.Querying;

namespace Aion.Components.Connections;

public interface IConnectionService
{
    Task<List<string>> GetDatabasesAsync(string connectionString);
    Task<List<string>> GetTablesAsync(string connectionString, string database);
    Task<QueryResult> ExecuteQueryAsync(string connectionString, string query, CancellationToken cancellationToken);
}