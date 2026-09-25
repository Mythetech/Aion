using Aion.Test.TestDoubles;
using Aion.Web.Providers;
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
