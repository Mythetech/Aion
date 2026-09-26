using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Components.Querying.Events;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Web.Services;

/// <summary>
/// Keeps connections and in-browser database metadata in IndexedDB. Query tabs save themselves through
/// <see cref="QueryState"/> and <see cref="IndexedDbQuerySaveService"/>.
/// </summary>
public class WebPersistenceManager : IConsumer<QueryExecuted>, IAsyncDisposable
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);

    private readonly IndexedDbStorageService _storage;
    private readonly ConnectionState _connectionState;
    private readonly ILogger<WebPersistenceManager> _logger;

    private readonly CancellationTokenSource _stopping = new();
    private Task? _sweepLoop;
    private bool _dirty;

    public WebPersistenceManager(
        IndexedDbStorageService storage,
        ConnectionState connectionState,
        ILogger<WebPersistenceManager> logger)
    {
        _storage = storage;
        _connectionState = connectionState;
        _logger = logger;
    }

    public void Start()
    {
        if (_sweepLoop is not null) return;

        _connectionState.ConnectionStateChanged += OnConnectionStateChanged;

        _sweepLoop = SweepPeriodicallyAsync(_stopping.Token);
    }

    public async Task Consume(QueryExecuted message)
    {
        if (message.Query.Result is not { Success: true }) return;

        if (IsMutatingQuery(message.ExecutedSql))
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

    private async Task SweepPeriodicallyAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await SweepAsync();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task PersistConnectionsAsync()
    {
        try
        {
            foreach (var connection in _connectionState.Connections.ToList())
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

        await PersistConnectionsAsync();
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

    public async ValueTask DisposeAsync()
    {
        _connectionState.ConnectionStateChanged -= OnConnectionStateChanged;

        await _stopping.CancelAsync();

        if (_sweepLoop is not null) await _sweepLoop;

        _stopping.Dispose();
        GC.SuppressFinalize(this);
    }
}
