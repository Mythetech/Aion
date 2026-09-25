using Aion.Test.TestDoubles;
using Aion.Web.Services;
using Shouldly;

namespace Aion.Test.Web.Services;

public class IndexedDbQuerySaveServiceTests
{
    [Fact]
    public async Task LoadQueriesAsync_RestoresTabsInTheirSavedOrder()
    {
        var js = new JsModuleFake();
        js.Returns("loadQueries", $$"""
            [
              {"id":"{{Guid.NewGuid()}}","name":"third","query":"","order":2},
              {"id":"{{Guid.NewGuid()}}","name":"first","query":"","order":0},
              {"id":"{{Guid.NewGuid()}}","name":"second","query":"","order":1}
            ]
            """);
        var sut = new IndexedDbQuerySaveService(new IndexedDbStorageService(js.Runtime));

        var queries = await sut.LoadQueriesAsync();

        queries.Select(q => q.Name).ShouldBe(["first", "second", "third"]);
    }
}
