using System.Collections.Concurrent;
using System.Text.Json;

namespace Aion.Core.Storage;

/// <summary>
/// A list of items persisted as one JSON file, keyed by a Guid. Writes are serialized per file
/// and replace the file atomically, so concurrent saves can't lose updates or leave it truncated.
/// </summary>
public sealed class JsonFileStore<T>
{
    // Keyed by path so every store instance for the same file shares one gate; DI may create
    // more than one instance of a consuming service.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    private readonly string _filePath;
    private readonly Func<T, Guid> _keySelector;
    private readonly SemaphoreSlim _gate;

    public JsonFileStore(string filePath, Func<T, Guid> keySelector)
    {
        _filePath = Path.GetFullPath(filePath);
        _keySelector = keySelector;
        _gate = Gates.GetOrAdd(_filePath, _ => new SemaphoreSlim(1, 1));
    }

    public async Task<IReadOnlyList<T>> LoadAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return await ReadAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpsertAsync(T item)
    {
        var key = _keySelector(item);
        await _gate.WaitAsync();
        try
        {
            var items = (await ReadAsync()).Where(i => _keySelector(i) != key).ToList();
            items.Add(item);
            await WriteAsync(items);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAsync(Guid key)
    {
        await _gate.WaitAsync();
        try
        {
            var items = (await ReadAsync()).Where(i => _keySelector(i) != key).ToList();
            await WriteAsync(items);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<T>> ReadAsync()
    {
        if (!File.Exists(_filePath))
            return [];

        await using var stream = File.OpenRead(_filePath);
        var items = await JsonSerializer.DeserializeAsync<List<T>>(stream) ?? [];

        // Files written before items were keyed by id can hold several entries for one id;
        // the last one written is the newest.
        return items
            .Select((item, index) => (item, index))
            .GroupBy(x => _keySelector(x.item))
            .Select(g => g.Last())
            .OrderBy(x => x.index)
            .Select(x => x.item)
            .ToList();
    }

    private async Task WriteAsync(List<T> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var tempPath = _filePath + ".tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, items);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }
}
