using Aion.Components.Connections;
using Aion.Components.History;
using Aion.Components.Querying;
using Aion.Contracts.Database;
using Aion.Web.Services;

namespace Aion.Web.Databases;

public class BrowserStorageCleaner
{
    private readonly ConnectionState _connectionState;
    private readonly QueryState _queryState;
    private readonly HistoryState _historyState;
    private readonly IndexedDbStorageService _storage;
    private readonly IDatabaseProviderFactory _providerFactory;

    public BrowserStorageCleaner(
        ConnectionState connectionState,
        QueryState queryState,
        HistoryState historyState,
        IndexedDbStorageService storage,
        IDatabaseProviderFactory providerFactory)
    {
        _connectionState = connectionState;
        _queryState = queryState;
        _historyState = historyState;
        _storage = storage;
        _providerFactory = providerFactory;
    }

    public async Task ClearAllAsync()
    {
        foreach (var connection in _connectionState.Connections.ToList())
        {
            await _connectionState.RemoveConnection(connection.Id);
        }

        // Earlier versions could remove a connection and leave its database behind; the stored metadata is
        // the only record of those.
        foreach (var meta in await _storage.LoadDatabaseMetasAsync())
        {
            if (_providerFactory.SupportedDatabases.Contains(meta.Type)
                && _providerFactory.GetProvider(meta.Type) is IManagedDatabaseProvider managed)
            {
                await managed.DeleteDatabaseAsync(meta.Name);
            }
        }

        await _storage.ClearAllAsync();
        await _queryState.CloseAllTabs();
        await _historyState.ClearAsync();
    }
}
