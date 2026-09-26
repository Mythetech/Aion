using Aion.Components.Connections;
using Aion.Components.Connections.Commands;
using Aion.Components.Querying;
using Aion.Components.Settings.Domains;
using Aion.Components.Theme;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.Connections;

public class ConnectionPanelTests : TestContext
{
    private const string Database = "sample_store";
    private static readonly TableInfo Products = new("", "products");

    private readonly IDatabaseProvider _provider;

    public ConnectionPanelTests()
    {
        Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;

        _provider = Substitute.For<IDatabaseProvider, IDatabaseIndexProvider>();
        _provider.DatabaseType.Returns(DatabaseType.WasmSQLite);
        _provider.SystemSchemas.Returns([]);
        _provider.GetColumnsAsync(default!, default!, default!, default!).ReturnsForAnyArgs(_ => new List<ColumnInfo>());
        ((IDatabaseIndexProvider)_provider).GetIndexesAsync(default!, default!).ReturnsForAnyArgs(_ => new List<IndexInfo>());

        var factory = Substitute.For<IDatabaseProviderFactory>();
        factory.GetProvider(Arg.Any<DatabaseType>()).Returns(_provider);

        var bus = Substitute.For<IMessageBus>();
        Services.AddSingleton(bus);
        Services.AddSingleton(new ConnectionState(Substitute.For<IConnectionService>(), factory, bus, NullLogger<ConnectionState>.Instance));
        Services.AddSingleton(new QueryState(bus, Substitute.For<IQuerySaveService>()));
        Services.AddSingleton(new BrowserSettings());
    }

    private static List<ColumnInfo> ProductColumns() =>
    [
        new() { Name = "id", DataType = "INTEGER", IsPrimaryKey = true },
        new() { Name = "name", DataType = "TEXT" },
        new()
        {
            Name = "category_id", DataType = "INTEGER",
            ForeignKey = new ForeignKeyInfo { ColumnName = "category_id", ReferencedTable = "categories", ReferencedColumn = "id" }
        },
        new() { Name = "description", DataType = "VARCHAR(255)", IsNullable = true },
        new() { Name = "stock_quantity", DataType = "INTEGER", DefaultValue = "0" }
    ];

    private static ConnectionModel ConnectionWithLoadedColumns(List<ColumnInfo> columns)
    {
        var database = new DatabaseModel { Name = Database, Tables = [Products], TablesLoaded = true };
        database.TableColumns[Products.DisplayName] = columns;
        database.LoadedColumnTables.Add(Products.DisplayName);

        return new ConnectionModel
        {
            Name = Database,
            Type = DatabaseType.WasmSQLite,
            Active = true,
            Databases = [database]
        };
    }

    private IRenderedComponent<ConnectionPanel> Render(ConnectionModel connection) =>
        RenderComponent<ConnectionPanel>(p => p.Add(x => x.Connection, connection));

    private static IRenderedComponent<MudTreeViewItem<string>> ColumnItem(IRenderedComponent<ConnectionPanel> cut, string column) =>
        cut.FindComponents<MudTreeViewItem<string>>()
            .Single(item => item.FindAll(".column-row").Count == 1 && item.Find(".column-row .tree-row-name").TextContent == column);

    [Fact]
    public void ColumnRow_ShowsNameAndShortTypeOnOneLine()
    {
        var cut = Render(ConnectionWithLoadedColumns(ProductColumns()));

        var row = ColumnItem(cut, "description").Find(".column-row");

        row.QuerySelector(".tree-row-name")!.TextContent.ShouldBe("description");
        row.QuerySelector(".column-type")!.TextContent.ShouldBe("varchar(255)?");
    }

    [Fact]
    public void ColumnRow_MarksOnlyNullableColumnsWithAQuietSuffix()
    {
        var cut = Render(ConnectionWithLoadedColumns(ProductColumns()));

        ColumnItem(cut, "description").FindAll(".column-nullable").Count.ShouldBe(1);
        ColumnItem(cut, "name").FindAll(".column-nullable").ShouldBeEmpty();
    }

    [Fact]
    public void ColumnRow_HoverTextDescribesTheWholeColumn()
    {
        var cut = Render(ConnectionWithLoadedColumns(ProductColumns()));

        ColumnItem(cut, "stock_quantity").Find(".column-row").GetAttribute("title")
            .ShouldBe("INTEGER · NOT NULL · default 0");
    }

    [Fact]
    public void ColumnRow_PicksKeyIconForPrimaryKeyLinkForForeignKeyAndColumnOtherwise()
    {
        var cut = Render(ConnectionWithLoadedColumns(ProductColumns()));

        ColumnItem(cut, "id").Instance.Icon.ShouldBe(AionIcons.PrimaryKey);
        ColumnItem(cut, "category_id").Instance.Icon.ShouldBe(AionIcons.ForeignKey);
        ColumnItem(cut, "name").Instance.Icon.ShouldBe(AionIcons.Column);
    }

    private static ConnectionModel ConnectionWithTables(params TableInfo[] tables) => new()
    {
        Name = Database,
        Type = DatabaseType.WasmSQLite,
        Active = true,
        Databases = [new DatabaseModel { Name = Database, Tables = [.. tables], TablesLoaded = true }]
    };

    private static IRenderedComponent<MudTreeViewItem<string>> TableItem(IRenderedComponent<ConnectionPanel> cut, TableInfo table) =>
        cut.FindComponents<MudTreeViewItem<string>>().Single(item => item.Instance.Value == $"{Database}|{table.DisplayName}");

