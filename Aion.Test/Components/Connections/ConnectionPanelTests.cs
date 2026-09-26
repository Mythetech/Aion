using Aion.Components.Connections;
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
            .Single(item => item.FindAll(".column-row").Count == 1 && item.Find(".column-name").TextContent == column);

    [Fact]
    public void ColumnRow_ShowsNameAndShortTypeOnOneLine()
    {
        var cut = Render(ConnectionWithLoadedColumns(ProductColumns()));

        var row = ColumnItem(cut, "description").Find(".column-row");

        row.QuerySelector(".column-name")!.TextContent.ShouldBe("description");
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
}
