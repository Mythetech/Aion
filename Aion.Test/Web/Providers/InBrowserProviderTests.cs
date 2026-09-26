using System.Text.Json;
using Aion.Contracts.Database;
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
            {"columns": ["id"], "types": [23], "rows": [[1]], "affectedRows": 0, "error": null}
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
            {"columns": ["id", "price", "mood"], "types": [23, 701, 91234], "rows": [[1, 2.5, "ok"]], "affectedRows": 0, "error": null}
            """);
        var provider = new PGliteProvider(_js.Runtime);

        // Act
        var result = await provider.ExecuteQueryAsync("pglite://shop", "SELECT id, price, mood FROM products", CancellationToken.None);

        // Assert
        result.ColumnTypes.ShouldBe(["integer", "double precision", null]);
    }

    [Fact]
    public async Task PGlite_ExecuteQueryAsync_RepeatedColumnNames_KeepEachValue()
    {
        // Arrange
        RunReturns("""
            {"columns": ["id", "id"], "types": [23, 23], "rows": [[1, 2]], "affectedRows": 0, "error": null}
            """);
        var provider = new PGliteProvider(_js.Runtime);

        // Act
        var result = await provider.ExecuteQueryAsync("pglite://shop", "SELECT a.id, b.id FROM a, b", CancellationToken.None);

        // Assert
        result.ColumnNames.ShouldBe(["id", "id"]);
        result.Rows.Single()["id"].ShouldBe(1L);
        result.Rows.Single()["id_2"].ShouldBe(2L);
    }

    [Fact]
    public async Task PGlite_ExecuteQueryAsync_ArraysAndObjects_KeepTheirJsonText()
    {
        // Arrange
        RunReturns("""
            {"columns": ["tags", "extra"], "types": [1009, 91234], "rows": [[["a", "b"], {"k": 1}]], "affectedRows": 0, "error": null}
            """);
        var provider = new PGliteProvider(_js.Runtime);

        // Act
        var result = await provider.ExecuteQueryAsync("pglite://shop", "SELECT tags, extra FROM t", CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Rows.Single()["tags"].ShouldBe("""["a", "b"]""");
        result.Rows.Single()["extra"].ShouldBe("""{"k": 1}""");
    }

    private readonly List<string> _catalogQueries = [];

    // Answers each catalog query with the JSON the PGlite interop would return for it.
    private void CatalogReturns(Func<string, string> jsonFor)
    {
        // Catalog reads go through the overload that takes a cancellation token, so the arguments are third.
        _js.Module.InvokeAsync<JsonElement>("query", Arg.Any<CancellationToken>(), Arg.Any<object?[]?>())
            .Returns(call =>
            {
                var sql = (string)call.ArgAt<object?[]>(2)[1]!;
                _catalogQueries.Add(sql);
                return new ValueTask<JsonElement>(JsonDocument.Parse(jsonFor(sql)).RootElement.Clone());
            });
    }

    [Fact]
    public async Task PGlite_GetTablesAsync_CountsEveryTableExactlyInOneQuery()
    {
        // Arrange
        CatalogReturns(sql => sql.Contains("pg_tables")
            ? """{"rows": [{"schemaname": "public", "tablename": "odd\"name"}, {"schemaname": "public", "tablename": "products"}]}"""
            : """{"rows": [{"i": 0, "n": 0}, {"i": 1, "n": 15}]}""");
        var provider = new PGliteProvider(_js.Runtime);

        // Act
        var tables = await provider.GetTablesAsync("pglite://shop", "shop");

        // Assert
        tables.Select(t => (t.DisplayName, t.RowCount)).ShouldBe(
        [
            ("public.odd\"name", TableRowCount.Exact(0)),
            ("public.products", TableRowCount.Exact(15))
        ]);
        var countQuery = _catalogQueries.Where(sql => sql.Contains("count(*)")).ShouldHaveSingleItem();
        countQuery.ShouldContain("FROM \"public\".\"odd\"\"name\"");
    }

    [Fact]
    public async Task PGlite_GetTablesAsync_WithoutTables_DoesNotCount()
    {
        CatalogReturns(_ => """{"rows": []}""");
        var provider = new PGliteProvider(_js.Runtime);

        var tables = await provider.GetTablesAsync("pglite://shop", "shop");

        tables.ShouldBeEmpty();
        _catalogQueries.ShouldNotContain(sql => sql.Contains("count(*)"));
    }

    [Fact]
    public async Task PGlite_GetViewsAsync_ListsUserViewsBySchema()
    {
        CatalogReturns(_ => """{"rows": [{"table_schema": "public", "table_name": "active_customers"}, {"table_schema": "sales", "table_name": "big_orders"}]}""");
        var provider = new PGliteProvider(_js.Runtime);

        var views = await provider.GetViewsAsync("pglite://shop", "shop");

        views.ShouldBe([new TableInfo("public", "active_customers"), new TableInfo("sales", "big_orders")]);
        _catalogQueries.ShouldHaveSingleItem().ShouldContain("information_schema.views");
    }

    [Fact]
    public async Task PGlite_GetColumnsAsync_KeepsTheUdtNameOfArraysAndUserDefinedTypes()
    {
        CatalogReturns(sql => sql.Contains("information_schema.columns")
            ? """
              {"rows": [
                {"column_name": "tags", "data_type": "ARRAY", "udt_name": "_int4", "is_nullable": true, "column_default": null, "character_maximum_length": null, "is_primary_key": false, "is_identity": false},
                {"column_name": "mood", "data_type": "USER-DEFINED", "udt_name": "mood", "is_nullable": true, "column_default": null, "character_maximum_length": null, "is_primary_key": false, "is_identity": false}
              ]}
              """
            : """{"rows": []}""");
        var provider = new PGliteProvider(_js.Runtime);

        var columns = await provider.GetColumnsAsync("pglite://shop", "shop", "public", "moods");

        columns.Select(c => c.UdtName).ShouldBe(["_int4", "mood"]);
    }

    [Theory]
    [InlineData("public", "it's", "'public'", "'it''s'")]
    [InlineData("public", "odd\"name", "'public'", "'odd\"name'")]
    [InlineData("my'schema", "back\\slash", "'my''schema'", "E'back\\\\slash'")]
    public async Task PGlite_ColumnAndForeignKeyQueries_QuoteNamesAsStringLiterals(string schema, string table, string schemaLiteral, string tableLiteral)
    {
        CatalogReturns(_ => """{"rows": []}""");
        var provider = new PGliteProvider(_js.Runtime);

        await provider.GetColumnsAsync("pglite://shop", "shop", schema, table);

        _catalogQueries.Count.ShouldBe(2);
        _catalogQueries.ShouldAllBe(sql => sql.Contains($"table_name = {tableLiteral}") && sql.Contains($"table_schema = {schemaLiteral}"));
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
