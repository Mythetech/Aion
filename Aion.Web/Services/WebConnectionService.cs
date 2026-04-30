using Aion.Components.Connections;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;

namespace Aion.Web.Services;

public class WebConnectionService : IConnectionService
{
    private readonly IDatabaseProviderFactory _providerFactory;
    private readonly List<ConnectionModel> _connections = new();

    public WebConnectionService(IDatabaseProviderFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task<IEnumerable<ConnectionModel>> GetSavedConnections()
        => Task.FromResult(_connections.AsEnumerable());

    public Task AddConnection(ConnectionModel connection)
    {
        _connections.Add(connection);
        return Task.CompletedTask;
    }

    public Task RemoveConnection(Guid id)
    {
        _connections.RemoveAll(c => c.Id == id);
        return Task.CompletedTask;
    }

    public Task UpdateConnection(ConnectionModel connection)
    {
        var index = _connections.FindIndex(c => c.Id == connection.Id);
        if (index >= 0)
            _connections[index] = connection;
        return Task.CompletedTask;
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
