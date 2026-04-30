using Aion.Components.Visualization;
using Aion.Contracts.Queries;
using Shouldly;

namespace Aion.Test.Unit.Visualization;

public class AggregationEngineTests
{
    private readonly AggregationEngine _sut = new();

    [Fact]
    public void Count_ShouldCountRowsPerGroup()
    {
        var result = CreateResult(
            ["category", "amount"],
            [
                new() { ["category"] = "A", ["amount"] = 10 },
                new() { ["category"] = "A", ["amount"] = 20 },
                new() { ["category"] = "B", ["amount"] = 30 }
            ]);

        var agg = _sut.Aggregate(result, "category", "amount", AggregateFunction.Count);

        agg.Labels.ShouldBe(["A", "B"]);
        agg.Values.ShouldBe([2.0, 1.0]);
    }

    [Fact]
    public void Sum_ShouldSumValuesPerGroup()
    {
        var result = CreateResult(
            ["category", "amount"],
            [
                new() { ["category"] = "A", ["amount"] = 10 },
                new() { ["category"] = "A", ["amount"] = 20 },
                new() { ["category"] = "B", ["amount"] = 30 }
            ]);

        var agg = _sut.Aggregate(result, "category", "amount", AggregateFunction.Sum);

        agg.Labels.ShouldBe(["A", "B"]);
        agg.Values.ShouldBe([30.0, 30.0]);
    }

    [Fact]
    public void Avg_ShouldAverageValuesPerGroup()
    {
        var result = CreateResult(
            ["category", "amount"],
            [
                new() { ["category"] = "X", ["amount"] = 10 },
                new() { ["category"] = "X", ["amount"] = 30 },
                new() { ["category"] = "Y", ["amount"] = 50 }
            ]);

        var agg = _sut.Aggregate(result, "category", "amount", AggregateFunction.Avg);

        agg.Labels.ShouldBe(["X", "Y"]);
        agg.Values.ShouldBe([20.0, 50.0]);
    }

    [Fact]
    public void Min_ShouldFindMinPerGroup()
    {
        var result = CreateResult(
            ["group", "val"],
            [
                new() { ["group"] = "G1", ["val"] = 5 },
                new() { ["group"] = "G1", ["val"] = 15 },
                new() { ["group"] = "G2", ["val"] = 8 }
            ]);

        var agg = _sut.Aggregate(result, "group", "val", AggregateFunction.Min);

        agg.Values.ShouldBe([5.0, 8.0]);
    }

    [Fact]
    public void Max_ShouldFindMaxPerGroup()
    {
        var result = CreateResult(
            ["group", "val"],
            [
                new() { ["group"] = "G1", ["val"] = 5 },
                new() { ["group"] = "G1", ["val"] = 15 },
                new() { ["group"] = "G2", ["val"] = 8 }
            ]);

        var agg = _sut.Aggregate(result, "group", "val", AggregateFunction.Max);

        agg.Values.ShouldBe([15.0, 8.0]);
    }

    [Fact]
    public void EmptyResult_ShouldReturnEmptyAggregation()
    {
        var result = CreateResult(["col"], []);

        var agg = _sut.Aggregate(result, "col", "col", AggregateFunction.Count);

        agg.Labels.ShouldBeEmpty();
        agg.Values.ShouldBeEmpty();
    }

    [Fact]
    public void InvalidGroupColumn_ShouldReturnEmptyAggregation()
    {
        var result = CreateResult(
            ["a"],
            [new() { ["a"] = 1 }]);

        var agg = _sut.Aggregate(result, "nonexistent", "a", AggregateFunction.Count);

        agg.Labels.ShouldBeEmpty();
    }

    [Fact]
    public void NullValues_ShouldGroupAsNull()
    {
        var result = CreateResult(
            ["category", "amount"],
            [
                new() { ["category"] = "A", ["amount"] = 10 },
                new() { ["category"] = null!, ["amount"] = 20 }
            ]);

        var agg = _sut.Aggregate(result, "category", "amount", AggregateFunction.Count);

        agg.Labels.ShouldContain("(null)");
    }

