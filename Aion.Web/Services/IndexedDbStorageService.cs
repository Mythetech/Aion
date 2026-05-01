using System.Text.Json;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Microsoft.JSInterop;

namespace Aion.Web.Services;

public class IndexedDbStorageService
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private bool _persistenceRequested;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public IndexedDbStorageService(IJSRuntime js)
    {
        _js = js;
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./js/aion-storage.js");

        if (!_persistenceRequested)
        {
            _persistenceRequested = true;
            await _module.InvokeVoidAsync("requestPersistence");
        }

        return _module;
    }

    public async Task SaveConnectionAsync(ConnectionModel connection)
    {
        var module = await GetModuleAsync();
        var json = JsonSerializer.Serialize(new ConnectionRecord(connection), JsonOptions);
        await module.InvokeVoidAsync("saveConnection", json);
    }

    public async Task<List<ConnectionRecord>> LoadConnectionsAsync()
    {
        var module = await GetModuleAsync();
        var json = await module.InvokeAsync<string>("loadConnections");
        return JsonSerializer.Deserialize<List<ConnectionRecord>>(json, JsonOptions) ?? [];
    }

    public async Task DeleteConnectionAsync(Guid id)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("deleteConnection", id.ToString());
    }

    public async Task SaveQueryAsync(QueryRecord query)
    {
        var module = await GetModuleAsync();
        var json = JsonSerializer.Serialize(query, JsonOptions);
        await module.InvokeVoidAsync("saveQuery", json);
    }

    public async Task<List<QueryRecord>> LoadQueriesAsync()
    {
        var module = await GetModuleAsync();
        var json = await module.InvokeAsync<string>("loadQueries");
        return JsonSerializer.Deserialize<List<QueryRecord>>(json, JsonOptions) ?? [];
    }

    public async Task DeleteQueryAsync(Guid id)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("deleteQuery", id.ToString());
    }

    public async Task SaveDatabaseMetaAsync(string name, DatabaseType type)
    {
        var module = await GetModuleAsync();
        var meta = new DatabaseMeta(name, type, DateTime.UtcNow);
        var json = JsonSerializer.Serialize(meta, JsonOptions);
        await module.InvokeVoidAsync("saveDatabaseMeta", json);
    }

    public async Task<List<DatabaseMeta>> LoadDatabaseMetasAsync()
    {
        var module = await GetModuleAsync();
        var json = await module.InvokeAsync<string>("loadDatabaseMetas");
        return JsonSerializer.Deserialize<List<DatabaseMeta>>(json, JsonOptions) ?? [];
    }

    public async Task DeleteDatabaseMetaAsync(string name)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("deleteDatabaseMeta", name);
    }

    public async Task ClearAllAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("clearAll");
    }
}

public record ConnectionRecord
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string ConnectionString { get; init; } = "";
    public int Type { get; init; }
    public bool SaveCredentials { get; init; }

    public ConnectionRecord() { }

    public ConnectionRecord(ConnectionModel model)
    {
        Id = model.Id.ToString();
        Name = model.Name;
        ConnectionString = model.ConnectionString;
        Type = (int)model.Type;
        SaveCredentials = model.SaveCredentials;
    }

    public ConnectionModel ToConnectionModel() => new()
    {
        Id = Guid.Parse(Id),
        Name = Name,
        ConnectionString = ConnectionString,
        Type = (DatabaseType)Type,
        SaveCredentials = SaveCredentials,
        IsSavedConnection = true
    };
}

public record QueryRecord
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Query { get; init; } = "";
    public string? ConnectionId { get; init; }
    public string? DatabaseName { get; init; }
    public int Order { get; init; }

    public QueryRecord() { }

    public QueryRecord(Aion.Components.Querying.QueryModel model)
    {
        Id = model.Id.ToString();
        Name = model.Name ?? "";
        Query = model.Query ?? "";
        ConnectionId = model.ConnectionId?.ToString();
        DatabaseName = model.DatabaseName;
        Order = model.Order;
    }
}

public record DatabaseMeta(string Name, DatabaseType Type, DateTime CreatedAt);
