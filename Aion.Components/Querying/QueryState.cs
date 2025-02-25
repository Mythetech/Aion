using Aion.Components.Connections;
using Aion.Components.Infrastructure.MessageBus;
using Aion.Core.Connections;
using Aion.Core.Queries;
using Aion.Components.Querying.Events;

namespace Aion.Components.Querying;

public class QueryState
{
    private readonly IMessageBus _messageBus;
    private readonly ConnectionState _connectionState;

    public event Action? StateChanged;
 
    protected void OnStateChanged() => StateChanged?.Invoke();

    public List<QueryModel> Queries { get; } = [new() {Name = "Query1", Query = "Select * From \" \""}];
    
    public QueryModel? Active { get; private set; }

    public QueryState(IMessageBus messageBus, ConnectionState connectionState)
    {
        _messageBus = messageBus;
        _connectionState = connectionState;
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
        q.DatabaseName = null; // Reset database when connection changes
        
        OnStateChanged();
    }

    public void UpdateQueryDatabase(QueryModel query, string databaseName)
    {
        var q = Queries.FirstOrDefault(x => x.Id.Equals(query.Id));
        if (q == null) return;
        
        q.DatabaseName = databaseName;
        
        OnStateChanged();
    }

    public async Task ExecuteQueryAsync(QueryModel query, CancellationToken cancellationToken)
    {
        try
        {
            var connection = _connectionState.Connections.FirstOrDefault(x => x.Id == query.ConnectionId);
            if (connection == null) return;

            var provider = _connectionState.GetProvider(connection.Type);
            
            if (query.UseTransaction && query.Transaction == null)
            {
                // Start new transaction
                query.Transaction = await provider.BeginTransactionAsync(connection.ConnectionString);
                await _messageBus.PublishAsync(new TransactionStarted(connection.Id, query.Transaction.Value));
            }

            query.IsExecuting = true;
            OnStateChanged();

            QueryResult result;
            if (query.Transaction?.Status == TransactionStatus.Active)
            {
                result = await provider.ExecuteInTransactionAsync(
                    connection.ConnectionString,
                    query.Query,
                    query.Transaction.Value.Id,
                    cancellationToken);
            }
            else
            {
                result = await provider.ExecuteQueryAsync(
                    connection.ConnectionString,
                    query.Query,
                    cancellationToken);
            }

            query.Result = result;
            query.IsExecuting = false;
            
            OnStateChanged();
            await _messageBus.PublishAsync(new QueryExecuted(query));
        }
        catch (Exception ex)
        {
            // ... error handling ...
        }
    }
}