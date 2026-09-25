using Aion.Components.History;
using Aion.Test.TestDoubles;
using Aion.Web.Services;
using Bunit;
using Shouldly;

namespace Aion.Test.Unit.History;

public class IndexedDbQueryHistoryStoreTests : TestContext
{
    private readonly BunitJSModuleInterop _module;
    private readonly IndexedDbQueryHistoryStore _store;

    public IndexedDbQueryHistoryStoreTests()
    {
        _module = JSInterop.SetupModule("./js/aion-storage.js");
        _module.SetupVoid("requestPersistence").SetVoidResult();
        _module.SetupVoid("replaceHistory", _ => true).SetVoidResult();
        _store = new IndexedDbQueryHistoryStore(new IndexedDbStorageService(JSInterop.JSRuntime));
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsThroughTheStorageModule()
    {
        var entries = new List<QueryHistoryEntry>
        {
            HistoryEntries.Failed("SELECT name FROM products WHERE category_id = 2", "no such column: category_id") with
            {
                ConnectionId = Guid.NewGuid(),
                ConnectionName = "sample_store",
                DatabaseName = "main"
            },
            HistoryEntries.Success("SELECT * FROM customers", HistoryEntries.Now.AddMinutes(-1), rows: 10)
        };

        await _store.SaveAsync(entries);
        var json = _module.Invocations["replaceHistory"].Single().Arguments[0].ShouldBeOfType<string>();
        _module.Setup<string>("loadHistory").SetResult(json);
        var loaded = await _store.LoadAsync();

        loaded.ShouldBe(entries);
    }

    [Fact]
    public async Task SaveAsync_UsesTheCamelCaseIdTheObjectStoreIsKeyedOn()
    {
        var entry = HistoryEntries.Failed("SELECT 1", "boom");

        await _store.SaveAsync([entry]);

        var json = _module.Invocations["replaceHistory"].Single().Arguments[0].ShouldBeOfType<string>();
        json.ShouldContain($"\"id\":\"{entry.Id}\"");
        json.ShouldContain("\"status\":\"Failed\"");
    }
}
