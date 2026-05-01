using Aion.Components.Querying;

namespace Aion.Web.Services;

public class IndexedDbQuerySaveService : IQuerySaveService
{
    private readonly IndexedDbStorageService _storage;

    public IndexedDbQuerySaveService(IndexedDbStorageService storage)
    {
        _storage = storage;
    }

    public async Task SaveQueryAsync(QueryModel query)
    {
        var record = new QueryRecord(query);
        await _storage.SaveQueryAsync(record);
    }

    public async Task DeleteQueryAsync(QueryModel query)
    {
        await _storage.DeleteQueryAsync(query.Id);
    }

    public async Task<IEnumerable<QueryModel>> LoadQueriesAsync()
    {
        var records = await _storage.LoadQueriesAsync();
        return records.Select(r => new QueryModel
        {
            Id = Guid.Parse(r.Id),
            Name = r.Name,
            Query = r.Query,
            ConnectionId = string.IsNullOrEmpty(r.ConnectionId) ? null : Guid.Parse(r.ConnectionId),
            DatabaseName = r.DatabaseName,
            Order = r.Order
        });
    }
}
