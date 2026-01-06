using Aion.Components.Connections;
using Mythetech.Framework.Infrastructure.MessageBus;
using Aion.Components.Querying.Commands;
using Aion.Core.Connections;
using Aion.Core.Queries;
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

    private bool _initializing = false;

    public QueryState(IMessageBus messageBus, IQuerySaveService saveService)
    {
        _messageBus = messageBus;
        _saveService = saveService;
    }

    public async Task InitializeAsync()
    {
        if (_initializing) return;
        
        _initializing = true;
        var queries = await _saveService.LoadQueriesAsync();
        if (queries?.Count() >= 1)
        {
            Queries = [..queries];
            SetActive(Queries.First());
        }
        
        OnStateChanged();
        _initializing = false;
    }
    
    private QueryModel AddQueryInternal(QueryModel query)
    {
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

    public void SetTransactionInfo(TransactionInfo transactionInfo)
    {
        Active.Transaction = transactionInfo;
        OnStateChanged();
    }

    public void SetActive(QueryModel query)
    {
        Active = Queries.FirstOrDefault(x => x.Id.Equals(query?.Id));

        if (Active == null) return;

        Active.IsExecuting = false;
        
        OnStateChanged();
    }

    public void SetResult(QueryModel query, QueryResult result)
    {
        var q = Queries.FirstOrDefault(x => x.Id.Equals(query.Id));

        if (q == null) return;
        
        q.Result = result;
        
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