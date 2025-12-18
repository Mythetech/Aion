using Aion.Components.Connections;
using Aion.Core.Connections;
using Aion.Core.Database;
using Aion.Core.Queries;

namespace Aion.Test.TestDoubles;

public class ConnectionServiceFake : IConnectionService
{
    private readonly IDatabaseProviderFactory _providerFactory;
    private List<ConnectionModel> _connections = new();

    public ConnectionServiceFake(IDatabaseProviderFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task AddConnection(ConnectionModel connection)
    {
        _connections.Add(connection);
        return Task.CompletedTask;
    }

    public async Task<List<string>?> GetDatabasesAsync(string connectionString, DatabaseType type)
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

    public Task<IEnumerable<ConnectionModel>> GetSavedConnections()
    {
        return Task.FromResult(_connections.AsEnumerable());
    }
}