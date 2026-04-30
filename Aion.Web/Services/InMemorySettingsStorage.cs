using Mythetech.Framework.Infrastructure.Settings;

namespace Aion.Web.Services;

public class InMemorySettingsStorage : ISettingsStorage
{
    private readonly Dictionary<string, string> _store = new();

    public Task SaveSettingsAsync(string settingsId, string jsonData)
    {
        _store[settingsId] = jsonData;
        return Task.CompletedTask;
    }

    public Task<string?> LoadSettingsAsync(string settingsId)
    {
        _store.TryGetValue(settingsId, out var data);
        return Task.FromResult(data);
    }

    public Task<Dictionary<string, string>> LoadAllSettingsAsync()
        => Task.FromResult(new Dictionary<string, string>(_store));
}
