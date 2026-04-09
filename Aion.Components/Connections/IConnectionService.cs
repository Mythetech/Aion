using Aion.Core.Connections;
using Aion.Core.Database;
using Aion.Core.Queries;

namespace Aion.Components.Connections;

public interface IConnectionService
{
    Task InitializeAsync();
    Task<IEnumerable<ConnectionModel>> GetSavedConnections();
    Task AddConnection(ConnectionModel connection);
    Task RemoveConnection(Guid id);
    Task UpdateConnection(ConnectionModel connection);
    Task<List<string>?> GetDatabasesAsync(string connectionString, DatabaseType type);
    Task<List<TableInfo>> GetTablesAsync(string connectionString, string database, DatabaseType type);
    Task<QueryResult> ExecuteQueryAsync(string connectionString, string query, DatabaseType type, CancellationToken cancellationToken);
}