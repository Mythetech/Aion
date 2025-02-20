using Aion.Components.Connections;
using Aion.Core.Database;
using Aion.Core.Queries;

namespace Aion.Desktop;

public class ConnectionService : IConnectionService
{
    private readonly IDatabaseProviderFactory _providerFactory;

    public ConnectionService(IDatabaseProviderFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    public async Task<List<string>> GetDatabasesAsync(string connectionString, DatabaseType type)
    {
        var provider = _providerFactory.GetProvider(type);
        return await provider.GetDatabasesAsync(connectionString);
    }

    public async Task<List<string>> GetTablesAsync(string connectionString, string database, DatabaseType type)
    {
        var provider = _providerFactory.GetProvider(type);
        return await provider.GetTablesAsync(connectionString, database);
    }

    public async Task<QueryResult> ExecuteQueryAsync(string connectionString, string query, DatabaseType type, CancellationToken cancellationToken)
    {
        var provider = _providerFactory.GetProvider(type);
        return await provider.ExecuteQueryAsync(connectionString, query, cancellationToken);
    }
}