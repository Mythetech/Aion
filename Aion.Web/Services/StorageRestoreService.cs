using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Contracts.Database;
using Microsoft.Extensions.Logging;

namespace Aion.Web.Services;

public class StorageRestoreService
{
    private readonly IndexedDbStorageService _storage;
    private readonly ConnectionState _connectionState;
    private readonly QueryState _queryState;
    private readonly IDatabaseProviderFactory _providerFactory;
    private readonly ISqliteWasmInitializer _sqliteInitializer;
    private readonly ILogger<StorageRestoreService> _logger;

    private Task? _restore;

    public StorageRestoreService(
        IndexedDbStorageService storage,
        ConnectionState connectionState,
        QueryState queryState,
        IDatabaseProviderFactory providerFactory,
        ISqliteWasmInitializer sqliteInitializer,
        ILogger<StorageRestoreService> logger)
    {
        _storage = storage;
        _connectionState = connectionState;
        _queryState = queryState;
        _providerFactory = providerFactory;
        _sqliteInitializer = sqliteInitializer;
        _logger = logger;
    }

    public bool HasRestoredData => _connectionState.Connections.Count > 0;

    // App and WebLayout both wait on the restore, so every caller shares one run instead of reloading
    // connections a second time while the first load is still in flight.
    public Task RestoreAsync() => _restore ??= RestoreCoreAsync();

    private async Task RestoreCoreAsync()
    {
        try
        {
            await _sqliteInitializer.InitializeAsync();
            var metas = await _storage.LoadDatabaseMetasAsync();

            foreach (var meta in metas)
            {
                try
                {
                    if (_providerFactory.GetProvider(meta.Type) is IManagedDatabaseProvider managed)
                        await managed.EnsureDatabaseAsync(meta.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to restore database {Name}", meta.Name);
                }
            }

            await _connectionState.InitializeAsync();
            await _queryState.InitializeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore persisted data");
        }
    }
}
