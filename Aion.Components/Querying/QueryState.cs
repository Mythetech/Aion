using System.Threading.Channels;
using Aion.Components.Connections;
using Mythetech.Framework.Infrastructure.MessageBus;
using Aion.Components.Querying.Commands;
using Aion.Contracts.Connections;
using Aion.Contracts.Queries;
using Aion.Components.Querying.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.Components.Querying;

/// <summary>
/// The open query tabs. Tabs are a workspace that outlives the app: every change is written through
/// <see cref="IQuerySaveService"/> once edits pause, and a tab shows as unsaved until its text is in storage.
/// </summary>
public class QueryState : IConsumer<QueryChanged>
{
    public static readonly TimeSpan DefaultAutoSaveDelay = TimeSpan.FromSeconds(1);

    private readonly IMessageBus _messageBus;
    private readonly IQuerySaveService _saveService;
    private readonly ILogger<QueryState> _logger;
    private readonly TimeSpan _autoSaveDelay;

    // Edits arrive faster than storage should be written, so they only signal this channel and the save
    // loop writes once they pause. One pending signal is enough because each save covers every tab.
    private readonly Channel<bool> _saveRequests = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _autoSaving;

    public event Action? StateChanged;

    public event Func<Task>? ActiveQueryTextChanged;

    /// <summary>Raised for a change that storage should also get.</summary>
    protected void OnStateChanged()
    {
        RequestSave();
        StateChanged?.Invoke();
    }

    /// <summary>Raised for a change that only affects what is on screen, such as the active tab.</summary>
    private void OnViewChanged() => StateChanged?.Invoke();

    public List<QueryModel> Queries { get; private set; } = [new() {Name = "Query1", Query = "Select * From \" \""}];

    public QueryModel? Active { get; private set; }

    private bool _initialized = false;

    /// <param name="autoSaveDelay">How long edits must pause before tabs are written; defaults to <see cref="DefaultAutoSaveDelay"/>.</param>
    public QueryState(IMessageBus messageBus, IQuerySaveService saveService, ILogger<QueryState>? logger = null, TimeSpan? autoSaveDelay = null)
    {
        _messageBus = messageBus;
        _saveService = saveService;
        _logger = logger ?? NullLogger<QueryState>.Instance;
        _autoSaveDelay = autoSaveDelay ?? DefaultAutoSaveDelay;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        var queries = await _saveService.LoadQueriesAsync();
        if (queries?.Count() >= 1)
        {
            Queries = [..queries];
            NormalizeOrder();
            foreach (var q in Queries)
            {
                q.SavedQuery = q.Query;
            }
        }

        SetActive(Queries.First());
        OnViewChanged();
        _loaded.TrySetResult();
    }

    private QueryModel AddQueryInternal(QueryModel query)
    {
        query.Order = Queries.Count;
        Queries.Add(query);
        SetActive(query);
        OnStateChanged();

        return query;
    }

    public QueryModel AddQuery(string? name = "Untitled")
    {
        var query = new QueryModel
        {
            Name = name,
            SavedQuery = "",
        };

        return AddQueryInternal(query);
    }

    public QueryModel Clone(QueryModel query)
    {
        var clone = query.Clone(true);

        return AddQueryInternal(clone);
    }

    public async Task Remove(QueryModel query)
    {
        await _messageBus.PublishAsync(new DeleteQuery(query));

        Queries.RemoveAll(x => x.Id == query.Id);
        if (Active == null || Active?.Id == query.Id)
        {
            var newActive = Queries?.FirstOrDefault();
            if (newActive != null)
            {
                SetActive(newActive);
            }
            else
            {
                AddQuery();
            }
        }

        OnStateChanged();
    }

    public void SetActive(QueryModel query)
    {
        Active = Queries.FirstOrDefault(x => x.Id.Equals(query?.Id));

        if (Active == null) return;

        OnViewChanged();
    }

    /// <param name="executedFrom">
    /// Where the run text started in the tab's SQL when only a selection was run, so the error's
    /// position can be moved from the selection into the whole text.
    /// </param>
    /// <param name="sourceText">
    /// The tab's whole text when the run started. Edits made while the statement ran are not what the
    /// error's position refers to.
    /// </param>
    public void SetResult(QueryModel query, QueryResult result, (int Line, int Column)? executedFrom = null, string? sourceText = null)
    {
        var q = Queries.FirstOrDefault(x => x.Id.Equals(query.Id));

        if (q == null) return;

        if (executedFrom is var (line, column) && result.ErrorDetail is { } error)
        {
            result.ErrorDetail = error.ShiftedTo(line, column);
        }

        q.Result = result;
        q.ResultSourceText = sourceText ?? q.Query;

        OnViewChanged();
    }

    public void UpdateQueryConnection(QueryModel query, ConnectionModel connection)
    {
        var q = Queries.FirstOrDefault(x => x.Id.Equals(query.Id));
        if (q == null) return;

        q.ConnectionId = connection.Id;
        q.DatabaseName = DefaultDatabase.For(connection);

        OnStateChanged();
    }

    public void UpdateQueryDatabase(QueryModel query, string databaseName)
    {
        var q = Queries.FirstOrDefault(x => x.Id.Equals(query.Id));
        if (q == null) return;

        q.DatabaseName = databaseName;

        OnStateChanged();
    }

    /// <summary>
    /// Replaces a tab's text from outside the editor, such as formatting, and has the editor show it.
    /// </summary>
    public async Task UpdateQueryText(QueryModel query, string queryText)
    {
        var q = Queries.FirstOrDefault(x => x.Id.Equals(query.Id));
        if (q == null) return;

        q.Query = queryText;

        OnStateChanged();

        if (IsActive(query))
        {
            await ActiveQueryTextChanged?.Invoke()!;
        }
    }

