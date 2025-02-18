namespace Aion.Components.Querying;

public class QueryState
{
 public event Action? StateChanged;
 
 protected void OnStateChanged() => StateChanged?.Invoke();

    public List<QueryModel> Queries { get; } = [new() {Name = "Query1", Query = "Select * From AspNetUsers"}];
    
    public QueryModel? Active { get; private set; }

    public QueryState()
    {
        SetActive(Queries.First());
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
}