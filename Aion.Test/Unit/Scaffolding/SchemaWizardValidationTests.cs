using Aion.Components.Scaffolding;
using Aion.Contracts.Database;
using Shouldly;

namespace Aion.Test.Unit.Scaffolding;

public class SchemaWizardValidationTests
{
    private static ColumnDefinitionModel Column(string name, string type = "integer", bool primaryKey = false) =>
        new() { Name = name, DataType = type, IsPrimaryKey = primaryKey };

    private static TableDefinitionModel Table(string name, params ColumnDefinitionModel[] columns) =>
        new() { Name = name, Columns = columns.ToList() };

    private static SchemaWizardModel Model(DatabaseType engine, params TableDefinitionModel[] tables) => new()
    {
        DatabaseName = "shop",
        EngineType = engine,
        Tables = tables.ToList()
    };

    private static SchemaWizardModel ValidModel(DatabaseType engine = DatabaseType.WasmPostgreSQL) =>
        Model(engine, Table("orders", Column("id", primaryKey: true), Column("total", "numeric")));

    private static IReadOnlyList<string> Problems(SchemaWizardModel model, params string[] existing) =>
        SchemaWizardValidation.Problems(model, existing);

    [Fact]
    public void Problems_ForACompleteDefinition_IsEmpty()
    {
        Problems(ValidModel()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("", "Enter a database name")]
    [InlineData("   ", "Enter a database name")]
    [InlineData(" shop", "The database name can't start or end with a space")]
    [InlineData("shop/archive", "The database name can only use letters, numbers, spaces, hyphens and underscores")]
    [InlineData("shop;Mode=ReadOnly", "The database name can only use letters, numbers, spaces, hyphens and underscores")]
    public void Problems_NameWhatIsWrongWithTheDatabaseName(string name, string expected)
    {
        var model = ValidModel();
        model.DatabaseName = name;

        Problems(model).ShouldBe([expected]);
    }

    [Fact]
    public void Problems_WhenTheDatabaseNameIsTaken_SaysSo()
    {
        var model = ValidModel();
        model.DatabaseName = "Sample_Store";

        Problems(model, "sample_store").ShouldBe(["A database named \"Sample_Store\" already exists"]);
    }

    [Fact]
    public void Problems_WhenATableHasNoName_NamesItByPosition()
    {
        var model = Model(DatabaseType.WasmSQLite, Table("orders", Column("id")), Table(" ", Column("id")));

        Problems(model).ShouldBe(["Table 2: Enter a table name"]);
    }

    [Fact]
    public void Problems_WhenTwoTablesShareAName_SaysSo()
    {
        var model = Model(DatabaseType.WasmSQLite, Table("orders", Column("id")), Table("Orders", Column("id")));

        Problems(model).ShouldBe(["Orders: Another table is also named \"Orders\""]);
    }

    [Fact]
    public void Problems_WhenATableHasNoColumns_SaysSo()
    {
        var model = Model(DatabaseType.WasmSQLite, Table("orders"));

        Problems(model).ShouldBe(["orders: Add at least one column"]);
    }

    [Fact]
    public void Problems_ListEveryIncompleteColumn()
    {
        var model = Model(DatabaseType.WasmPostgreSQL,
            Table("orders", Column("id"), Column("", "text"), Column("total", "")));

        Problems(model).ShouldBe([
            "orders: Column 2 needs a name",
            "orders: Column \"total\" needs a data type"
        ]);
    }

    [Fact]
    public void Problems_WhenColumnsShareAName_ReportTheNameOnce()
    {
        var model = Model(DatabaseType.WasmPostgreSQL,
            Table("orders", Column("id"), Column("ID"), Column("id")));

        Problems(model).ShouldBe(["orders: More than one column is named \"id\""]);
    }

    [Fact]
    public void Problems_WhenMoreThanOneColumnIsThePrimaryKey_SaysOnlyOneCanBe()
    {
        var model = Model(DatabaseType.WasmPostgreSQL,
            Table("order_items", Column("order_id", primaryKey: true), Column("product_id", primaryKey: true)));

        Problems(model).ShouldBe(["order_items: Only one column can be the primary key"]);
    }

    [Fact]
    public void Problems_ForSqlitesReservedNames_SaySqliteReservesThem()
    {
        var model = Model(DatabaseType.WasmSQLite, Table("sqlite_stats", Column("id")));

        Problems(model).ShouldBe(["sqlite_stats: Names starting with sqlite_ are reserved by SQLite"]);
    }

    [Fact]
    public void Problems_ForSqlitesReservedNames_OnPostgres_AreFine()
    {
        var model = Model(DatabaseType.WasmPostgreSQL, Table("sqlite_stats", Column("id")));

        Problems(model).ShouldBeEmpty();
    }

    [Fact]
    public void Problems_ForNamesPostgresWouldShorten_SayHowLongTheyCanBe()
    {
        var longName = new string('a', 64);
        var model = Model(DatabaseType.WasmPostgreSQL, Table("orders", Column(longName)));

        Problems(model).ShouldBe([$"orders: Column \"{longName}\": PostgreSQL names can be at most 63 bytes long"]);
    }

    [Fact]
    public void Problems_ForNamesWithSurroundingSpaces_SaySo()
    {
        var model = Model(DatabaseType.WasmSQLite, Table("orders ", Column("id")));

        Problems(model).ShouldBe(["orders : Names can't start or end with a space"]);
    }

    [Fact]
    public void Problems_ReportTheDatabaseBeforeItsTables()
    {
        var model = Model(DatabaseType.WasmSQLite, Table("", Column("id")));
        model.DatabaseName = "";

        Problems(model).ShouldBe(["Enter a database name", "Table 1: Enter a table name"]);
    }
}