    [Fact]
    public void StringNumericValues_ShouldParseCorrectly()
    {
        var result = CreateResult(
            ["category", "amount"],
            [
                new() { ["category"] = "A", ["amount"] = "42.5" },
                new() { ["category"] = "A", ["amount"] = "7.5" }
            ]);

        var agg = _sut.Aggregate(result, "category", "amount", AggregateFunction.Sum);

        agg.Values.ShouldBe([50.0]);
    }

    [Fact]
    public void GetGroupableColumns_ShouldExcludeHighCardinalityColumns()
    {
        var rows = Enumerable.Range(0, 100).Select(i => new Dictionary<string, object>
        {
            ["id"] = i,
            ["status"] = i % 3 == 0 ? "active" : "inactive"
        }).ToList();

        var result = CreateResult(["id", "status"], rows);

        var groupable = _sut.GetGroupableColumns(result);

        groupable.ShouldContain("status");
    }

    [Fact]
    public void GetMeasurableColumns_ShouldDetectNumericColumns()
    {
        var result = CreateResult(
            ["name", "age", "score"],
            [
                new() { ["name"] = "Alice", ["age"] = 30, ["score"] = 95.5 },
                new() { ["name"] = "Bob", ["age"] = 25, ["score"] = 87.0 }
            ]);

        var measurable = _sut.GetMeasurableColumns(result);

        measurable.ShouldContain("age");
        measurable.ShouldContain("score");
        measurable.ShouldNotContain("name");
    }

    [Fact]
    public void RecommendedChartType_FewGroupsWithCount_ShouldSuggestPie()
    {
        var result = CreateResult(
            ["status", "count"],
            [
                new() { ["status"] = "active", ["count"] = 10 },
                new() { ["status"] = "inactive", ["count"] = 5 },
                new() { ["status"] = "pending", ["count"] = 3 }
            ]);

        var agg = _sut.Aggregate(result, "status", "count", AggregateFunction.Count);

        agg.RecommendedChartType.ShouldBe(ChartTypeRecommendation.Pie);
    }

    [Fact]
    public void RecommendedChartType_ManyGroups_ShouldSuggestLine()
    {
        var rows = Enumerable.Range(1, 20).Select(i => new Dictionary<string, object>
        {
            ["month"] = $"2024-{i:D2}",
            ["revenue"] = i * 1000
        }).ToList();

        var result = CreateResult(["month", "revenue"], rows);

        var agg = _sut.Aggregate(result, "month", "revenue", AggregateFunction.Sum);

        agg.RecommendedChartType.ShouldBe(ChartTypeRecommendation.Line);
    }

    [Fact]
    public void RecommendedChartType_ModerateGroupsWithSum_ShouldSuggestBar()
    {
        var rows = Enumerable.Range(1, 10).Select(i => new Dictionary<string, object>
        {
            ["dept"] = $"Dept{i}",
            ["budget"] = i * 5000
        }).ToList();

        var result = CreateResult(["dept", "budget"], rows);

        var agg = _sut.Aggregate(result, "dept", "budget", AggregateFunction.Sum);

        agg.RecommendedChartType.ShouldBe(ChartTypeRecommendation.Bar);
    }

    [Fact]
    public void Aggregate_WithMixedNumericTypes_ShouldHandleCorrectly()
    {
        var result = CreateResult(
            ["group", "val"],
            [
                new() { ["group"] = "A", ["val"] = (long)100 },
                new() { ["group"] = "A", ["val"] = 50.5 },
                new() { ["group"] = "A", ["val"] = (short)25 }
            ]);

        var agg = _sut.Aggregate(result, "group", "val", AggregateFunction.Sum);

        agg.Values.ShouldBe([175.5]);
    }

    [Fact]
    public void Labels_ShouldBeSortedAlphabetically()
    {
        var result = CreateResult(
            ["name", "val"],
            [
                new() { ["name"] = "Charlie", ["val"] = 1 },
                new() { ["name"] = "Alice", ["val"] = 2 },
                new() { ["name"] = "Bob", ["val"] = 3 }
            ]);

        var agg = _sut.Aggregate(result, "name", "val", AggregateFunction.Count);

        agg.Labels.ShouldBe(["Alice", "Bob", "Charlie"]);
    }

    private static QueryResult CreateResult(
        string[] columns,
        List<Dictionary<string, object>> rows)
    {
        return new QueryResult
        {
            Columns = columns.ToList(),
            Rows = rows
        };
    }
}
