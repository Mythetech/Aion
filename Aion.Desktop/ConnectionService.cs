using Aion.Components.Connections;
using Aion.Core.Connections;
using Aion.Core.Database;
using Aion.Core.Queries;

namespace Aion.Desktop;

public class ConnectionService : IConnectionService
{
    private readonly IDatabaseProviderFactory _providerFactory;
    private readonly IConnectionStorage _storage;
    private List<ConnectionModel> _connections = new();

    public ConnectionService(IDatabaseProviderFactory providerFactory, IConnectionStorage storage)
    {
        _providerFactory = providerFactory;
        _storage = storage;
    }

    public async Task InitializeAsync()
    {
        var savedConnections = await _storage.LoadConnectionsAsync();
        foreach (var connection in savedConnections)
        {
            if (!_connections.Any(c => c.ConnectionString.Equals(connection.ConnectionString)))
            {
                _connections.Add(connection);
            }
        }
    }

    public async Task AddConnection(ConnectionModel connection)
    {
        _connections.Add(connection);
        
        if (connection.SaveCredentials)
            await _storage.SaveConnectionsAsync(_connections);
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

    public Task<IEnumerable<ConnectionModel>> GetSavedConnections()
    {
        return Task.FromResult(_connections.AsEnumerable());
    }
}