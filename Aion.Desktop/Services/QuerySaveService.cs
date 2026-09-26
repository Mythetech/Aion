using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Aion.Components.Querying;
using Aion.Core.Storage;

namespace Aion.Desktop.Services;

public class FileQuerySaveService : IQuerySaveService
{
    private readonly JsonFileStore<QueryModel> _store;

    public FileQuerySaveService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aion", "saved-queries.json"))
    {
    }

    public FileQuerySaveService(string storageFile)
    {
        // Keyed by Id, not Name: every new tab is called "Untitled", so name keys let tabs
        // overwrite each other and let closing one tab delete another tab's saved copy.
        _store = new JsonFileStore<QueryModel>(storageFile, q => q.Id);
    }

    public async Task SaveQueryAsync(QueryModel query)
    {
        var q = query.Clone();
        q.Result = default;
        q.ActualPlan = default;
        q.EstimatedPlan = default;
        q.Transaction = default;

        await _store.UpsertAsync(q);
    }

    public async Task DeleteQueryAsync(QueryModel query)
    {
        await _store.RemoveAsync(query.Id);
    }

    public async Task<IEnumerable<QueryModel>> LoadQueriesAsync()
    {
        return await _store.LoadAsync();
    }
}
