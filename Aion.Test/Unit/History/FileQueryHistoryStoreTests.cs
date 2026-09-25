using Aion.Components.History;
using Aion.Desktop.Services;
using Aion.Test.TestDoubles;
using Shouldly;

namespace Aion.Test.Unit.History;

public class FileQueryHistoryStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aion-history-tests", Guid.NewGuid().ToString("N"));
    private readonly string _filePath;

    public FileQueryHistoryStoreTests()
    {
        _filePath = Path.Combine(_root, "Aion", "query-history.json");
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsEveryField()
    {
        var entries = new List<QueryHistoryEntry>
        {
            HistoryEntries.Failed("SELECT name FROM products WHERE category_id = 2", "no such column: category_id") with
            {
                ConnectionId = Guid.NewGuid(),
                ConnectionName = "sample_store",
                DatabaseName = "main"
            },
            HistoryEntries.Success("SELECT * FROM customers", HistoryEntries.Now.AddMinutes(-1), rows: 10),
            new() { Sql = "SELECT pg_sleep(60)", Status = QueryHistoryStatus.Cancelled, ExecutedAt = HistoryEntries.Now.AddHours(-1) }
        };
        var store = new FileQueryHistoryStore(_filePath);

        await store.SaveAsync(entries);
        var loaded = await new FileQueryHistoryStore(_filePath).LoadAsync();

        loaded.ShouldBe(entries);
    }

    [Fact]
    public async Task SaveAsync_ReplacesThePreviousListWithoutLeavingATempFile()
    {
        var store = new FileQueryHistoryStore(_filePath);
        await store.SaveAsync([HistoryEntries.Success("SELECT 1"), HistoryEntries.Success("SELECT 2")]);

        await store.SaveAsync([HistoryEntries.Success("SELECT 3")]);

        (await store.LoadAsync()).Select(e => e.Sql).ShouldBe(["SELECT 3"]);
        File.Exists(_filePath + ".tmp").ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAsync_StoresStatusByName()
    {
        await new FileQueryHistoryStore(_filePath).SaveAsync([HistoryEntries.Failed("SELECT 1", "boom")]);

        (await File.ReadAllTextAsync(_filePath)).ShouldContain("\"Status\":\"Failed\"");
    }

    [Fact]
    public async Task LoadAsync_WhenNothingWasSaved_ReturnsEmpty()
    {
        (await new FileQueryHistoryStore(_filePath).LoadAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WhenTheFileIsCorrupt_ReturnsEmpty()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await File.WriteAllTextAsync(_filePath, "[{\"Sql\": \"SELECT");

        (await new FileQueryHistoryStore(_filePath).LoadAsync()).ShouldBeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
