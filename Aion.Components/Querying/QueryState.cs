using Aion.Components.Connections;

namespace Aion.Components.Querying;

public class QueryState
{
 public event Action? StateChanged;
 
 protected void OnStateChanged() => StateChanged?.Invoke();

    public List<QueryModel> Queries { get; } = [new() {Name = "Query1", Query = "Select * From \"AuditRecords\""}];
    
    public QueryModel? Active { get; private set; }

    public QueryState()
    {
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
}