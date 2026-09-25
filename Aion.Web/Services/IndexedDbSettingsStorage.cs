using Mythetech.Framework.Infrastructure.Settings;

namespace Aion.Web.Services;

/// <summary>
/// Keeps framework settings in the browser's IndexedDB, in the same database as connections and queries,
/// so they survive a reload.
/// </summary>
public class IndexedDbSettingsStorage : ISettingsStorage
{
    private readonly IndexedDbStorageService _storage;

    public IndexedDbSettingsStorage(IndexedDbStorageService storage)
    {
        _storage = storage;
    }

    public Task SaveSettingsAsync(string settingsId, string jsonData)
        => _storage.SaveSettingsAsync(settingsId, jsonData);

    public Task<string?> LoadSettingsAsync(string settingsId)
        => _storage.LoadSettingsAsync(settingsId);

    public async Task<Dictionary<string, string>> LoadAllSettingsAsync()
    {
        var records = await _storage.LoadAllSettingsAsync();
        return records.ToDictionary(r => r.SettingsId, r => r.Json);
    }
}
