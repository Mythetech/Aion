using Aion.Components.Querying;

namespace Aion.Web.Services;

public class InMemoryQuerySaveService : IQuerySaveService
{
    private readonly List<QueryModel> _queries = new();

    public Task SaveQueryAsync(QueryModel query)
    {
        var existing = _queries.FindIndex(q => q.Name == query.Name);
        if (existing >= 0)
            _queries[existing] = query;
        else
            _queries.Add(query);
        return Task.CompletedTask;
    }

    public Task DeleteQueryAsync(QueryModel query)
    {
        _queries.RemoveAll(q => q.Name == query.Name);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<QueryModel>> LoadQueriesAsync()
        => Task.FromResult(_queries.AsEnumerable());
}
