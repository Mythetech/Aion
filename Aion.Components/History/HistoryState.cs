using Microsoft.Extensions.Logging;

namespace Aion.Components.History;

public class HistoryState
{
    public const int MaxEntries = 500;

    private readonly IQueryHistoryStore _store;
    private readonly ILogger<HistoryState> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IReadOnlyList<QueryHistoryEntry> _entries = [];
    private bool _loaded;

    public HistoryState(IQueryHistoryStore store, ILogger<HistoryState> logger)
    {
        _store = store;
        _logger = logger;
    }

    public event Action? HistoryChanged;

    /// <summary>
    /// Entries ordered newest first. The list is replaced rather than mutated, so a reference taken
    /// during a render stays consistent while new runs are recorded.
    /// </summary>
    public IReadOnlyList<QueryHistoryEntry> Entries => _entries;

    public async Task InitializeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedAsync();
        }
        finally
        {
            _gate.Release();
        }

        NotifyHistoryChanged();
    }

    public async Task AddAsync(QueryHistoryEntry entry)
    {
        await _gate.WaitAsync();
        try
        {
            _entries = Cap([entry, .._entries]);

            // Saving before the persisted history has been read would overwrite it with only this session's runs.
            if (await EnsureLoadedAsync())
            {
                await PersistAsync();
            }
        }
        finally
        {
            _gate.Release();
        }

        NotifyHistoryChanged();
    }

    public async Task ClearAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _entries = [];
            _loaded = true;
            await PersistAsync();
        }
        finally
        {
            _gate.Release();
        }

        NotifyHistoryChanged();
    }

    /// <summary>
    /// The entries whose SQL contains <paramref name="term"/>, limited to one connection when <paramref name="connectionId"/> is given.
    /// </summary>
    public IReadOnlyList<QueryHistoryEntry> Search(string? term, Guid? connectionId = null)
    {
        IEnumerable<QueryHistoryEntry> entries = _entries;
        if (connectionId is { } id)
            entries = entries.Where(e => e.ConnectionId == id);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var trimmed = term.Trim();
            entries = entries.Where(e => e.Sql.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
        }

        return entries as IReadOnlyList<QueryHistoryEntry> ?? entries.ToList();
    }

    private async Task<bool> EnsureLoadedAsync()
    {
        if (_loaded) return true;

        try
        {
            var persisted = await _store.LoadAsync();
            _entries = Cap(_entries.Concat(persisted).DistinctBy(e => e.Id));
            _loaded = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load query history; it will be retried on the next run");
        }

        return _loaded;
    }

    private async Task PersistAsync()
    {
        try
        {
            await _store.SaveAsync(_entries);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save query history");
        }
    }

    private static IReadOnlyList<QueryHistoryEntry> Cap(IEnumerable<QueryHistoryEntry> entries) =>
        entries.OrderByDescending(e => e.ExecutedAt).Take(MaxEntries).ToArray();

    private void NotifyHistoryChanged() => HistoryChanged?.Invoke();
}
