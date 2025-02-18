using Aion.Components.Querying;

namespace Aion.Components.Connections;

public interface IConnectionService
{
    Task<List<string>> ConnectAsync(string connectionString);
    Task<QueryResult> ExecuteQueryAsync(string connectionString, string query);
}