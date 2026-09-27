using Aion.Contracts.Database;
using Aion.Contracts.Queries;
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

    [Theory]
    [InlineData("SELEC $ FROM products", "SELEC", 1, 1)]
    [InlineData("SELECT $ FROM products\nWHERE n = = 1", "=", 2, 11)]
    [InlineData("SELECT $ FROM products WHERE n = '\U0001F600'\n  AND = 1", "=", 2, 7)]
    public async Task ExecuteQuery_WhenTheStatementDoesNotParse_LocatesTheTokenLiteDBStoppedAt(string sql, string token, int line, int column)
    {
        var result = await _provider.ExecuteQueryAsync(ConnectionString, sql, CancellationToken.None);

        var error = result.ErrorDetail.ShouldNotBeNull();
        error.Kind.ShouldBe(QueryErrorKind.Syntax);
        error.Token.ShouldBe(token);
        (error.Line, error.Column).ShouldBe((line, column));
    }

    [Fact]
    public async Task ExecuteQuery_WhenTheStatementEndsTooSoon_PointsAtItsEnd()
    {
        var result = await _provider.ExecuteQueryAsync(ConnectionString, "SELECT $ FROM products\nORDER BY", CancellationToken.None);

        var error = result.ErrorDetail.ShouldNotBeNull();
        error.Title.ShouldBe("Syntax error at end of input");
        (error.Line, error.Column, error.EndColumn).ShouldBe((2, 7, 9));
    }

    [Fact]
    public async Task ExecuteQuery_KeepsLiteDBsMessageAndErrorCode()
    {
        var result = await _provider.ExecuteQueryAsync(ConnectionString, "SELEC $ FROM products", CancellationToken.None);

        result.Error.ShouldBe("Unexpected token `SELEC` in position 1.");
        result.ErrorDetail.ShouldNotBeNull().Code.ShouldBe("Error 203");
    }

    [Fact]
    public async Task ExecuteInTransaction_WhenTheStatementDoesNotParse_LocatesTheTokenToo()
    {
        var transaction = await _provider.BeginTransactionAsync(ConnectionString);

        var result = await _provider.ExecuteInTransactionAsync(ConnectionString, "SELECT $ FROM products\nWHERE n = = 1", transaction.Id, CancellationToken.None);
        await _provider.RollbackTransactionAsync(ConnectionString, transaction.Id);

        var error = result.ErrorDetail.ShouldNotBeNull();
        (error.Line, error.Column).ShouldBe((2, 11));
    }
}
