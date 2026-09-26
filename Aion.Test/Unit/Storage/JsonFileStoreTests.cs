using Aion.Core.Storage;
using Shouldly;

namespace Aion.Test.Unit.Storage;

public class JsonFileStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"aion-store-{Guid.NewGuid():N}");
    private string FilePath => Path.Combine(_directory, "items.json");

    public record Item(Guid Id, string Name, string Text);

    private JsonFileStore<Item> CreateStore() => new(FilePath, i => i.Id);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsEmpty()
    {
        var items = await CreateStore().LoadAsync();

        items.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpsertAsync_ItemsSharingAName_AreStoredSeparately()
    {
        var store = CreateStore();
        var first = new Item(Guid.NewGuid(), "Untitled", "SELECT 1");
        var second = new Item(Guid.NewGuid(), "Untitled", "SELECT 2");

        await store.UpsertAsync(first);
        await store.UpsertAsync(second);

        var items = await store.LoadAsync();
        items.Count.ShouldBe(2);
        items.ShouldContain(first);
        items.ShouldContain(second);
    }

    [Fact]
    public async Task UpsertAsync_SameKey_ReplacesTheItem()
    {
        var store = CreateStore();
        var id = Guid.NewGuid();

        await store.UpsertAsync(new Item(id, "Old name", "SELECT 1"));
        await store.UpsertAsync(new Item(id, "Renamed", "SELECT 2"));

        var items = await store.LoadAsync();
        items.ShouldHaveSingleItem().ShouldBe(new Item(id, "Renamed", "SELECT 2"));
    }

    [Fact]
    public async Task RemoveAsync_OnlyRemovesTheMatchingKey()
    {
        var store = CreateStore();
        var closed = new Item(Guid.NewGuid(), "Untitled", "SELECT 1");
        var kept = new Item(Guid.NewGuid(), "Untitled", "SELECT 2");
        await store.UpsertAsync(closed);
        await store.UpsertAsync(kept);

        await store.RemoveAsync(closed.Id);

        var items = await store.LoadAsync();
        items.ShouldHaveSingleItem().ShouldBe(kept);
    }

    [Fact]
    public async Task UpsertAsync_ConcurrentWrites_LoseNothing()
    {
        var items = Enumerable.Range(0, 50)
            .Select(i => new Item(Guid.NewGuid(), $"Query {i}", $"SELECT {i}"))
            .ToList();

        await Task.WhenAll(items.Select(i => CreateStore().UpsertAsync(i)));

        var stored = await CreateStore().LoadAsync();
        stored.Count.ShouldBe(items.Count);
        stored.ShouldBe(items, ignoreOrder: true);
    }

    [Fact]
    public async Task LoadAsync_DuplicateKeysInFile_KeepsTheLastEntry()
    {
        var id = Guid.NewGuid();
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(FilePath,
            $$"""[{"Id":"{{id}}","Name":"Before rename","Text":"SELECT 1"},{"Id":"{{id}}","Name":"After rename","Text":"SELECT 2"}]""");

        var items = await CreateStore().LoadAsync();

        items.ShouldHaveSingleItem().Name.ShouldBe("After rename");
    }

    [Fact]
    public async Task UpsertAsync_LeavesNoTemporaryFileBehind()
    {
        await CreateStore().UpsertAsync(new Item(Guid.NewGuid(), "Query", "SELECT 1"));

        Directory.GetFiles(_directory).ShouldBe([FilePath]);
    }
}
