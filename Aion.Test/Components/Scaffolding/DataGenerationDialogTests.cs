using Aion.Components.Scaffolding.DataGeneration;
using Aion.Contracts.Database;
using Aion.Contracts.Database.Dialects;
using Aion.Contracts.Queries;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.Scaffolding;

public class DataGenerationDialogTests : TestContext
{
    private readonly IDatabaseProvider _provider = Substitute.For<IDatabaseProvider, ISqlDialectProvider>();
    private readonly TransactionInfo _transaction = new();

    public DataGenerationDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();

        _provider.DatabaseType.Returns(DatabaseType.SQLServer);
        ((ISqlDialectProvider)_provider).Dialect.Returns(SqlServerDialect.Instance);
        _provider.BeginTransactionAsync("conn").Returns(_transaction);
        _provider.ExecuteInTransactionAsync("conn", Arg.Any<string>(), _transaction.Id, Arg.Any<CancellationToken>())
            .Returns(new QueryResult());
    }

    private async Task<IRenderedComponent<MudDialogProvider>> ShowAsync(IDatabaseProvider provider, params ColumnInfo[] columns)
    {
        var host = RenderComponent<MudDialogProvider>();
        var dialogs = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<DataGenerationDialog>
        {
            { x => x.TableName, "orders" },
            { x => x.Schema, "dbo" },
            { x => x.Database, "shop" },
            { x => x.Columns, columns.ToList() },
            { x => x.Provider, provider },
            { x => x.ConnectionString, "conn" }
        };
        await host.InvokeAsync(() => dialogs.ShowAsync<DataGenerationDialog>("Generate Data", parameters));
        return host;
    }

    private static AngleSharp.Dom.IElement GenerateButton(IRenderedComponent<MudDialogProvider> host) =>
        host.FindAll("button").Single(b => b.TextContent.Trim() == "Generate");

    [Fact]
    public async Task IdentityColumns_SayTheDatabaseFillsThem()
    {
        var host = await ShowAsync(_provider,
            new ColumnInfo { Name = "id", DataType = "int", IsIdentity = true, IsPrimaryKey = true },
            new ColumnInfo { Name = "notes", DataType = "nvarchar", IsNullable = true });

        host.Markup.ShouldContain("Filled by the database (identity)");
    }

    [Fact]
    public async Task WhenAColumnNeedsAGenerator_ListsItAndDisablesGenerate()
    {
        var host = await ShowAsync(_provider, new ColumnInfo { Name = "location", DataType = "geography" });

        host.FindAll(".generate-problems li").Select(li => li.TextContent.Trim())
            .ShouldBe(["Pick a generator for \"location\": it can't be NULL and has no default"]);
        GenerateButton(host).HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task ForAnEngineWithoutSql_SaysGeneratingIsNotSupported()
    {
        var liteDb = Substitute.For<IDatabaseProvider>();
        liteDb.DatabaseType.Returns(DatabaseType.LiteDB);

        var host = await ShowAsync(liteDb, new ColumnInfo { Name = "notes", DataType = "string", IsNullable = true });

        host.Markup.ShouldContain("Generating data is not supported for LiteDB connections.");
        GenerateButton(host).HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task WhenTheEngineRejectsTheRows_ShowsItsMessage()
    {
        var failure = new QueryResult();
        failure.SetError(new QueryError { Raw = "Msg 2628", Message = "String or binary data would be truncated.", Title = "Error" });
        _provider.ExecuteInTransactionAsync("conn", Arg.Is<string>(s => s.StartsWith("INSERT")), _transaction.Id, Arg.Any<CancellationToken>())
            .Returns(failure);
        var host = await ShowAsync(_provider, new ColumnInfo { Name = "notes", DataType = "nvarchar", IsNullable = true });

        await GenerateButton(host).ClickAsync(new MouseEventArgs());

        host.WaitForAssertion(() => host.Find(".mud-alert-text-error").TextContent.Trim()
            .ShouldBe("Inserting rows 1 to 100 failed, so no rows were added: String or binary data would be truncated."));
        host.FindAll(".mud-alert-text-success").ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenTheRowsAreAdded_SaysHowMany()
    {
        var host = await ShowAsync(_provider, new ColumnInfo { Name = "notes", DataType = "nvarchar", IsNullable = true });

        await GenerateButton(host).ClickAsync(new MouseEventArgs());

        host.WaitForAssertion(() => host.Find(".mud-alert-text-success").TextContent.Trim().ShouldBe("Added 100 rows to orders"));
        await _provider.Received(1).CommitTransactionAsync("conn", _transaction.Id);
    }
}
