using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Contracts.Database;
using Aion.Web.Providers;
using Microsoft.Extensions.Logging;
using SqliteWasmBlazor;

namespace Aion.Web.Services;

public class StorageRestoreService
{
    private readonly IndexedDbStorageService _storage;
    private readonly ConnectionState _connectionState;
    private readonly QueryState _queryState;
    private readonly SqliteWasmProvider _sqliteProvider;
    private readonly PGliteProvider _pgliteProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StorageRestoreService> _logger;

    private bool _restored;

    public StorageRestoreService(
        IndexedDbStorageService storage,
        ConnectionState connectionState,
        QueryState queryState,
        SqliteWasmProvider sqliteProvider,
        PGliteProvider pgliteProvider,
        IServiceProvider serviceProvider,
        ILogger<StorageRestoreService> logger)
    {
        _storage = storage;
        _connectionState = connectionState;
        _queryState = queryState;
        _sqliteProvider = sqliteProvider;
        _pgliteProvider = pgliteProvider;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public bool HasRestoredData => _connectionState.Connections.Count > 0;

    public async Task RestoreAsync()
    {
        if (_restored) return;
        _restored = true;

        try
        {
            await _serviceProvider.InitializeSqliteWasmAsync();
            var metas = await _storage.LoadDatabaseMetasAsync();

            foreach (var meta in metas)
            {
                try
                {
                    switch (meta.Type)
                    {
                        case DatabaseType.WasmSQLite:
                            await _sqliteProvider.EnsureDatabaseAsync(meta.Name);
                            break;
                        case DatabaseType.WasmPostgreSQL:
                            await _pgliteProvider.EnsureDatabaseAsync(meta.Name);
                            break;
                    }
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
