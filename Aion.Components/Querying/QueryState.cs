using Aion.Components.Connections;
using Aion.Components.Infrastructure.MessageBus;
using Aion.Core.Connections;
using Aion.Core.Queries;
using Aion.Components.Querying.Events;

namespace Aion.Components.Querying;

public class QueryState
{
    private readonly IMessageBus _messageBus;

    public event Action? StateChanged;
 
    protected void OnStateChanged() => StateChanged?.Invoke();
    

    public List<QueryModel> Queries { get; } = [new() {Name = "Query1", Query = "Select * From \" \""}];
    
    public QueryModel? Active { get; private set; }

    public QueryState(IMessageBus messageBus)
    {
        _messageBus = messageBus;
        SetActive(Queries.First());
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

    public void Remove(QueryModel query)
    {
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