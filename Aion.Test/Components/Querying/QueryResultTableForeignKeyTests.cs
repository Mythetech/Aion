using Aion.Components.Querying;
using Aion.Components.Settings.Domains;
using Aion.Components.Querying.Consumers;
using Aion.Components.RequestContextPanel;
using Aion.Components.RequestContextPanel.Commands;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.Querying;

public class QueryResultTableForeignKeyTests : TestContext
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly Guid _connectionId = Guid.NewGuid();

    public QueryResultTableForeignKeyTests()
    {
        Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(_bus);
        Services.AddSingleton(new ResultsSettings());
    }

    private static List<ColumnInfo> OrderColumns() =>
    [
        new() { Name = "id", IsPrimaryKey = true },
        new()
        {
            Name = "customer_id",
            ForeignKey = new ForeignKeyInfo
            {
                ColumnName = "customer_id", ReferencedSchema = "sales", ReferencedTable = "customers", ReferencedColumn = "id"
            }
        },
        new()
        {
            Name = "product_id",
            ForeignKey = new ForeignKeyInfo
            {
                ColumnName = "product_id", ReferencedSchema = "catalog", ReferencedTable = "products", ReferencedColumn = "id"
            }
        }
    ];

    private IRenderedComponent<QueryResultTable> RenderEditing() =>
        RenderComponent<QueryResultTable>(p => p
            .Add(x => x.Result, new QueryResult
            {
                Columns = ["id", "customer_id", "product_id"],
                Rows = [new Dictionary<string, object> { ["id"] = 1, ["customer_id"] = 7, ["product_id"] = 42 }]
            })
            .Add(x => x.QueryName, "Edit - sales.orders")
            .Add(x => x.EditMetadata, new QueryEditMetadata
            {
                SourceTable = "orders", SourceSchema = "sales", IsEditMode = true, ColumnMetadata = OrderColumns()
            })
            .Add(x => x.ConnectionId, _connectionId)
            .Add(x => x.DatabaseName, "shop"));

    private OpenForeignKeyView Published() =>
        (OpenForeignKeyView)_bus.ReceivedCalls()
            .Single(c => c.GetArguments()[0] is OpenForeignKeyView)
            .GetArguments()[0]!;

    [Fact]
    public async Task ForeignKeyCellInEditMode_OpensTheViewerWithTheReferencedSchema()
    {
        var cut = RenderEditing();

        await cut.Find("[aria-label='Show customer_id reference']").ClickAsync(new());

        var detail = Published().ForeignKeyDetail;
        detail.ReferencedSchema.ShouldBe("sales");
        detail.ReferencedTable.ShouldBe("customers");
        detail.ForeignKeyValue.ShouldBe(7);
        detail.ConnectionId.ShouldBe(_connectionId);
    }

    [Fact]
    public async Task ForeignKeyCell_OffersTheRowsOtherForeignKeysWithTheirOwnValues()
    {
        var cut = RenderEditing();

        await cut.Find("[aria-label='Show customer_id reference']").ClickAsync(new());

        Published().Choices!.Select(c => (c.SourceColumn, c.ReferencedTableDisplayName, c.ForeignKeyValue))
            .ShouldBe([("customer_id", "sales.customers", (object)7), ("product_id", "catalog.products", 42)]);
    }

    // Rows opened from one known table, such as its first 1000 rows, carry the table's columns without edit mode.
    private IRenderedComponent<QueryResultTable> RenderReadOnly() =>
        RenderComponent<QueryResultTable>(p => p
            .Add(x => x.Result, new QueryResult
            {
                Columns = ["id", "customer_id", "product_id"],
                Rows =
                [
                    new Dictionary<string, object> { ["id"] = 1, ["customer_id"] = 7, ["product_id"] = 42 },
                    new Dictionary<string, object> { ["id"] = 2, ["customer_id"] = null!, ["product_id"] = 43 }
                ]
            })
            .Add(x => x.QueryName, "First 1000 rows - sales.orders")
            .Add(x => x.EditMetadata, new QueryEditMetadata
            {
                SourceTable = "orders", SourceSchema = "sales", SourceDatabase = "shop", ConnectionId = _connectionId,
                ColumnMetadata = OrderColumns()
            })
            .Add(x => x.ConnectionId, _connectionId)
            .Add(x => x.DatabaseName, "shop"));

    [Fact]
    public async Task ForeignKeyCellInReadOnlyRows_OpensTheViewerWithTheRowsForeignKeys()
    {
        var cut = RenderReadOnly();

        await cut.Find("[aria-label='Show customer_id reference']").ClickAsync(new());

        var published = Published();
        published.ForeignKeyDetail.ReferencedTableDisplayName.ShouldBe("sales.customers");
        published.ForeignKeyDetail.ForeignKeyValue.ShouldBe(7);
        published.ForeignKeyDetail.ConnectionId.ShouldBe(_connectionId);
        published.Choices!.Select(c => (c.SourceColumn, c.ForeignKeyValue))
            .ShouldBe([("customer_id", (object)7), ("product_id", 42)]);
    }

    [Fact]
    public void ReadOnlyRows_LinkEveryForeignKeyValueButNulls()
    {
        var cut = RenderReadOnly();

        cut.FindAll(".cell-fk").Select(link => link.GetAttribute("aria-label"))
            .ShouldBe(["Show customer_id reference", "Show product_id reference", "Show product_id reference"]);
    }

    [Fact]
    public void RowsFromAnUnknownTable_HaveNoForeignKeyLinks()
    {
        var cut = RenderComponent<QueryResultTable>(p => p
            .Add(x => x.Result, new QueryResult
            {
                Columns = ["customer_id"],
                Rows = [new Dictionary<string, object> { ["customer_id"] = 7 }]
            })
            .Add(x => x.QueryName, "Query1")
            .Add(x => x.ConnectionId, _connectionId)
            .Add(x => x.DatabaseName, "shop"));

        cut.FindAll(".cell-fk").ShouldBeEmpty();
    }
}
