using System.Text.Json;
using Aion.Test.TestDoubles;
using Aion.Web.Providers;
using Microsoft.JSInterop;
using NSubstitute;
using NSubstitute.Core;
using Shouldly;

namespace Aion.Test.Web.Providers;

/// <summary>
/// PGlite runs every tab on one session per database, so catalog reads made while a tab holds a transaction
/// join it. These check which reads go through the savepoint the interop module wraps them in.
/// </summary>
public class PGliteCatalogReadTests
{
    private const string Shop = "pglite://shop";
    private const string NoRows = """{"columns": [], "rows": []}""";
    private const string PlanRows = """{"columns": ["QUERY PLAN"], "rows": [{"QUERY PLAN": "Seq Scan on products"}]}""";

    private readonly JsModuleFake _js = new();
    private readonly PGliteProvider _sut;

    public PGliteCatalogReadTests()
    {
        foreach (var function in new[] { "query", "readInSavepoint" })
        {
            _js.Module.InvokeAsync<JsonElement>(function, Arg.Any<CancellationToken>(), Arg.Any<object?[]?>())
                .Returns(call => new ValueTask<JsonElement>(Json(
                    call.ArgAt<object?[]>(2)[1] is string sql && sql.StartsWith("EXPLAIN") ? PlanRows : NoRows)));
        }

        _sut = new PGliteProvider(_js.Runtime);
    }

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static bool IsCatalogCall(ICall call) =>
        call.GetMethodInfo().Name == nameof(IJSObjectReference.InvokeAsync)
        && call.GetArguments()[0] is "query" or "readInSavepoint";

    /// <summary>Each catalog read as (interop function, SQL).</summary>
    private List<(string Function, string Sql)> CatalogReads() =>
        _js.Module.ReceivedCalls()
            .Where(IsCatalogCall)
            .Select(call => (Function: (string)call.GetArguments()[0]!, Args: (object?[])call.GetArguments()[^1]!))
            .Select(call => (call.Function, (string)call.Args[1]!))
            .ToList();

    private async Task ReadEverySchemaPartAsync(string connectionString, string database)
    {
        await _sut.GetTablesAsync(connectionString, database);
        await _sut.GetColumnsAsync(connectionString, database, "public", "products");
        await _sut.GetIndexesAsync(connectionString, database);
    }

    [Fact]
    public async Task SchemaReads_WithoutATransaction_QueryDirectly()
    {
        // Act
        await ReadEverySchemaPartAsync(Shop, "shop");

        // Assert
        CatalogReads().Select(r => r.Function).ShouldAllBe(f => f == "query");
        CatalogReads().Count.ShouldBe(4);
    }

    [Fact]
    public async Task SchemaReads_WhileATransactionIsOpen_RunInsideASavepoint()
    {
        // Arrange
        await _sut.BeginTransactionAsync(Shop);

        // Act
        await ReadEverySchemaPartAsync(Shop, "shop");

        // Assert: tables, columns, their foreign keys and indexes.
        CatalogReads().Select(r => r.Function).ShouldBe(["readInSavepoint", "readInSavepoint", "readInSavepoint", "readInSavepoint"]);
    }

    [Fact]
    public async Task SchemaReads_OfAnotherDatabase_QueryDirectly()
    {
        // Arrange
        await _sut.BeginTransactionAsync(Shop);

        // Act
        await ReadEverySchemaPartAsync("pglite://hr", "hr");

        // Assert
        CatalogReads().Select(r => r.Function).ShouldAllBe(f => f == "query");
    }

    [Fact]
    public async Task SchemaReads_AfterTheTransactionEnds_QueryDirectlyAgain()
    {
        // Arrange
        var transaction = await _sut.BeginTransactionAsync(Shop);
        await _sut.RollbackTransactionAsync(Shop, transaction.Id);
        _js.Module.ClearReceivedCalls();

        // Act
        await ReadEverySchemaPartAsync(Shop, "shop");

        // Assert
        CatalogReads().Select(r => r.Function).ShouldAllBe(f => f == "query");
    }

    [Fact]
    public async Task EstimatedPlan_WhileATransactionIsOpen_RunsInsideASavepoint()
    {
        // Arrange
        await _sut.BeginTransactionAsync(Shop);

        // Act
        var plan = await _sut.GetEstimatedPlanAsync(Shop, "SELECT * FROM products", CancellationToken.None);

        // Assert
        plan.PlanContent.ShouldContain("Seq Scan on products");
        CatalogReads().ShouldBe([("readInSavepoint", "EXPLAIN SELECT * FROM products")]);
    }

    [Fact]
    public async Task GetColumns_WithAQuoteInTheTableName_KeepsTheNameInsideItsLiteral()
    {
        // Act
        await _sut.GetColumnsAsync(Shop, "shop", "public", "o'brien");

        // Assert
        var reads = CatalogReads();
        reads.ShouldAllBe(r => r.Sql.Contains("'o''brien'"));
        reads.ShouldAllBe(r => !r.Sql.Contains("'o'brien'"));
    }

    [Fact]
    public async Task SchemaRead_WhileTheOpenTransactionIsAborted_SaysToRollItBack()
    {
        // Arrange
        await _sut.BeginTransactionAsync(Shop);
        _js.Module.InvokeAsync<JsonElement>("readInSavepoint", Arg.Any<CancellationToken>(), Arg.Any<object?[]?>())
            .Returns<ValueTask<JsonElement>>(_ => throw new JSException(
                "current transaction is aborted, commands ignored until end of transaction block\nError: at readInSavepoint (pglite-interop.js:52:13)"));

        // Act
        var failure = await Should.ThrowAsync<InvalidOperationException>(() => _sut.GetTablesAsync(Shop, "shop"));

        // Assert
        failure.Message.ShouldBe(PGliteProvider.AbortedTransactionReadMessage);
    }
}
