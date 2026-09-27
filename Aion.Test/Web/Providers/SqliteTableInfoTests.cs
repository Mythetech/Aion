using Aion.Web.Providers;
using Shouldly;

namespace Aion.Test.Web.Providers;

/// <summary>
/// The rows mirror what PRAGMA table_xinfo returns in the in-browser SQLite build.
/// </summary>
public class SqliteTableInfoTests
{
    private static SqliteTableInfoRow Row(string name, string type, bool notNull = false, int pk = 0, string? defaultValue = null, int hidden = 0) =>
        new(name, type, notNull, defaultValue, pk, hidden);

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

    [Fact]
    public void VirtualAndStoredGeneratedColumns_AreGenerated()
    {
        var columns = SqliteTableInfo.ToColumns(
        [
            Row("price", "REAL"),
            Row("with_tax", "REAL", hidden: 2),
            Row("label", "TEXT", hidden: 3)
        ]);

        columns.Select(c => (c.Name, c.IsGenerated)).ShouldBe(
        [
            ("price", false),
            ("with_tax", true),
            ("label", true)
        ]);
    }

    [Fact]
    public void HiddenColumnsOfVirtualTables_AreLeftOut()
    {
        // PRAGMA table_info never listed them, and they can't be selected with * or written like other columns.
        var columns = SqliteTableInfo.ToColumns([Row("body", ""), Row("notes_fts", "", hidden: 1), Row("rank", "", hidden: 1)]);

        columns.Select(c => c.Name).ShouldBe(["body"]);
    }
}
