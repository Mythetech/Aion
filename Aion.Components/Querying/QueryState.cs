using Aion.Components.Connections;
using Mythetech.Framework.Infrastructure.MessageBus;
using Aion.Components.Querying.Commands;
using Aion.Contracts.Connections;
using Aion.Contracts.Queries;
using Aion.Components.Querying.Events;

namespace Aion.Components.Querying;

public class QueryState : IConsumer<QueryChanged>
{
    private readonly IMessageBus _messageBus;
    private readonly IQuerySaveService _saveService;

    public event Action? StateChanged;

    public event Func<Task>? ActiveQueryTextChanged;
 
    protected void OnStateChanged() => StateChanged?.Invoke();
    

    public List<QueryModel> Queries { get; private set; } = [new() {Name = "Query1", Query = "Select * From \" \""}];
    
    public QueryModel? Active { get; private set; }

    private bool _initialized = false;

    public QueryState(IMessageBus messageBus, IQuerySaveService saveService)
    {
        _messageBus = messageBus;
        _saveService = saveService;
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
        OnStateChanged();
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

        OnStateChanged();
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

        OnStateChanged();
    }

    public void UpdateQueryConnection(QueryModel query, ConnectionModel connection)
    {
        var q = Queries.FirstOrDefault(x => x.Id.Equals(query.Id));
        if (q == null) return;
        
        q.ConnectionId = connection.Id;
        q.DatabaseName = null; 
        
        OnStateChanged();
    }

    public void UpdateQueryDatabase(QueryModel query, string databaseName)
    {
        var q = Queries.FirstOrDefault(x => x.Id.Equals(query.Id));
        if (q == null) return;
        
        q.DatabaseName = databaseName;
        
        OnStateChanged();
    }
    
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

    public void MarkSaved(QueryModel query)
    {
        var q = Queries.FirstOrDefault(x => x.Id == query.Id);
        if (q == null) return;

        q.SavedQuery = q.Query;
        OnStateChanged();
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