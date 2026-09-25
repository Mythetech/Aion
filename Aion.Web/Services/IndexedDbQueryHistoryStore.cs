using Aion.Components.History;

namespace Aion.Web.Services;

public class IndexedDbQueryHistoryStore : IQueryHistoryStore
{
    private readonly IndexedDbStorageService _storage;

    public IndexedDbQueryHistoryStore(IndexedDbStorageService storage)
    {
        _storage = storage;
    }

    public async Task<IReadOnlyList<QueryHistoryEntry>> LoadAsync() => await _storage.LoadHistoryAsync();

    public async Task SaveAsync(IReadOnlyList<QueryHistoryEntry> entries) => await _storage.ReplaceHistoryAsync(entries);
}
