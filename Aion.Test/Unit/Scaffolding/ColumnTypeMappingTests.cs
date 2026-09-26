using Aion.Components.Scaffolding;
using Aion.Contracts.Database;
using Shouldly;

namespace Aion.Test.Unit.Scaffolding;

public class ColumnTypeMappingTests
{
    private static readonly string[] SqliteTypes = ["INTEGER", "TEXT", "REAL", "BLOB", "NUMERIC"];

    private static readonly string[] PgliteTypes =
    [
        "integer", "bigint", "smallint", "serial", "bigserial", "boolean", "text", "varchar", "char", "numeric",
        "decimal", "real", "double precision", "date", "timestamp", "timestamptz", "time", "interval", "uuid",
        "json", "jsonb", "bytea"
    ];

    [Theory]
    [InlineData("INTEGER", "integer")]
    [InlineData("TEXT", "text")]
    [InlineData("REAL", "double precision")]
    [InlineData("BLOB", "bytea")]
    [InlineData("NUMERIC", "numeric")]
    public void Map_FromSqliteToPostgres_PicksTheEquivalentType(string sqlite, string expected)
    {
        ColumnTypeMapping.Map(sqlite, DatabaseType.WasmSQLite, DatabaseType.WasmPostgreSQL, PgliteTypes).ShouldBe(expected);
    }

    [Theory]
    [InlineData("integer", "INTEGER")]
    [InlineData("bigserial", "INTEGER")]
    [InlineData("boolean", "INTEGER")]
    [InlineData("varchar", "TEXT")]
    [InlineData("uuid", "TEXT")]
    [InlineData("jsonb", "TEXT")]
    [InlineData("timestamptz", "TEXT")]
    [InlineData("interval", "TEXT")]
    [InlineData("decimal", "NUMERIC")]
    [InlineData("double precision", "REAL")]
    [InlineData("bytea", "BLOB")]
    public void Map_FromPostgresToSqlite_PicksTheTypeSqliteStoresItAs(string postgres, string expected)
    {
        ColumnTypeMapping.Map(postgres, DatabaseType.WasmPostgreSQL, DatabaseType.WasmSQLite, SqliteTypes).ShouldBe(expected);
    }

    [Fact]
    public void Map_WithinOneEngine_KeepsTheType()
    {
        ColumnTypeMapping.Map("bigint", DatabaseType.WasmPostgreSQL, DatabaseType.WasmPostgreSQL, PgliteTypes).ShouldBe("bigint");
    }

    [Fact]
    public void Map_WhenTheTargetHasNothingOfThatKind_GivesNothing()
    {
        ColumnTypeMapping.Map("bytea", DatabaseType.WasmPostgreSQL, DatabaseType.WasmSQLite, ["INTEGER", "TEXT"]).ShouldBeNull();
    }

    [Fact]
    public void Map_ForATypeItDoesNotRecognise_GivesNothing()
    {
        ColumnTypeMapping.Map("geometry", DatabaseType.WasmPostgreSQL, DatabaseType.WasmSQLite, SqliteTypes).ShouldBeNull();
    }

    [Fact]
    public void ChangeEngine_ConvertsEachColumnAndListsWhatChanged()
    {
        var model = new SchemaWizardModel
        {
            EngineType = DatabaseType.WasmPostgreSQL,
            Tables =
            [
                new TableDefinitionModel
                {
                    Name = "orders",
                    Columns =
                    [
                        new ColumnDefinitionModel { Name = "id", DataType = "integer" },
                        new ColumnDefinitionModel { Name = "placed_at", DataType = "timestamptz" },
                        new ColumnDefinitionModel { Name = "", DataType = "" }
                    ]
                }
            ]
        };

        var changes = model.ChangeEngine(DatabaseType.WasmSQLite, SqliteTypes);

        model.EngineType.ShouldBe(DatabaseType.WasmSQLite);
        model.Tables[0].Columns.Select(c => c.DataType).ShouldBe(["INTEGER", "TEXT", ""]);
        changes.ShouldBe([
            new ColumnTypeChange("orders", "id", "integer", "INTEGER"),
            new ColumnTypeChange("orders", "placed_at", "timestamptz", "TEXT")
        ]);
    }

    [Fact]
    public void ChangeEngine_ClearsTypesWithNoEquivalentSoTheyMustBePickedAgain()
    {
        var model = new SchemaWizardModel
        {
            EngineType = DatabaseType.WasmPostgreSQL,
            Tables = [new TableDefinitionModel { Name = "", Columns = [new ColumnDefinitionModel { Name = "", DataType = "bytea" }] }]
        };

        var changes = model.ChangeEngine(DatabaseType.WasmSQLite, ["INTEGER", "TEXT"]);

        model.Tables[0].Columns[0].DataType.ShouldBe("");
        changes.ShouldBe([new ColumnTypeChange("Table 1", "column 1", "bytea", null)]);
    }

    [Fact]
    public void ChangeEngine_ToTheSameEngine_ChangesNothing()
    {
        var model = new SchemaWizardModel
        {
            EngineType = DatabaseType.WasmSQLite,
            Tables = [new TableDefinitionModel { Name = "t", Columns = [new ColumnDefinitionModel { Name = "c", DataType = "TEXT" }] }]
        };

        model.ChangeEngine(DatabaseType.WasmSQLite, SqliteTypes).ShouldBeEmpty();
        model.Tables[0].Columns[0].DataType.ShouldBe("TEXT");
    }
}