    /// <summary>
    /// Records text typed into the editor. The editor already shows it, so it is not asked to reload,
    /// and <see cref="StateChanged"/> is raised only when the tab's unsaved mark appears or goes away,
    /// so typing doesn't re-render the page on every key.
    /// </summary>
    public void EditQueryText(QueryModel query, string text)
    {
        var q = Queries.FirstOrDefault(x => x.Id.Equals(query.Id));
        if (q == null || q.Query == text) return;

        var wasDirty = q.IsDirty;
        q.Query = text;
        RequestSave();

        if (q.IsDirty != wasDirty)
        {
            OnViewChanged();
        }
    }

    public void RenameActiveQuery(string name) => RenameQuery(Active, name);

    public void RenameQuery(QueryModel? query, string name)
    {
        var q = Queries.FirstOrDefault(x => x.Id.Equals(query?.Id));
        if (q == null) return;

        q.Name = name;

        OnStateChanged();
    }

    public void ReorderQuery(Guid queryId, int newIndex)
    {
        var query = Queries.FirstOrDefault(q => q.Id == queryId);
        if (query == null) return;

        Queries.Remove(query);
        Queries.Insert(Math.Clamp(newIndex, 0, Queries.Count), query);
        NormalizeOrder();
        OnStateChanged();
    }

    public async Task CloseOthers(QueryModel query)
    {
        var toRemove = Queries.Where(q => q.Id != query.Id).ToList();
        foreach (var q in toRemove)
        {
            await _messageBus.PublishAsync(new DeleteQuery(q));
        }

        Queries.RemoveAll(q => q.Id != query.Id);
        SetActive(query);
        NormalizeOrder();
        OnStateChanged();
    }

    public async Task CloseAllTabs()
    {
        foreach (var q in Queries.ToList())
        {
            await _messageBus.PublishAsync(new DeleteQuery(q));
        }

        Queries.Clear();
        AddQuery();
    }

    public async Task CloseToRight(QueryModel query)
    {
        var toRemove = Queries.Where(q => q.Order > query.Order).ToList();
        foreach (var q in toRemove)
        {
            await _messageBus.PublishAsync(new DeleteQuery(q));
            Queries.Remove(q);
        }

        if (Active != null && !Queries.Contains(Active))
        {
            SetActive(query);
        }

        NormalizeOrder();
        OnStateChanged();
    }

    public void DetachConnection(Guid connectionId)
    {
        var attached = Queries.Where(q => q.ConnectionId == connectionId).ToList();
        if (attached.Count == 0) return;

        foreach (var query in attached)
        {
            query.ConnectionId = null;
            query.DatabaseName = null;
        }

        OnStateChanged();
    }

    /// <summary>
    /// Writes the tab to storage now, as Save Query and Run do, and clears its unsaved mark.
    /// </summary>
    public async Task SaveAsync(QueryModel query)
    {
        var q = Queries.FirstOrDefault(x => x.Id == query.Id);
        if (q == null) return;

        await SaveTabAsync(q);
        OnViewChanged();
    }

    /// <summary>
    /// Writes every open tab to storage now and clears their unsaved marks.
    /// </summary>
    public async Task SaveAllAsync()
    {
        foreach (var query in Queries.ToList())
        {
            await SaveTabAsync(query);
        }

        OnViewChanged();
    }

    private async Task SaveTabAsync(QueryModel query)
    {
        // Captured before the write starts: text typed while it is in flight isn't in storage yet.
        var text = query.Query;
        await _saveService.SaveQueryAsync(query);
        query.SavedQuery = text;
    }

    private void RequestSave() => _saveRequests.Writer.TryWrite(true);

    /// <summary>
    /// Writes the open tabs each time edits pause, until cancelled. <see cref="QueryAutoSaver"/> runs it
    /// for the app's lifetime so the tabs are only ever read and written on the UI dispatcher.
    /// </summary>
    public async Task SaveWhenEditsPauseAsync(CancellationToken cancellationToken)
    {
        if (_autoSaving) throw new InvalidOperationException("The query tabs are already being saved automatically.");
        _autoSaving = true;

        try
        {
            // Nothing is written until the saved tabs are loaded, so a save can never replace them with
            // the default tab.
            await _loaded.Task.WaitAsync(cancellationToken);

            var requests = _saveRequests.Reader;
            while (await requests.WaitToReadAsync(cancellationToken))
            {
                // Keep waiting while changes keep arriving so a burst of typing is written once.
                while (requests.TryRead(out _))
                {
                    await Task.Delay(_autoSaveDelay, cancellationToken);
                }

                await SaveOpenTabsAsync();
            }
        }
        finally
        {
            _autoSaving = false;
        }
    }

    private async Task SaveOpenTabsAsync()
    {
        foreach (var query in Queries.ToList())
        {
            // A tab closed while earlier tabs were being written must not be written back after its delete.
            if (!Queries.Contains(query)) continue;

            try
            {
                await SaveTabAsync(query);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save query tab {QueryId}", query.Id);
            }
        }

        OnViewChanged();
    }

    private void NormalizeOrder()
    {
        for (int i = 0; i < Queries.Count; i++)
        {
            Queries[i].Order = i;
        }
    }

    private bool IsActive(QueryModel query)
    {
        return Active?.Id.Equals(query.Id) ?? false;
    }

    public Task Consume(QueryChanged message)
    {
        OnStateChanged();
        return Task.CompletedTask;
    }
}
