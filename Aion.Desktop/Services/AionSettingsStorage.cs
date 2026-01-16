using LiteDB;
using Mythetech.Framework.Infrastructure.Settings;

namespace Aion.Desktop.Services;

/// <summary>
/// LiteDB implementation of ISettingsStorage for persisting framework settings.
/// Stores settings in the Aion AppData folder alongside connections and queries.
/// </summary>
public class AionSettingsStorage : ISettingsStorage
{
    private const string DatabaseName = "settings.db";
    private const string CollectionName = "settings";

    private static string GetDatabasePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var aionPath = Path.Combine(appDataPath, "Aion");

        if (!Directory.Exists(aionPath))
            Directory.CreateDirectory(aionPath);

        return Path.Combine(aionPath, DatabaseName);
    }

    private LiteDatabase GetDatabase() => new(GetDatabasePath());

    public Task SaveSettingsAsync(string settingsId, string jsonData)
    {
        using var db = GetDatabase();
        var collection = db.GetCollection<SettingsDocument>(CollectionName);

        var doc = new SettingsDocument
        {
            SettingsId = settingsId,
            JsonData = jsonData,
            UpdatedAt = DateTime.UtcNow
        };

        collection.Upsert(doc);
        return Task.CompletedTask;
    }

    public Task<string?> LoadSettingsAsync(string settingsId)
    {
        using var db = GetDatabase();
        var collection = db.GetCollection<SettingsDocument>(CollectionName);

        var doc = collection.FindById(settingsId);
        return Task.FromResult(doc?.JsonData);
    }

    public Task<Dictionary<string, string>> LoadAllSettingsAsync()
    {
        using var db = GetDatabase();
        var collection = db.GetCollection<SettingsDocument>(CollectionName);

        var result = collection.FindAll()
            .ToDictionary(d => d.SettingsId, d => d.JsonData);

        return Task.FromResult(result);
    }

    private class SettingsDocument
    {
        [BsonId]
        public string SettingsId { get; set; } = "";
        public string JsonData { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
    }
}
