using Aion.Components.Connections;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;

namespace Aion.Web.Services;

public class WebConnectionService : IConnectionService
{
    private readonly IDatabaseProviderFactory _providerFactory;
    private readonly IndexedDbStorageService _storage;
    private readonly List<ConnectionModel> _connections = new();

    public WebConnectionService(IDatabaseProviderFactory providerFactory, IndexedDbStorageService storage)
    {
        _providerFactory = providerFactory;
        _storage = storage;
    }

    public async Task InitializeAsync()
    {
        _connections.Clear();
        var records = await _storage.LoadConnectionsAsync();
        _connections.AddRange(records.Select(r => r.ToConnectionModel()));
    }

    public Task<IEnumerable<ConnectionModel>> GetSavedConnections()
        => Task.FromResult(_connections.AsEnumerable());

    public async Task AddConnection(ConnectionModel connection)
    {
        _connections.Add(connection);
        await _storage.SaveConnectionAsync(connection);
    }

    public async Task RemoveConnection(Guid id)
    {
        _connections.RemoveAll(c => c.Id == id);
        await _storage.DeleteConnectionAsync(id);
    }

    public async Task UpdateConnection(ConnectionModel connection)
    {
        var index = _connections.FindIndex(c => c.Id == connection.Id);
        if (index >= 0)
            _connections[index] = connection;
        await _storage.SaveConnectionAsync(connection);
    }

    public async Task<List<string>?> GetDatabasesAsync(string connectionString, DatabaseType type)
    {
        var provider = _providerFactory.GetProvider(type);
        return await provider.GetDatabasesAsync(connectionString);
    }

    public async Task<List<TableInfo>> GetTablesAsync(string connectionString, string database, DatabaseType type)
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
