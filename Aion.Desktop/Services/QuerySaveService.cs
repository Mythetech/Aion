using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aion.Components.Querying;

namespace Aion.Desktop.Services;

public class FileQuerySaveService : IQuerySaveService
{
    private readonly string _storageFile;
    private readonly object _lock = new();

    public FileQuerySaveService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _storageFile = Path.Combine(appData, "Aion", "saved-queries.json");
    }

    public async Task SaveQueryAsync(QueryModel query)
    {
        var q = query.Clone();
        var queries = await LoadQueriesAsync();
        var queryList = queries.ToList();
        
        queryList.RemoveAll(q => q.Name == query.Name);
        q.Result = default;
        q.ActualPlan = default;
        q.EstimatedPlan = default;
        q.Transaction = default;
        queryList.Add(q);
        
        await SaveQueriesAsync(queryList);
    }

    public async Task DeleteQueryAsync(QueryModel query)
    {
        var queries = await LoadQueriesAsync();
        var queryList = queries.ToList();
        
        queryList.RemoveAll(q => q.Name == query.Name);
        
        await SaveQueriesAsync(queryList);
    }

    public async Task<IEnumerable<QueryModel>> LoadQueriesAsync()
    {
        if (!File.Exists(_storageFile))
            return Enumerable.Empty<QueryModel>();

        var json = await File.ReadAllTextAsync(_storageFile);
        return JsonSerializer.Deserialize<List<QueryModel>>(json) ?? new List<QueryModel>();
    }

    private async Task SaveQueriesAsync(IEnumerable<QueryModel> queries)
    {
        var directory = Path.GetDirectoryName(_storageFile);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory!);

        await File.WriteAllTextAsync(_storageFile, 
            JsonSerializer.Serialize(queries));
    }
} 