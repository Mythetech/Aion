using Aion.Contracts.Queries;
using Shouldly;

namespace Aion.Test.Unit.Querying;

public class QueryResultColumnsTests
{
    [Fact]
    public void AddColumn_KeysARepeatedNameApart_AndKeepsTheNameForDisplay()
    {
        var result = new QueryResult();

        var first = result.AddColumn("id", "integer");
        var second = result.AddColumn("id", "integer");
        var third = result.AddColumn("id");

        (first, second, third).ShouldBe(("id", "id_2", "id_3"));
        result.Columns.ShouldBe(["id", "id_2", "id_3"]);
        result.ColumnNames.ShouldBe(["id", "id", "id"]);
        result.ColumnTypes.ShouldBe(["integer", "integer", null]);
    }

    [Fact]
    public void AddColumn_NeverReusesAKeyARealColumnAlreadyHas()
    {
        var result = new QueryResult();

        result.AddColumn("id_2");
        result.AddColumn("id");
        result.AddColumn("id");

        result.Columns.ShouldBe(["id_2", "id", "id_3"]);
        result.Columns.Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public void AddColumn_KeysUnnamedColumnsByPosition()
    {
        var result = new QueryResult();

        result.AddColumn("");
        result.AddColumn("");

        result.Columns.ShouldBe(["column1", "column2"]);
        result.ColumnNames.ShouldBe(["", ""]);
    }

    [Fact]
    public void ColumnName_FallsBackToTheKey_ForResultsBuiltWithoutNames()
    {
        var result = new QueryResult { Columns = ["_id", "name"] };

        result.ColumnName(0).ShouldBe("_id");
        result.ColumnName(1).ShouldBe("name");
    }

    [Fact]
    public void Clone_KeepsTheColumnNames()
    {
        var result = new QueryResult();
        result.AddColumn("id");
        result.AddColumn("id");

        result.Clone().ColumnNames.ShouldBe(["id", "id"]);
    }
}
