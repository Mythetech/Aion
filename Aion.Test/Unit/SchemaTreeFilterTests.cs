using Aion.Components.Connections;
using Aion.Contracts.Database;
using Shouldly;

namespace Aion.Test.Unit;

public class SchemaTreeFilterTests
{
    private static readonly TableInfo Customers = new("public", "customers");
    private static readonly TableInfo Orders = new("public", "orders");
    private static readonly TableInfo OrderItems = new("public", "order_items");

    private static DatabaseModel Database(params TableInfo[] tables) => new() { Name = "shop", Tables = [.. tables], TablesLoaded = true };

    private static void LoadColumns(DatabaseModel database, TableInfo table, params string[] columns)
    {
        database.TableColumns[table.DisplayName] = columns.Select(name => new ColumnInfo { Name = name, DataType = "text" }).ToList();
        database.LoadedColumnTables.Add(table.DisplayName);
    }

    private static List<string> Names(IEnumerable<SchemaTreeMatch> matches) => matches.Select(m => m.Table.DisplayName).ToList();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithoutText_ListsEveryTableWithAllItsColumns(string? text)
    {
        var database = Database(Customers, Orders);
        LoadColumns(database, Orders, "id", "customer_id");

        var filter = new SchemaTreeFilter(text);
        var matches = filter.Apply(database.Tables, database);

        filter.IsActive.ShouldBeFalse();
        Names(matches).ShouldBe(["public.customers", "public.orders"]);
        matches.ShouldAllBe(m => m.MatchingColumns == null);
    }

    [Fact]
    public void MatchesTableNamesIgnoringCase()
    {
        var database = Database(Customers, Orders, OrderItems);

        var matches = new SchemaTreeFilter("ORDER").Apply(database.Tables, database);

        Names(matches).ShouldBe(["public.orders", "public.order_items"]);
    }

    [Fact]
    public void IgnoresSpaceAroundTheText()
    {
        var database = Database(Customers, Orders);

        var matches = new SchemaTreeFilter("  cust ").Apply(database.Tables, database);

        Names(matches).ShouldBe(["public.customers"]);
    }

    [Fact]
    public void MatchesTheSchemaAsShownInTheTree()
    {
        var database = Database(Customers, new TableInfo("sales", "invoices"));

        var matches = new SchemaTreeFilter("sales.").Apply(database.Tables, database);

        Names(matches).ShouldBe(["sales.invoices"]);
    }

    [Fact]
    public void TableMatchedByName_KeepsAllItsColumns()
    {
        var database = Database(Orders);
        LoadColumns(database, Orders, "id", "order_date");

        var match = new SchemaTreeFilter("order").Apply(database.Tables, database).Single();

        match.MatchingColumns.ShouldBeNull();
        match.MatchedByColumns.ShouldBeFalse();
    }

    [Fact]
    public void TableMatchedOnlyByLoadedColumns_KeepsJustThoseColumns()
    {
        var database = Database(Customers, Orders);
        LoadColumns(database, Customers, "id", "email", "email_verified", "name");

        var match = new SchemaTreeFilter("EMAIL").Apply(database.Tables, database).Single();

        match.Table.ShouldBe(Customers);
        match.MatchedByColumns.ShouldBeTrue();
        match.MatchingColumns!.Select(c => c.Name).ShouldBe(["email", "email_verified"]);
    }

    [Fact]
    public void TableWhoseColumnsAreNotLoaded_IsOnlyMatchedByName()
    {
        var database = Database(Customers);
        database.TableColumns[Customers.DisplayName] = [new ColumnInfo { Name = "email" }];

        var matches = new SchemaTreeFilter("email").Apply(database.Tables, database);

        matches.ShouldBeEmpty();
    }
}
