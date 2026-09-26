using Aion.Components.Scaffolding;
using Aion.Components.Scaffolding.DataGeneration;
using Aion.Contracts.Database;
using Shouldly;

namespace Aion.Test.Unit.DataGeneration;

public class DataGenerationPlanTests
{
    private static ColumnInfo Column(string name, string type, bool nullable = false, bool primaryKey = false,
        bool identity = false, string? defaultValue = null, int? maxLength = null, ForeignKeyInfo? foreignKey = null) => new()
    {
        Name = name,
        DataType = type,
        IsNullable = nullable,
        IsPrimaryKey = primaryKey,
        IsIdentity = identity,
        DefaultValue = defaultValue,
        MaxLength = maxLength,
        ForeignKey = foreignKey
    };

    private static ColumnGeneratorBinding Binding(DatabaseType engine, params ColumnInfo[] columns) =>
        DataGenerationPlan.CreateBindings(columns, engine).Single(b => b.Column == columns[^1]);

    [Fact]
    public void CreateBindings_LeavesIdentityColumnsToTheDatabase()
    {
        var binding = Binding(DatabaseType.PostgreSQL, Column("id", "integer", primaryKey: true, identity: true));

        binding.FilledByDatabase.ShouldBe("identity");
        binding.Generator.ShouldBeNull();
        binding.WritesValue.ShouldBeFalse();
    }

    [Fact]
    public void CreateBindings_LeavesSqlitesRowidToTheDatabase()
    {
        var binding = Binding(DatabaseType.WasmSQLite, Column("id", "INTEGER", primaryKey: true));

        binding.FilledByDatabase.ShouldBe("rowid");
    }

    [Fact]
    public void CreateBindings_OnlyTreatsASingleIntegerKeyAsSqlitesRowid()
    {
        var columns = new[]
        {
            Column("order_id", "INTEGER", primaryKey: true),
            Column("line", "INTEGER", primaryKey: true)
        };

        DataGenerationPlan.CreateBindings(columns, DatabaseType.WasmSQLite).ShouldAllBe(b => b.FilledByDatabase == null);
    }

    [Fact]
    public void CreateBindings_LeavesRowversionColumnsToTheDatabase()
    {
        Binding(DatabaseType.SQLServer, Column("version", "timestamp")).FilledByDatabase.ShouldBe("rowversion");
    }

    [Theory]
    [InlineData("int4", DatabaseType.PostgreSQL, typeof(RandomIntGenerator))]
    [InlineData("character varying", DatabaseType.PostgreSQL, typeof(RandomTextGenerator))]
    [InlineData("nvarchar", DatabaseType.SQLServer, typeof(RandomTextGenerator))]
    [InlineData("timestamptz", DatabaseType.PostgreSQL, typeof(DateRangeGenerator))]
    [InlineData("bool", DatabaseType.PostgreSQL, typeof(BooleanGenerator))]
    [InlineData("tinyint(1)", DatabaseType.MySQL, typeof(BooleanGenerator))]
    [InlineData("bit", DatabaseType.SQLServer, typeof(BooleanGenerator))]
    [InlineData("numeric(10,2)", DatabaseType.PostgreSQL, typeof(RandomNumberGenerator))]
    [InlineData("uniqueidentifier", DatabaseType.SQLServer, typeof(UuidGenerator))]
    [InlineData("jsonb", DatabaseType.PostgreSQL, typeof(JsonGenerator))]
    public void CreateBindings_SuggestAGeneratorForTheTypeUnderAnyOfItsNames(string type, DatabaseType engine, Type expected)
    {
        Binding(engine, Column("value", type)).Generator.ShouldBeOfType(expected);
    }

    [Theory]
    [InlineData("email", "text", typeof(EmailGenerator))]
    [InlineData("full_name", "varchar(100)", typeof(NameGenerator))]
    [InlineData("created_at", "timestamp", typeof(DateRangeGenerator))]
    [InlineData("order_date", "TEXT", typeof(DateRangeGenerator))]
    [InlineData("is_active", "INTEGER", typeof(BooleanGenerator))]
    [InlineData("is_active", "boolean", typeof(BooleanGenerator))]
    public void CreateBindings_UseTheColumnNameWhenItFitsTheType(string name, string type, Type expected)
    {
        Binding(DatabaseType.WasmSQLite, Column(name, type)).Generator.ShouldBeOfType(expected);
    }

    [Fact]
    public void CreateBindings_DoNotPickANameGeneratorForAColumnThatCannotHoldText()
    {
        Binding(DatabaseType.PostgreSQL, Column("name_count", "integer")).Generator.ShouldBeOfType<RandomIntGenerator>();
    }

    [Fact]
    public void CreateBindings_NumberAKeyTheDatabaseDoesNotNumber()
    {
        Binding(DatabaseType.SQLServer, Column("id", "int", primaryKey: true)).Generator.ShouldBeOfType<AutoIncrementGenerator>();
    }

    [Fact]
    public void CreateBindings_FillForeignKeysFromTheReferencedColumn()
    {
        var foreignKey = new ForeignKeyInfo { ColumnName = "customer_id", ReferencedTable = "customers", ReferencedColumn = "id" };

        Binding(DatabaseType.PostgreSQL, Column("customer_id", "integer", foreignKey: foreignKey))
            .Generator.ShouldBeOfType<ReferencedValueGenerator>();
    }

