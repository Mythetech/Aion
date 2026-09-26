using System.Threading.Channels;
using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Components.Querying.Events;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Web.Services;

public class WebPersistenceManager : IConsumer<QueryExecuted>, IAsyncDisposable
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan QueryDebounce = TimeSpan.FromSeconds(2);

    private readonly IndexedDbStorageService _storage;
    private readonly ConnectionState _connectionState;
    private readonly QueryState _queryState;
    private readonly ILogger<WebPersistenceManager> _logger;

    // State change events are synchronous, so they only signal this channel; the persistence loop awaits the
    // writes. One pending signal is enough because each write saves every open query.
    private readonly Channel<bool> _queryChanges = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    private readonly CancellationTokenSource _stopping = new();
    private Task? _sweepLoop;
    private Task? _queryLoop;
    private bool _dirty;

    public WebPersistenceManager(
        IndexedDbStorageService storage,
        ConnectionState connectionState,
        QueryState queryState,
        ILogger<WebPersistenceManager> logger)
    {
        _storage = storage;
        _connectionState = connectionState;
        _queryState = queryState;
        _logger = logger;
    }

    public void Start()
    {
        if (_sweepLoop is not null) return;

        _connectionState.ConnectionStateChanged += OnConnectionStateChanged;
        _queryState.StateChanged += OnQueryStateChanged;

        _sweepLoop = SweepPeriodicallyAsync(_stopping.Token);
        _queryLoop = PersistQueriesWhenChangedAsync(_stopping.Token);
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

    private void OnQueryStateChanged()
    {
        _queryChanges.Writer.TryWrite(true);
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

    private async Task PersistQueriesWhenChangedAsync(CancellationToken cancellationToken)
    {
        var changes = _queryChanges.Reader;
        try
        {
            while (await changes.WaitToReadAsync(cancellationToken))
            {
                // Keep waiting while changes keep arriving so a burst of edits is written once.
                while (changes.TryRead(out _))
                {
                    await Task.Delay(QueryDebounce, cancellationToken);
                }

                await PersistQueriesAsync();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task PersistQueriesAsync()
    {
        try
        {
            foreach (var query in _queryState.Queries.ToList())
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
        await PersistQueriesAsync();
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
        _queryState.StateChanged -= OnQueryStateChanged;

        await _stopping.CancelAsync();
        _queryChanges.Writer.TryComplete();

        if (_sweepLoop is not null) await _sweepLoop;
        if (_queryLoop is not null) await _queryLoop;

        _stopping.Dispose();
        GC.SuppressFinalize(this);
    }
}