    private static IRenderedComponent<MudTreeViewItem<string>> GroupItem(IRenderedComponent<ConnectionPanel> cut, string label) =>
        cut.FindComponents<MudTreeViewItem<string>>()
            .Single(item => item.FindAll(".tree-group-label").Count == 1 && item.Find(".tree-group-label").TextContent == label);

    private static Task ToggleAsync(IRenderedComponent<MudTreeViewItem<string>> item) =>
        item.Find(".mud-treeview-item-arrow button").ClickAsync(new());

    [Fact]
    public async Task ExpandingATable_LoadsItsColumnsDirectlyUnderIt()
    {
        _provider.GetColumnsAsync(Arg.Any<string>(), Database, "", "products").Returns(ProductColumns());
        var cut = Render(ConnectionWithTables(Products));

        await ToggleAsync(TableItem(cut, Products));

        cut.WaitForAssertion(() => TableItem(cut, Products).FindAll(".column-row .tree-row-name")
            .Select(name => name.TextContent)
            .ShouldBe(["id", "name", "category_id", "description", "stock_quantity"]));
        await _provider.Received(1).GetColumnsAsync(Arg.Any<string>(), Database, "", "products");
        cut.FindComponents<MudTreeViewItem<string>>().ShouldNotContain(item => item.Instance.Text == "Columns");
    }

    [Fact]
    public void TableBeforeItIsExpanded_DoesNotQueryColumns()
    {
        Render(ConnectionWithTables(Products));

        _provider.DidNotReceiveWithAnyArgs().GetColumnsAsync(default!, default!, default!, default!);
    }

    [Fact]
    public void TableWithNoColumns_SaysSoInsteadOfSpinning()
    {
        var connection = ConnectionWithLoadedColumns([]);
        connection.Databases[0].IndexesLoaded = true;

        var cut = Render(connection);

        var table = TableItem(cut, Products);
        table.Find(".tree-empty").TextContent.ShouldBe("No columns");
        table.FindAll(".mud-progress-circular").ShouldBeEmpty();
    }

    [Fact]
    public void ForeignKeysGroup_CountsAndListsTheTablesForeignKeyColumns()
    {
        var cut = Render(ConnectionWithLoadedColumns(ProductColumns()));

        var group = GroupItem(cut, "Foreign keys");
        group.Find(".tree-count").TextContent.ShouldBe("1");
        group.FindAll(".tree-row-name").Select(name => name.TextContent).ShouldBe(["category_id"]);
        group.Find(".tree-detail").TextContent.ShouldBe("categories.id");
    }

    [Fact]
    public void IndexesGroup_WithIndexesLoaded_CountsAndListsOnlyThisTablesIndexes()
    {
        var connection = ConnectionWithLoadedColumns(ProductColumns());
        connection.Databases[0].Indexes =
        [
            new IndexInfo("", "", "products", "idx_products_category", false, false, ["category_id"]),
            new IndexInfo("", "", "orders", "idx_orders_customer", false, false, ["customer_id"])
        ];
        connection.Databases[0].IndexesLoaded = true;

        var cut = Render(connection);

        var group = GroupItem(cut, "Indexes");
        group.Find(".tree-count").TextContent.ShouldBe("1");
        group.FindAll(".tree-row-name").Select(name => name.TextContent).ShouldBe(["idx_products_category"]);
    }

    [Fact]
    public async Task IndexesGroup_LeavesCountOutUntilExpandedThenLoadsIndexes()
    {
        ((IDatabaseIndexProvider)_provider).GetIndexesAsync(Arg.Any<string>(), Database)
            .Returns([new IndexInfo("", "", "products", "idx_products_category", false, false, ["category_id"])]);
        var cut = Render(ConnectionWithLoadedColumns(ProductColumns()));
        GroupItem(cut, "Indexes").FindAll(".tree-count").ShouldBeEmpty();

        await ToggleAsync(GroupItem(cut, "Indexes"));

        cut.WaitForAssertion(() => GroupItem(cut, "Indexes").Find(".tree-count").TextContent.ShouldBe("1"));
    }

    [Fact]
    public void TreeValues_StayUniqueWhenColumnsShareNamesWithGroups()
    {
        var columns = ProductColumns();
        columns.Add(new ColumnInfo { Name = "indexes", DataType = "TEXT" });
        columns.Add(new ColumnInfo { Name = "foreign-keys", DataType = "TEXT" });

        var cut = Render(ConnectionWithLoadedColumns(columns));

        cut.FindComponents<MudTreeViewItem<string>>().Select(item => item.Instance.Value).ShouldBeUnique();
    }

    [Fact]
    public async Task ExpandTableCommand_ExpandsTheTableAndLoadsItsColumns()
    {
        _provider.GetColumnsAsync(Arg.Any<string>(), Database, "", "products").Returns(ProductColumns());
        var connection = ConnectionWithTables(Products);
        var cut = Render(connection);

        await cut.InvokeAsync(() => cut.Instance.Consume(new ExpandTable(connection, connection.Databases[0], Products.DisplayName)));

        cut.WaitForAssertion(() => TableItem(cut, Products).Instance.Expanded.ShouldBeTrue());
        TableItem(cut, Products).FindAll(".column-row").ShouldNotBeEmpty();
    }
}