    [Fact]
    public void CreateBindings_KeepGeneratedTextWithinTheColumnsLength()
    {
        var binding = Binding(DatabaseType.PostgreSQL, Column("code", "character varying", maxLength: 3));

        binding.Options.MinLength.ShouldBe(3);
        binding.Options.MaxLength.ShouldBe(3);
    }

    [Fact]
    public void CreateBindings_KeepRandomNumbersWithinTheColumnsRange()
    {
        var binding = Binding(DatabaseType.MySQL, Column("rating", "tinyint"));

        binding.Options.MinValue.ShouldBe(0);
        binding.Options.MaxValue.ShouldBe(127);
    }

    [Fact]
    public void CreateBindings_ShowTheDefaultRangeForNumbersWithoutAKnownOne()
    {
        var binding = Binding(DatabaseType.WasmSQLite, Column("price", "REAL"));

        binding.Options.MinValue.ShouldBe(0);
        binding.Options.MaxValue.ShouldBe(1000);
    }

    [Fact]
    public void CompatibleGenerators_OfferNullOnlyForNullableColumns()
    {
        DataGenerationPlan.CompatibleGenerators(Binding(DatabaseType.PostgreSQL, Column("notes", "text", nullable: true)))
            .ShouldContain(g => g is NullGenerator);
        DataGenerationPlan.CompatibleGenerators(Binding(DatabaseType.PostgreSQL, Column("notes", "text")))
            .ShouldNotContain(g => g is NullGenerator);
    }

    // Providers don't report computed columns, so leaving a column out has to be possible for any of them.
    [Fact]
    public void CompatibleGenerators_AlwaysOfferLeavingTheColumnOut()
    {
        DataGenerationPlan.CompatibleGenerators(Binding(DatabaseType.SQLServer, Column("total", "decimal(10,2)")))
            .ShouldContain(g => g is DatabaseDefaultGenerator);
    }

    [Fact]
    public void CreateBindings_ForARequiredTypeNothingGenerates_LeaveTheChoiceToTheUser()
    {
        Binding(DatabaseType.PostgreSQL, Column("span", "tsrange")).Generator.ShouldBeNull();
    }

    [Fact]
    public void CompatibleGenerators_OfferOnlyGeneratorsThatFitTheType()
    {
        var generators = DataGenerationPlan.CompatibleGenerators(Binding(DatabaseType.PostgreSQL, Column("born", "date")));

        generators.ShouldContain(g => g is DateRangeGenerator);
        generators.ShouldNotContain(g => g is EmailGenerator);
        generators.ShouldNotContain(g => g is RandomIntGenerator);
    }

    [Fact]
    public void CreateBindings_ForATypeNothingGenerates_UseNullWhenAllowed()
    {
        Binding(DatabaseType.PostgreSQL, Column("span", "tsrange", nullable: true)).Generator.ShouldBeOfType<NullGenerator>();
    }

    [Fact]
    public void CreateBindings_ForATypeNothingGenerates_UseTheDefaultWhenThereIsOne()
    {
        Binding(DatabaseType.PostgreSQL, Column("span", "tsrange", defaultValue: "'empty'")).Generator.ShouldBeOfType<DatabaseDefaultGenerator>();
    }

    private static DataGenerationModel Model(params ColumnInfo[] columns) => new()
    {
        TableName = "t",
        RowCount = 10,
        ColumnGenerators = DataGenerationPlan.CreateBindings(columns, DatabaseType.PostgreSQL)
    };

    [Fact]
    public void Problems_ForARequiredColumnWithoutAGenerator_AskForOne()
    {
        var model = Model(Column("span", "tsrange"));

        DataGenerationPlan.Problems(model).ShouldBe(["Pick a generator for \"span\": it can't be NULL and has no default"]);
    }

    [Fact]
    public void Problems_ForNullInARequiredColumn_SaySo()
    {
        var model = Model(Column("notes", "text"));
        model.ColumnGenerators[0].Generator = new NullGenerator();

        DataGenerationPlan.Problems(model).ShouldBe(["\"notes\" can't be NULL"]);
    }

    [Fact]
    public void Problems_ForAnEmptyCustomList_AskForValues()
    {
        var model = Model(Column("status", "text"));
        model.ColumnGenerators[0].Generator = new CustomListGenerator();

        DataGenerationPlan.Problems(model).ShouldBe(["Enter the values to pick from for \"status\""]);
    }

    [Fact]
    public void Problems_WhenTheDatabaseFillsEveryColumn_SayThereIsNothingToGenerate()
    {
        var model = Model(Column("id", "integer", identity: true));

        DataGenerationPlan.Problems(model).ShouldBe(["The database fills in every column of this table, so there is nothing to generate"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    public void Problems_ForARowCountOutOfRange_SayWhatIsAllowed(int rows)
    {
        var model = Model(Column("notes", "text"));
        model.RowCount = rows;

        DataGenerationPlan.Problems(model).ShouldBe(["Choose between 1 and 10,000 rows"]);
    }

    [Fact]
    public void Problems_ForAReadyTable_IsEmpty()
    {
        DataGenerationPlan.Problems(Model(Column("id", "integer", identity: true), Column("notes", "text"))).ShouldBeEmpty();
    }
}
