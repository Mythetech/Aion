using System.Text.Json;
using Aion.Contracts.Queries;
using Aion.Test.TestDoubles;
using Aion.Web.Providers;
using Microsoft.JSInterop;
using NSubstitute;
using Shouldly;
using SqliteWasmBlazor;

namespace Aion.Test.Web.Providers;

public class InBrowserProviderTests
{
    private readonly JsModuleFake _js = new();

    [Fact]
    public async Task PGlite_GetDatabasesAsync_ListsOnlyTheConnectionsOwnDatabase()
    {
        var provider = new PGliteProvider(_js.Runtime);
        await provider.EnsureDatabaseAsync("sales");
        await provider.EnsureDatabaseAsync("inventory");

        var databases = await provider.GetDatabasesAsync("pglite://sales");

        databases.ShouldBe(["sales"]);
    }

    [Fact]
    public async Task PGlite_GetDatabasesAsync_ListsTheDatabaseBeforeItIsOpened()
    {
        var provider = new PGliteProvider(_js.Runtime);

        var databases = await provider.GetDatabasesAsync("pglite://sales");

        databases.ShouldBe(["sales"]);
    }

    [Theory]
    [InlineData("aion")]
    [InlineData("storage")]
    [InlineData("a")]
    public async Task PGlite_DeleteDatabaseAsync_DestroysExactlyTheNamedDatabase(string name)
    {
        var provider = new PGliteProvider(_js.Runtime);

        await provider.DeleteDatabaseAsync(name);

        var destroyCalls = _js.CallsTo("destroy");
        destroyCalls.Count.ShouldBe(1);
        destroyCalls[0].ShouldBe([name]);
    }

    private void RunReturns(string json)
    {
        _js.Module.InvokeAsync<JsonElement>("run", Arg.Any<object?[]?>())
            .Returns(_ => new ValueTask<JsonElement>(JsonDocument.Parse(json).RootElement.Clone()));
    }

    [Fact]
    public async Task PGlite_ExecuteQueryAsync_FailedStatement_KeepsTheEngineCodeAndMessage()
    {
        // Arrange
        RunReturns("""
            {"error": {"message": "column \"categry_id\" does not exist", "code": "42703", "position": "33", "detail": null, "hint": null}}
            """);
        var provider = new PGliteProvider(_js.Runtime);

        // Act
        var result = await provider.ExecuteQueryAsync("pglite://shop", "SELECT name FROM products WHERE categry_id = 2", CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("42703: column \"categry_id\" does not exist");
        result.ErrorDetail.ShouldNotBeNull();
        result.ErrorDetail.Code.ShouldBe("SQLSTATE 42703");
        result.ErrorDetail.Kind.ShouldBe(QueryErrorKind.UnknownColumn);
        result.ErrorDetail.Token.ShouldBe("categry_id");
        result.ErrorDetail.Line.ShouldBe(1);
        result.ErrorDetail.Column.ShouldBe(33);
        result.ErrorDetail.EndColumn.ShouldBe(43);
    }

    [Fact]
    public async Task PGlite_ExecuteQueryAsync_FailedStatement_KeepsDetailAndHintInTheRawText()
    {
        // Arrange
        RunReturns("""
            {"error": {"message": "duplicate key value violates unique constraint \"products_pkey\"", "code": "23505", "detail": "Key (id)=(1) already exists.", "hint": "Pick another id."}}
            """);
        var provider = new PGliteProvider(_js.Runtime);

        // Act
        var result = await provider.ExecuteQueryAsync("pglite://shop", "INSERT INTO products (id) VALUES (1)", CancellationToken.None);

        // Assert
        result.Error.ShouldBe("23505: duplicate key value violates unique constraint \"products_pkey\"\n\nDETAIL: Key (id)=(1) already exists.\nHINT: Pick another id.");
        result.ErrorDetail!.Title.ShouldBe("Duplicate key value violates unique constraint \"products_pkey\"");
    }

    [Fact]
    public async Task PGlite_ExecuteQueryAsync_SuccessfulStatement_ReadsRows()
    {
        // Arrange
        RunReturns("""
            {"columns": ["id"], "rows": [{"id": 1}], "affectedRows": 0, "error": null}
            """);
        var provider = new PGliteProvider(_js.Runtime);

        // Act
        var result = await provider.ExecuteQueryAsync("pglite://shop", "SELECT id FROM products", CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Rows.Single()["id"].ShouldBe(1L);
    }

    [Fact]
    public async Task PGlite_ExecuteQueryAsync_NamesEachColumnsTypeFromItsTypeId()
    {
        // Arrange
        RunReturns("""
            {"columns": ["id", "price", "mood"], "types": [23, 701, 91234], "rows": [{"id": 1, "price": 2.5, "mood": "ok"}], "affectedRows": 0, "error": null}
            """);
        var provider = new PGliteProvider(_js.Runtime);

        // Act
        var result = await provider.ExecuteQueryAsync("pglite://shop", "SELECT id, price, mood FROM products", CancellationToken.None);

        // Assert
        result.ColumnTypes.ShouldBe(["integer", "double precision", null]);
    }

    [Fact]
    public async Task SqliteWasm_GetDatabasesAsync_ListsOnlyTheConnectionsOwnDatabase()
    {
        var provider = new SqliteWasmProvider(Substitute.For<ISqliteWasmDatabaseService>());
        await provider.EnsureDatabaseAsync("sales");
        await provider.EnsureDatabaseAsync("inventory");

        var databases = await provider.GetDatabasesAsync("Data Source=sales;Mode=Memory;Cache=Shared");

        databases.ShouldBe(["sales"]);
    }

    [Fact]
    public async Task SqliteWasm_GetDatabasesAsync_WithoutDataSource_ListsNothing()
    {
        var provider = new SqliteWasmProvider(Substitute.For<ISqliteWasmDatabaseService>());

        var databases = await provider.GetDatabasesAsync("Mode=Memory");

        databases.ShouldNotBeNull();
        databases.ShouldBeEmpty();
    }

    [Fact]
    public async Task SqliteWasm_DeleteDatabaseAsync_DeletesOnlyThatDatabaseFile()
    {
        var databaseService = Substitute.For<ISqliteWasmDatabaseService>();
        var provider = new SqliteWasmProvider(databaseService);

        await provider.DeleteDatabaseAsync("sales");

        await databaseService.Received(1).DeleteDatabaseAsync("sales.db", Arg.Any<CancellationToken>());
        await databaseService.DidNotReceive().DeleteDatabaseAsync(Arg.Is<string>(n => n != "sales.db"), Arg.Any<CancellationToken>());
    }
}
