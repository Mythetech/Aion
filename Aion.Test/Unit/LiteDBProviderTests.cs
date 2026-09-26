using Aion.Contracts.Database;
using Aion.Core.Database.LiteDB;
using LiteDB;
using Shouldly;

namespace Aion.Test.Unit;

public sealed class LiteDBProviderTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"aion-litedb-{Guid.NewGuid():N}.db");
    private readonly LiteDBProvider _provider = new();

    private string ConnectionString => $"Filename={_path};Connection=shared";

    public void Dispose()
    {
        File.Delete(_path);
    }

    [Fact]
    public void HasNoViews()
    {
        _provider.ShouldNotBeAssignableTo<IDatabaseViewProvider>();
    }

    [Fact]
    public async Task GetTablesAsync_CountsEachCollectionsDocumentsExactly()
    {
        using (var db = new LiteDatabase(ConnectionString))
        {
            db.GetCollection("products").InsertBulk(Enumerable.Range(1, 3).Select(i => new BsonDocument { ["n"] = i }));
            db.GetCollection("empty").Insert(new BsonDocument { ["n"] = 0 });
            db.GetCollection("empty").DeleteAll();
        }

        var tables = await _provider.GetTablesAsync(ConnectionString, "db");

        tables.Select(t => (t.Name, t.RowCount)).ShouldBe(
        [
            ("empty", TableRowCount.Exact(0)),
            ("products", TableRowCount.Exact(3))
        ]);
    }
}
