using Aion.Components.Scaffolding;
using Aion.Components.Scaffolding.DataGeneration;
using Aion.Contracts.Database;
using Shouldly;

namespace Aion.Test.Unit.DataGeneration;

public class GeneratedValuesTests
{
    private static ColumnTypeShape Shape(string type, DatabaseType engine = DatabaseType.PostgreSQL, int? length = null) =>
        ColumnTypeShape.Of(type, engine, length);

    private static readonly DateTime Moment = new(2024, 3, 5, 14, 30, 15);

    [Fact]
    public void Fit_ShortensTextToTheColumnsMaximumLength()
    {
        GeneratedValues.Fit("O'Brien-Smith", Shape("varchar(7)")).ShouldBe("O'Brien");
    }

    [Fact]
    public void Fit_UsesTheCatalogLengthWhenTheTypeHasNone()
    {
        GeneratedValues.Fit("abcdefgh", Shape("character varying", length: 3)).ShouldBe("abc");
    }

    [Fact]
    public void Fit_LeavesTextThatFitsAlone()
    {
        GeneratedValues.Fit("Anne O'Neil", Shape("text")).ShouldBe("Anne O'Neil");
    }

    [Theory]
    [InlineData("boolean", DatabaseType.PostgreSQL, true)]
    [InlineData("bit", DatabaseType.SQLServer, true)]
    [InlineData("tinyint(1)", DatabaseType.MySQL, true)]
    public void Fit_KeepsBooleansForBooleanColumns(string type, DatabaseType engine, bool value)
    {
        GeneratedValues.Fit(value, Shape(type, engine)).ShouldBe(value);
    }

    [Theory]
    [InlineData(true, 1L)]
    [InlineData(false, 0L)]
    public void Fit_WritesBooleansAsOneOrZeroInIntegerColumns(bool value, long expected)
    {
        GeneratedValues.Fit(value, Shape("INTEGER", DatabaseType.WasmSQLite)).ShouldBe(expected);
    }

    [Fact]
    public void Fit_WritesBooleansAsWordsInTextColumns()
    {
        GeneratedValues.Fit(true, Shape("text")).ShouldBe("true");
    }

    [Fact]
    public void Fit_GivesDateColumnsOnlyTheDate()
    {
        GeneratedValues.Fit(Moment, Shape("date")).ShouldBe(new DateOnly(2024, 3, 5));
    }

    [Fact]
    public void Fit_GivesTimeColumnsOnlyTheTimeOfDay()
    {
        GeneratedValues.Fit(Moment, Shape("time")).ShouldBe(new TimeOnly(14, 30, 15));
    }

    [Fact]
    public void Fit_GivesTimestampWithTimeZoneColumnsAnOffset()
    {
        GeneratedValues.Fit(Moment, Shape("timestamptz")).ShouldBe(new DateTimeOffset(Moment, TimeSpan.Zero));
    }

    [Fact]
    public void Fit_KeepsDateTimesForTimestampColumns()
    {
        GeneratedValues.Fit(Moment, Shape("datetime2", DatabaseType.SQLServer)).ShouldBe(Moment);
    }

    [Fact]
    public void Fit_WritesDateTimesInTextColumnsTheWaySqliteStoresThem()
    {
        GeneratedValues.Fit(Moment, Shape("TEXT", DatabaseType.WasmSQLite)).ShouldBe("2024-03-05 14:30:15");
    }

    [Theory]
    [InlineData("42", "integer", 42L)]
    [InlineData("12.5", "numeric", 12.5)]
    public void Fit_ReadsNumbersTypedIntoACustomList(string value, string type, object expected)
    {
        GeneratedValues.Fit(value, Shape(type)).ShouldBe(expected is double d ? (decimal)d : expected);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("0", false)]
    [InlineData("Yes", true)]
    public void Fit_ReadsBooleansTypedIntoACustomList(string value, bool expected)
    {
        GeneratedValues.Fit(value, Shape("boolean")).ShouldBe(expected);
    }

    [Fact]
    public void Fit_ReadsUuidsTypedIntoACustomList()
    {
        var id = Guid.NewGuid();

        GeneratedValues.Fit(id.ToString(), Shape("uniqueidentifier", DatabaseType.SQLServer)).ShouldBe(id);
    }

    [Fact]
    public void Fit_KeepsNull()
    {
        GeneratedValues.Fit(null, Shape("text")).ShouldBeNull();
    }
}
