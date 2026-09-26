using Aion.Web.Providers;
using Shouldly;

namespace Aion.Test.Web.Providers;

public class SqliteCatalogSqlTests
{
    [Theory]
    [InlineData("products", "PRAGMA table_info(\"products\")")]
    [InlineData("odd\"name", "PRAGMA table_info(\"odd\"\"name\")")]
    [InlineData("it's", "PRAGMA table_info(\"it's\")")]
    public void TableInfo_QuotesTheTableAsAnIdentifier(string table, string expected)
    {
        SqliteCatalogSql.TableInfo(table).ShouldBe(expected);
    }

    [Theory]
    [InlineData("products", "PRAGMA foreign_key_list(\"products\")")]
    [InlineData("odd\"name", "PRAGMA foreign_key_list(\"odd\"\"name\")")]
    public void ForeignKeyList_QuotesTheTableAsAnIdentifier(string table, string expected)
    {
        SqliteCatalogSql.ForeignKeyList(table).ShouldBe(expected);
    }

    [Fact]
    public void CountRows_QuotesEachTableAndTagsItsCountWithItsPosition()
    {
        var statements = SqliteCatalogSql.CountRows(["products", "odd\"name", "it's"]);

        statements.ShouldBe(
        [
            """
            SELECT 0, COUNT(*) FROM "products"
            UNION ALL
            SELECT 1, COUNT(*) FROM "odd""name"
            UNION ALL
            SELECT 2, COUNT(*) FROM "it's"
            """.ReplaceLineEndings("\n")
        ]);
    }

    [Fact]
    public void CountRows_SplitsBelowSqlitesLimitOnUnionedSelects()
    {
        var tables = Enumerable.Range(0, 450).Select(i => $"t{i}").ToList();

        var statements = SqliteCatalogSql.CountRows(tables);

        statements.Count.ShouldBe(2);
        statements[0].Split("UNION ALL").Length.ShouldBe(SqliteCatalogSql.CountBatchSize);
        statements[1].ShouldStartWith($"SELECT {SqliteCatalogSql.CountBatchSize}, COUNT(*) FROM \"t{SqliteCatalogSql.CountBatchSize}\"");
    }

    [Fact]
    public void CountRows_WithoutTables_HasNothingToRun()
    {
        SqliteCatalogSql.CountRows([]).ShouldBeEmpty();
    }
}
