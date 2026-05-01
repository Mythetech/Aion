using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Components.Querying.Events;
using Aion.Contracts.Database;
using Aion.Web.Providers;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Web.Services;

public class WebPersistenceManager : IConsumer<QueryExecuted>, IDisposable
{
    private readonly IndexedDbStorageService _storage;
    private readonly ConnectionState _connectionState;
    private readonly QueryState _queryState;
    private readonly SqliteWasmProvider _sqliteProvider;
    private readonly ILogger<WebPersistenceManager> _logger;
    private Timer? _sweepTimer;
    private Timer? _queryDebounceTimer;
    private bool _dirty;

    public WebPersistenceManager(
        IndexedDbStorageService storage,
        ConnectionState connectionState,
        QueryState queryState,
        SqliteWasmProvider sqliteProvider,
        ILogger<WebPersistenceManager> logger)
    {
        _storage = storage;
        _connectionState = connectionState;
        _queryState = queryState;
        _sqliteProvider = sqliteProvider;
        _logger = logger;
    }

    public void Start()
    {
        _connectionState.ConnectionStateChanged += OnConnectionStateChanged;
        _queryState.StateChanged += OnQueryStateChanged;

        _sweepTimer = new Timer(_ => _ = SweepAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public async Task Consume(QueryExecuted message)
    {
        if (message.Query.Result is not { Success: true }) return;

        var queryText = message.Query.Query;
        if (IsMutatingQuery(queryText))
        {
            await PersistDatabaseAsync(message.Query);
        }
    }

    private async Task PersistDatabaseAsync(QueryModel query)
    {
        if (query.ConnectionId == null || query.DatabaseName == null) return;

        var connection = _connectionState.Connections.FirstOrDefault(c => c.Id == query.ConnectionId);
        if (connection == null) return;

        try
        {
            await _storage.SaveDatabaseMetaAsync(query.DatabaseName, connection.Type);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist database {Name} after mutation", query.DatabaseName);
        }
    }

    private void OnConnectionStateChanged()
    {
        _dirty = true;
    }

    private void OnQueryStateChanged()
    {
        _queryDebounceTimer?.Dispose();
        _queryDebounceTimer = new Timer(_ => _ = PersistQueriesAsync(), null, TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
    }

    private async Task PersistQueriesAsync()
    {
        try
        {
            foreach (var query in _queryState.Queries)
            {
                var record = new QueryRecord(query);
                await _storage.SaveQueryAsync(record);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist queries");
        }
    }

    private async Task PersistConnectionsAsync()
    {
        try
        {
            foreach (var connection in _connectionState.Connections)
            {
                await _storage.SaveConnectionAsync(connection);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist connections");
        }
    }

    private async Task SweepAsync()
    {
        if (!_dirty) return;
        _dirty = false;

        try
        {
            await PersistConnectionsAsync();
            await PersistQueriesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Periodic sweep failed");
        }
    }

    private static bool IsMutatingQuery(string query)
    {
        var trimmed = query.TrimStart();
        return trimmed.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("CREATE", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("DROP", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("ALTER", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _connectionState.ConnectionStateChanged -= OnConnectionStateChanged;
        _queryState.StateChanged -= OnQueryStateChanged;
        _sweepTimer?.Dispose();
        _queryDebounceTimer?.Dispose();
    }
}
