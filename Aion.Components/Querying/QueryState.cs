using Aion.Components.Connections;
using Aion.Components.Infrastructure.MessageBus;
using Aion.Components.Querying.Commands;
using Aion.Core.Connections;
using Aion.Core.Queries;
using Aion.Components.Querying.Events;

namespace Aion.Components.Querying;

public class QueryState
{
    private readonly IMessageBus _messageBus;
    private readonly IQuerySaveService _saveService;

    public event Action? StateChanged;
 
    protected void OnStateChanged() => StateChanged?.Invoke();
    

    public List<QueryModel> Queries { get; private set; } = [new() {Name = "Query1", Query = "Select * From \" \""}];
    
    public QueryModel? Active { get; private set; }

    public QueryState(IMessageBus messageBus, IQuerySaveService saveService)
    {
        _messageBus = messageBus;
        _saveService = saveService;
        SetActive(Queries.First());
    }

    public async Task InitializeAsync()
    {
        var queries = await _saveService.LoadQueriesAsync();
        if (queries?.Count() >= 1)
        {
            Queries = [..queries];
            SetActive(Queries.First());
        }
        
        OnStateChanged();
    }

    public QueryModel AddQuery(string? name = "Untitled")
    {
        var query = new QueryModel
        {
            Name = name,
        };
        
        Queries.Add(query);
        SetActive(query);
        OnStateChanged();

        return query;
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
        Active = Queries.FirstOrDefault(x => x.Id.Equals(query.Id));
        
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
}