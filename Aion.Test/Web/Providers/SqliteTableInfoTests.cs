using Aion.Web.Providers;
using Shouldly;

namespace Aion.Test.Web.Providers;

/// <summary>
/// The rows mirror what PRAGMA table_info returns in the in-browser SQLite build.
/// </summary>
public class SqliteTableInfoTests
{
    private static SqliteTableInfoRow Row(string name, string type, bool notNull = false, int pk = 0, string? defaultValue = null) =>
        new(name, type, notNull, defaultValue, pk);

    [Fact]
    public void IntegerPrimaryKey_IsNotNullableEvenThoughThePragmaSaysNotNullIsOff()
    {
        var columns = SqliteTableInfo.ToColumns([Row("id", "INTEGER", pk: 1), Row("name", "TEXT", notNull: true)]);

        var id = columns.Single(c => c.Name == "id");
        id.IsPrimaryKey.ShouldBeTrue();
        id.IsNullable.ShouldBeFalse();
    }

    [Theory]
    [InlineData("TEXT")]
    [InlineData("INT")]
    [InlineData("BIGINT")]
    public void OtherSingleColumnPrimaryKeys_KeepTheReportedNullability(string type)
    {
        // Only INTEGER PRIMARY KEY is the rowid alias; SQLite lets other primary key columns of a rowid table hold NULL.
        var columns = SqliteTableInfo.ToColumns([Row("key", type, pk: 1)]);

        columns.Single().IsNullable.ShouldBeTrue();
    }

    [Fact]
    public void CompositeIntegerPrimaryKey_KeepsTheReportedNullability()
    {
        var columns = SqliteTableInfo.ToColumns([Row("a", "INTEGER", pk: 1), Row("b", "INTEGER", pk: 2)]);

        columns.ShouldAllBe(c => c.IsPrimaryKey && c.IsNullable);
    }

    [Fact]
    public void DeclaredNotNull_IsNotNullable()
    {
        var columns = SqliteTableInfo.ToColumns([Row("key", "TEXT", notNull: true, pk: 1)]);

        columns.Single().IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Columns_KeepTheirOrderTypeAndDefault()
    {
        var columns = SqliteTableInfo.ToColumns(
        [
            Row("id", "INTEGER", pk: 1),
            Row("status", "TEXT", notNull: true, defaultValue: "'pending'"),
            Row("notes", "")
        ]);

        columns.Select(c => (c.Name, c.DataType, c.DefaultValue)).ShouldBe(
        [
            ("id", "INTEGER", null),
            ("status", "TEXT", "'pending'"),
            ("notes", "", null)
        ]);
        columns.ShouldAllBe(c => !c.IsIdentity);
    }
}
