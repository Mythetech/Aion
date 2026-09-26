using System.Text.Json;
using Aion.Components.History;

namespace Aion.Desktop.Services;

public class FileQueryHistoryStore : IQueryHistoryStore
{
    private readonly string _filePath;

    public FileQueryHistoryStore(string filePath)
    {
        _filePath = filePath;
    }

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aion", "query-history.json");

    public async Task<IReadOnlyList<QueryHistoryEntry>> LoadAsync()
    {
        if (!File.Exists(_filePath)) return [];

        await using var stream = File.OpenRead(_filePath);
        try
        {
            return await JsonSerializer.DeserializeAsync<List<QueryHistoryEntry>>(stream) ?? [];
        }
        catch (JsonException)
        {
            // A corrupt file cannot be recovered; starting empty lets the next save replace it instead of
            // leaving history unsaved for the rest of the session.
            return [];
        }
    }

    public async Task SaveAsync(IReadOnlyList<QueryHistoryEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

        // Write then move so a crash mid-write leaves the previous history intact rather than a truncated file.
        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, entries);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }
}
