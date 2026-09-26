using Aion.Components.Connections;
using Aion.Components.Connections.Commands;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Settings.Domains;
using Aion.Components.Theme;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
    private readonly IDatabaseProvider _liteDbProvider = Substitute.For<IDatabaseProvider>();
    private readonly IConnectionService _connectionService = Substitute.For<IConnectionService>();
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly ConnectionState _connectionState;

    public ConnectionPanelTests()
    {
        Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;

        _provider = Substitute.For<IDatabaseProvider, IDatabaseIndexProvider, IDatabaseViewProvider>();
        _provider.DatabaseType.Returns(DatabaseType.WasmSQLite);
        _provider.SystemSchemas.Returns([]);
        _provider.GetColumnsAsync(default!, default!, default!, default!).ReturnsForAnyArgs(_ => new List<ColumnInfo>());
        ((IDatabaseIndexProvider)_provider).GetIndexesAsync(default!, default!).ReturnsForAnyArgs(_ => new List<IndexInfo>());
        Views.GetViewsAsync(default!, default!).ReturnsForAnyArgs(_ => new List<TableInfo>());
        _liteDbProvider.DatabaseType.Returns(DatabaseType.LiteDB);
        _liteDbProvider.SystemSchemas.Returns([]);

        var factory = Substitute.For<IDatabaseProviderFactory>();
        factory.GetProvider(Arg.Any<DatabaseType>()).Returns(_provider);
        factory.GetProvider(DatabaseType.LiteDB).Returns(_liteDbProvider);

        Services.AddSingleton(_bus);
        _connectionState = new ConnectionState(_connectionService, factory, _bus, NullLogger<ConnectionState>.Instance);
        Services.AddSingleton(_connectionState);
        Services.AddSingleton(new QueryState(_bus, Substitute.For<IQuerySaveService>()));
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
    public void Header_NamesTheEngineInsteadOfTheEnum()
    {
        var cut = Render(ConnectionWithLoadedColumns(ProductColumns()));

        cut.Find("h6").TextContent.ShouldBe("SQLite · in-browser");
    }

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
    public void TableRow_ShowsACountedRowCountBesideTheName()
    {
        var cut = Render(ConnectionWithTables(Products with { RowCount = TableRowCount.Exact(15) }));

        var count = TableItem(cut, Products).Find(".row-count");
        count.TextContent.ShouldBe("15 rows");
        count.GetAttribute("title").ShouldBe("15 rows, counted when the tables were listed");
    }

    [Fact]
    public void TableRow_MarksAnEstimatedRowCountAsAnEstimate()
    {
        var cut = Render(ConnectionWithTables(Products with { RowCount = TableRowCount.Estimated(1_234) }));

        var count = TableItem(cut, Products).Find(".row-count");
        count.TextContent.ShouldBe("~1.2k rows");
        count.GetAttribute("title").ShouldBe("About 1,234 rows, estimated from table statistics");
    }

    [Fact]
    public void TableRow_WithoutARowCount_LeavesTheSpaceEmpty()
    {
        var cut = Render(ConnectionWithTables(Products));

        TableItem(cut, Products).FindAll(".row-count").ShouldBeEmpty();
    }

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
        table.Find(".tree-status-empty").TextContent.ShouldBe("No columns");
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

    [Theory]
    [InlineData(new[] { "email" }, "unique index on (email)")]
    [InlineData(new string[0], "unique index")]
    public void IndexRow_HoverTextNamesItsKindAndColumnsWhenKnown(string[] columns, string expected)
    {
        // PGlite lists indexes without their columns.
        var connection = ConnectionWithLoadedColumns(ProductColumns());
        connection.Databases[0].Indexes = [new IndexInfo("", "", "products", "idx_products_email", true, false, columns)];
        connection.Databases[0].IndexesLoaded = true;

        var cut = Render(connection);

        GroupItem(cut, "Indexes").Find(".detail-row").GetAttribute("title").ShouldBe(expected);
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

    private static IRenderedComponent<MudTreeViewItem<string>> NamedItem(IRenderedComponent<ConnectionPanel> cut, string value) =>
        cut.FindComponents<MudTreeViewItem<string>>().Single(item => item.Instance.Value == value);

    // Database items manage their own open state, so read it from the item's collapse rather than its parameter.
    private static bool IsOpen(IRenderedComponent<MudTreeViewItem<string>> item) =>
        item.FindComponent<MudCollapse>().Instance.Expanded;

    private void TablesFail(string message) =>
        _connectionService.GetTablesAsync(Arg.Any<string>(), Database, DatabaseType.WasmSQLite)
            .Returns<List<TableInfo>>(_ => throw new InvalidOperationException(message));

    private static ConnectionModel ConnectionWithUnloadedDatabase() => new()
    {
        Name = Database,
        Type = DatabaseType.WasmSQLite,
        Active = true,
        Databases = [new DatabaseModel { Name = Database }]
    };

    [Fact]
    public async Task TablesThatFailToLoad_ShowTheErrorWithARetryInsteadOfSpinning()
    {
        TablesFail("SQLITE_ERROR: database disk image is malformed");
        var cut = Render(ConnectionWithUnloadedDatabase());

        await ToggleAsync(NamedItem(cut, $"{Database}/Tables"));

        cut.WaitForAssertion(() => NamedItem(cut, $"{Database}/Tables").Find(".tree-status-failed").TextContent
            .ShouldContain("database disk image is malformed"));
        NamedItem(cut, $"{Database}/Tables").FindAll(".mud-progress-circular").ShouldBeEmpty();
    }

    [Fact]
    public async Task RetryOnAFailedTablesLoad_LoadsThemAgain()
    {
        TablesFail("busy");
        var cut = Render(ConnectionWithUnloadedDatabase());
        await ToggleAsync(NamedItem(cut, $"{Database}/Tables"));
        cut.WaitForAssertion(() => cut.FindAll(".tree-status-failed").ShouldNotBeEmpty());
        _connectionService.GetTablesAsync(Arg.Any<string>(), Database, DatabaseType.WasmSQLite).Returns([Products]);

        await cut.Find(".tree-status-failed button").ClickAsync(new());

        cut.WaitForAssertion(() => TableItem(cut, Products).ShouldNotBeNull());
        cut.FindAll(".tree-status-failed").ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadErrors_ShowTheEnginesMessageWithoutTheDriverPrefix()
    {
        TablesFail("Worker error: SQLITE_ERROR: sqlite3 result code 1: no such table: sqlite_master2");
        var cut = Render(ConnectionWithUnloadedDatabase());

        await ToggleAsync(NamedItem(cut, $"{Database}/Tables"));

        cut.WaitForAssertion(() => NamedItem(cut, $"{Database}/Tables").Find(".tree-status-text").TextContent
            .ShouldBe("no such table: sqlite_master2"));
    }

    [Fact]
    public async Task ColumnsThatFailToLoad_ShowAnErrorRowUnderTheTable()
    {
        _provider.GetColumnsAsync(Arg.Any<string>(), Database, "", "products")
            .Returns<List<ColumnInfo>>(_ => throw new InvalidOperationException("near \"name\": syntax error"));
        var cut = Render(ConnectionWithTables(Products));

        await ToggleAsync(TableItem(cut, Products));

        cut.WaitForAssertion(() => TableItem(cut, Products).Find(".tree-status-failed").TextContent
            .ShouldContain("near \"name\": syntax error"));
        TableItem(cut, Products).Instance.Expanded.ShouldBeTrue();
    }

    [Fact]
    public async Task IndexesThatFailToLoad_ShowAnErrorRowInTheTablesIndexesGroup()
    {
        ((IDatabaseIndexProvider)_provider).GetIndexesAsync(Arg.Any<string>(), Database)
            .Returns<List<IndexInfo>>(_ => throw new InvalidOperationException("no such table: pragma_index_list"));
        var cut = Render(ConnectionWithLoadedColumns(ProductColumns()));

        await ToggleAsync(GroupItem(cut, "Indexes"));

        cut.WaitForAssertion(() => GroupItem(cut, "Indexes").Find(".tree-status-failed").TextContent
            .ShouldContain("no such table: pragma_index_list"));
    }

    [Fact]
    public async Task Indexes_AreListedUnderTheirTableWithoutADatabaseLevelNode()
    {
        ((IDatabaseIndexProvider)_provider).GetIndexesAsync(Arg.Any<string>(), Database).Returns(
        [
            new IndexInfo("", "", "products", "products_pkey", true, true, ["id"]),
            new IndexInfo("", "", "orders", "idx_orders_customer", false, false, ["customer_id"])
        ]);
        var cut = Render(ConnectionWithLoadedColumns(ProductColumns()));

        await ToggleAsync(GroupItem(cut, "Indexes"));

        cut.WaitForAssertion(() => GroupItem(cut, "Indexes").FindAll(".tree-row-name").Select(name => name.TextContent)
            .ShouldBe(["products_pkey"]));
        cut.FindComponents<MudTreeViewItem<string>>().ShouldNotContain(item => item.Instance.Value == $"{Database}/Indexes");
        cut.FindComponents<MudTreeViewItem<string>>().ShouldNotContain(item => item.Instance.Text == "Indexes");
    }

    [Fact]
    public void DatabaseWithNoTables_SaysSo()
    {
        var cut = Render(ConnectionWithTables());

        NamedItem(cut, $"{Database}/Tables").Find(".tree-status-empty").TextContent.ShouldBe("No tables");
    }

    [Fact]
    public void TableWithNoIndexes_CountsNone()
    {
        var connection = ConnectionWithLoadedColumns(ProductColumns());
        connection.Databases[0].IndexesLoaded = true;

        var cut = Render(connection);

        GroupItem(cut, "Indexes").Find(".tree-count").TextContent.ShouldBe("0");
    }

    [Fact]
    public void ConnectionWithNoDatabases_SaysSo()
    {
        var cut = Render(new ConnectionModel { Name = Database, Type = DatabaseType.WasmSQLite, Active = true });

        cut.Find(".tree-status-empty").TextContent.ShouldBe("No databases");
    }

    [Fact]
    public void UnreachableConnectionWithNoDatabases_ShowsTheConnectionError()
    {
        var cut = Render(new ConnectionModel
        {
            Name = Database,
            Type = DatabaseType.WasmSQLite,
            Active = false,
            HealthStatus = ConnectionHealthStatus.Unhealthy,
            LastError = "Connection refused"
        });

        cut.Find(".tree-status-failed").TextContent.ShouldContain("Connection refused");
    }

    private static readonly TableInfo Customers = new("", "customers");
    private static readonly TableInfo Orders = new("", "orders");
    private static readonly TableInfo OrderItems = new("", "order_items");

    private static List<string> ListedTables(IRenderedComponent<ConnectionPanel> cut) =>
        cut.FindComponents<MudTreeViewItem<string>>()
            .Select(item => item.Instance.Value ?? "")
            .Where(value => value.StartsWith($"{Database}|") && !value.Contains('/'))
            .Select(value => value[(Database.Length + 1)..])
            .ToList();

    private static Task FilterAsync(IRenderedComponent<ConnectionPanel> cut, string text) =>
        cut.Find(".schema-filter input").InputAsync(new ChangeEventArgs { Value = text });

    private static List<string> ColumnNames(IRenderedComponent<MudTreeViewItem<string>> table) =>
        table.FindAll(".column-row .tree-row-name").Select(name => name.TextContent).ToList();

    [Fact]
    public void Filter_IsNamedForScreenReaders()
    {
        var cut = Render(ConnectionWithTables(Customers));

        cut.Find(".schema-filter input").GetAttribute("aria-label").ShouldBe("Filter tables and columns");
    }

    [Fact]
    public async Task Filter_NarrowsTablesByNameIgnoringCase()
    {
        var cut = Render(ConnectionWithTables(Customers, Orders, OrderItems));

        await FilterAsync(cut, "ORDER");

        cut.WaitForAssertion(() => ListedTables(cut).ShouldBe(["orders", "order_items"]));
    }

    [Fact]
    public async Task Filter_OpensATableMatchedByALoadedColumnAndListsOnlyThatColumn()
    {
        var connection = ConnectionWithLoadedColumns(ProductColumns());
        connection.Databases[0].Tables.Add(Customers);
        var cut = Render(connection);

        await FilterAsync(cut, "STOCK");

        cut.WaitForAssertion(() => ListedTables(cut).ShouldBe(["products"]));
        TableItem(cut, Products).Instance.Expanded.ShouldBeTrue();
        ColumnNames(TableItem(cut, Products)).ShouldBe(["stock_quantity"]);
        TableItem(cut, Products).FindAll(".tree-group-label").ShouldBeEmpty();
    }

    [Fact]
    public async Task Filter_KeepsEveryColumnOfATableMatchedByName()
    {
        var cut = Render(ConnectionWithLoadedColumns(ProductColumns()));

        await FilterAsync(cut, "prod");

        cut.WaitForAssertion(() => ListedTables(cut).ShouldBe(["products"]));
        ColumnNames(TableItem(cut, Products)).ShouldBe(["id", "name", "category_id", "description", "stock_quantity"]);
    }

    [Fact]
    public async Task ClearingTheFilter_RestoresEveryTableAndTheExpansionTheUserHad()
    {
        _provider.GetColumnsAsync(Arg.Any<string>(), Database, "", "orders")
            .Returns([new ColumnInfo { Name = "id", DataType = "INTEGER" }]);
        var connection = ConnectionWithLoadedColumns(ProductColumns());
        connection.Databases[0].Tables.Add(Orders);
        var cut = Render(connection);
        await ToggleAsync(TableItem(cut, Orders));
        cut.WaitForAssertion(() => TableItem(cut, Orders).Instance.Expanded.ShouldBeTrue());

        await FilterAsync(cut, "stock");
        cut.WaitForAssertion(() => ListedTables(cut).ShouldBe(["products"]));
        await FilterAsync(cut, "");

        cut.WaitForAssertion(() => ListedTables(cut).ShouldBe(["products", "orders"]));
        TableItem(cut, Orders).Instance.Expanded.ShouldBeTrue();
        TableItem(cut, Products).Instance.Expanded.ShouldBeFalse();
        ColumnNames(TableItem(cut, Products)).Count.ShouldBe(5);
    }

    [Fact]
    public async Task EscapeInTheFilter_ClearsIt()
    {
        var cut = Render(ConnectionWithTables(Customers, Orders));
        await FilterAsync(cut, "cust");
        cut.WaitForAssertion(() => ListedTables(cut).ShouldBe(["customers"]));

        await cut.Find(".schema-filter input").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        cut.WaitForAssertion(() => ListedTables(cut).ShouldBe(["customers", "orders"]));
        cut.Find(".schema-filter input").GetAttribute("value").ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task FilterWithoutMatches_SaysSoInsteadOfNoTables()
    {
        var cut = Render(ConnectionWithTables(Customers, Orders));

        await FilterAsync(cut, "zzz");

        cut.WaitForAssertion(() => NamedItem(cut, $"{Database}/Tables").Find(".tree-status-empty").TextContent
            .ShouldBe("No tables match"));
    }

    [Fact]
    public async Task Filter_OnAConnectionWithOneDatabase_LoadsItsTablesAndOpensThem()
    {
        _connectionService.GetTablesAsync(Arg.Any<string>(), Database, DatabaseType.WasmSQLite).Returns([Products, Customers]);
        var cut = Render(ConnectionWithUnloadedDatabase());

        await FilterAsync(cut, "prod");

        cut.WaitForAssertion(() => ListedTables(cut).ShouldBe(["products"]));
        IsOpen(NamedItem(cut, Database)).ShouldBeTrue();
        IsOpen(NamedItem(cut, $"{Database}/Tables")).ShouldBeTrue();
    }

    [Fact]
    public async Task ClearingTheFilter_ClosesTheDatabaseItOpened()
    {
        _connectionService.GetTablesAsync(Arg.Any<string>(), Database, DatabaseType.WasmSQLite).Returns([Products]);
        var cut = Render(ConnectionWithUnloadedDatabase());
        await FilterAsync(cut, "prod");
        cut.WaitForAssertion(() => IsOpen(NamedItem(cut, Database)).ShouldBeTrue());

        await FilterAsync(cut, "");

        cut.WaitForAssertion(() => IsOpen(NamedItem(cut, Database)).ShouldBeFalse());
    }

    private IDatabaseViewProvider Views => (IDatabaseViewProvider)_provider;

    private static readonly TableInfo ActiveCustomers = new("", "active_customers");
    private static readonly TableInfo BigOrders = new("", "big_orders");

    private static ConnectionModel ConnectionWithViews(params TableInfo[] views)
    {
        var connection = ConnectionWithTables(Products);
        connection.Databases[0].Views = [.. views];
        connection.Databases[0].ViewsState = SchemaLoadState.Loaded;
        return connection;
    }

    private static IRenderedComponent<MudTreeViewItem<string>> ViewItem(IRenderedComponent<ConnectionPanel> cut, TableInfo view) =>
        NamedItem(cut, $"{Database}/Views|{view.DisplayName}");

    private static List<string> ListedViews(IRenderedComponent<ConnectionPanel> cut) =>
        cut.FindComponents<MudTreeViewItem<string>>()
            .Select(item => item.Instance.Value ?? "")
            .Where(value => value.StartsWith($"{Database}/Views|") && !value[$"{Database}/Views|".Length..].Contains('/'))
            .Select(value => value[$"{Database}/Views|".Length..])
            .ToList();

    [Fact]
    public void ViewsGroup_ListsTheViewsAndCountsThem()
    {
        var cut = Render(ConnectionWithViews(ActiveCustomers, BigOrders));

        ListedViews(cut).ShouldBe(["active_customers", "big_orders"]);
        NamedItem(cut, $"{Database}/Views").Instance.EndText.ShouldBe("2");
    }

    [Fact]
    public void DatabaseWithNoViews_SaysSo()
    {
        var cut = Render(ConnectionWithViews());

        NamedItem(cut, $"{Database}/Views").Find(".tree-status-empty").TextContent.ShouldBe("No views");
    }

    [Fact]
    public async Task ExpandingTheViewsGroup_ListsTheViews()
    {
        Views.GetViewsAsync(Arg.Any<string>(), Database).Returns([ActiveCustomers]);
        var cut = Render(ConnectionWithTables(Products));

        await ToggleAsync(NamedItem(cut, $"{Database}/Views"));

        cut.WaitForAssertion(() => ListedViews(cut).ShouldBe(["active_customers"]));
    }

    [Fact]
    public async Task ExpandingTables_AlsoListsTheViewsSoTheirCountShows()
    {
        _connectionService.GetTablesAsync(Arg.Any<string>(), Database, DatabaseType.WasmSQLite).Returns([Products]);
        Views.GetViewsAsync(Arg.Any<string>(), Database).Returns([ActiveCustomers]);
        var cut = Render(ConnectionWithUnloadedDatabase());

        await ToggleAsync(NamedItem(cut, $"{Database}/Tables"));

        cut.WaitForAssertion(() => NamedItem(cut, $"{Database}/Views").Instance.EndText.ShouldBe("1"));
    }

    [Fact]
    public async Task ExpandingAView_ListsItsColumnsWithoutTableGroups()
    {
        _provider.GetColumnsAsync(Arg.Any<string>(), Database, "", "active_customers")
            .Returns([new ColumnInfo { Name = "email", DataType = "TEXT" }]);
        var cut = Render(ConnectionWithViews(ActiveCustomers));

        await ToggleAsync(ViewItem(cut, ActiveCustomers));

        cut.WaitForAssertion(() => ColumnNames(ViewItem(cut, ActiveCustomers)).ShouldBe(["email"]));
        ViewItem(cut, ActiveCustomers).FindAll(".tree-group-label").ShouldBeEmpty();
    }

    [Fact]
    public async Task ViewMenu_SelectsTheFirstRowsOfTheView()
    {
        var popovers = RenderComponent<MudPopoverProvider>();
        var connection = ConnectionWithViews(ActiveCustomers);
        var cut = Render(connection);

        await ViewItem(cut, ActiveCustomers).Find("button[aria-label='active_customers actions']").ClickAsync(new());
        await popovers.WaitForElement(".mud-menu-item").ClickAsync(new());

        await _bus.Received(1).PublishAsync(new OpenTableRows(connection.Id, Database, "", "active_customers"));
    }

    [Fact]
    public async Task Filter_NarrowsViewsByNameToo()
    {
        var cut = Render(ConnectionWithViews(ActiveCustomers, BigOrders));

        await FilterAsync(cut, "ACTIVE");

        cut.WaitForAssertion(() => ListedViews(cut).ShouldBe(["active_customers"]));
        NamedItem(cut, $"{Database}/Views").Instance.EndText.ShouldBe("1 of 2");
    }

    [Fact]
    public void EngineWithoutViews_HasNoViewsGroup()
    {
        var connection = ConnectionWithTables(Products);
        connection.Type = DatabaseType.LiteDB;

        var cut = Render(connection);

        cut.FindComponents<MudTreeViewItem<string>>().ShouldNotContain(item => item.Instance.Value == $"{Database}/Views");
    }

    [Fact]
    public async Task ExpandedDatabase_StaysExpandedWhenAnotherDatabaseIsListedBeforeIt()
    {
        var connection = new ConnectionModel
        {
            Name = "server",
            Type = DatabaseType.WasmSQLite,
            Active = true,
            Databases = [new DatabaseModel { Name = "billing" }, new DatabaseModel { Name = "shop" }]
        };
        _connectionState.Connections.Add(connection);
        var cut = Render(connection);
        await ToggleAsync(NamedItem(cut, "shop"));
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), DatabaseType.WasmSQLite).Returns(["archive", "billing", "shop"]);

        await cut.InvokeAsync(() => _connectionState.RefreshDatabaseAsync(connection));
        cut.Render();

        IsOpen(NamedItem(cut, "shop")).ShouldBeTrue();
        IsOpen(NamedItem(cut, "archive")).ShouldBeFalse();
    }
}
