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

    [Fact]
    public void SuggestSelection_GroupedResult_SumsTheMeasureTheRowsAreOrderedBy()
    {
        var result = CreateResult(
            ["customer", "city", "orders", "revenue"],
            [
                new() { ["customer"] = "Charlie Davis", ["city"] = "Denver", ["orders"] = 3L, ["revenue"] = 259.96999999999997 },
                new() { ["customer"] = "Alice Johnson", ["city"] = "Austin", ["orders"] = 2L, ["revenue"] = 234.95 },
                new() { ["customer"] = "George Kim", ["city"] = "Denver", ["orders"] = 2L, ["revenue"] = 164.98 },
                new() { ["customer"] = "Bob Smith", ["city"] = "Boston", ["orders"] = 3L, ["revenue"] = 124.97 },
                new() { ["customer"] = "Hannah Lee", ["city"] = "Austin", ["orders"] = 1L, ["revenue"] = 44.99 }
            ]);

        var selection = _sut.SuggestSelection(result);

        selection.ShouldBe(new AnalysisSelection("customer", "revenue", AggregateFunction.Sum));
    }

    [Fact]
    public void SuggestSelection_CountThatIsOrderedByCoincidence_LosesToTheStrictlyOrderedMeasure()
    {
        var result = CreateResult(
            ["customer", "orders", "revenue"],
            [
                new() { ["customer"] = "Charlie Davis", ["orders"] = 3L, ["revenue"] = 259.97 },
                new() { ["customer"] = "Alice Johnson", ["orders"] = 2L, ["revenue"] = 234.95 },
                new() { ["customer"] = "George Kim", ["orders"] = 2L, ["revenue"] = 164.98 },
                new() { ["customer"] = "Hannah Lee", ["orders"] = 1L, ["revenue"] = 44.99 }
            ]);

        var selection = _sut.SuggestSelection(result);

        selection!.MeasureColumn.ShouldBe("revenue");
    }

    [Fact]
    public void SuggestSelection_PlainTableWithIdColumn_SkipsTheIdForGroupAndMeasure()
    {
        var result = CreateResult(
            ["id", "name", "category", "price", "stock"],
            [
                new() { ["id"] = 1L, ["name"] = "Laptop", ["category"] = "Electronics", ["price"] = 999.99, ["stock"] = 5L },
                new() { ["id"] = 2L, ["name"] = "Desk", ["category"] = "Furniture", ["price"] = 249.5, ["stock"] = 12L },
                new() { ["id"] = 3L, ["name"] = "Mouse", ["category"] = "Electronics", ["price"] = 19.99, ["stock"] = 80L },
                new() { ["id"] = 4L, ["name"] = "Chair", ["category"] = "Furniture", ["price"] = 149.0, ["stock"] = 7L }
            ]);

        var selection = _sut.SuggestSelection(result);

        selection.ShouldBe(new AnalysisSelection("name", "price", AggregateFunction.Sum));
    }

    [Fact]
    public void SuggestSelection_NoNumericColumns_CountsRowsByATextColumnThatRepeats()
    {
        var result = CreateResult(
            ["name", "city"],
            [
                new() { ["name"] = "Alice", ["city"] = "Austin" },
                new() { ["name"] = "Bob", ["city"] = "Boston" },
                new() { ["name"] = "Charlie", ["city"] = "Austin" }
            ]);

        var selection = _sut.SuggestSelection(result);

        selection.ShouldBe(new AnalysisSelection("city", null, AggregateFunction.Count));
    }

    [Fact]
    public void SuggestSelection_OnlyIdNumericColumns_FallsBackToCount()
    {
        var result = CreateResult(
            ["customer_id", "status"],
            [
                new() { ["customer_id"] = 1, ["status"] = "shipped" },
                new() { ["customer_id"] = 2, ["status"] = "pending" },
                new() { ["customer_id"] = 3, ["status"] = "shipped" }
            ]);

        var selection = _sut.SuggestSelection(result);

        selection.ShouldBe(new AnalysisSelection("status", null, AggregateFunction.Count));
    }

    [Fact]
    public void SuggestSelection_AllNumericColumns_GroupsByTheFirstColumn()
    {
        var result = CreateResult(
            ["year", "total"],
            [
                new() { ["year"] = 2023, ["total"] = 10.5 },
                new() { ["year"] = 2024, ["total"] = 7.25 }
            ]);

        var selection = _sut.SuggestSelection(result);

        selection.ShouldBe(new AnalysisSelection("year", "total", AggregateFunction.Sum));
    }

    [Fact]
    public void SuggestSelection_EmptyResult_ReturnsNull()
    {
        _sut.SuggestSelection(CreateResult(["a"], [])).ShouldBeNull();
    }

    [Fact]
    public void GetMeasurableColumns_ParsesNumericStringsWithInvariantCulture()
    {
        var result = CreateResult(
            ["label", "amount", "localized"],
            [
                new() { ["label"] = "a", ["amount"] = "12.50", ["localized"] = "12,5" },
                new() { ["label"] = "b", ["amount"] = null!, ["localized"] = "7,25" },
                new() { ["label"] = "c", ["amount"] = "3", ["localized"] = "1,5" }
            ]);

        var measurable = _sut.GetMeasurableColumns(result);

        measurable.ShouldBe(["amount"]);
    }

    [Theory]
    [InlineData("id", true)]
    [InlineData("ID", true)]
    [InlineData("customer_id", true)]
    [InlineData("customerId", true)]
    [InlineData("CustomerID", true)]
    [InlineData("paid", false)]
    [InlineData("PAID", false)]
    [InlineData("valid", false)]
    [InlineData("revenue", false)]
    public void IsIdLike_RecognizesKeyColumnNames(string column, bool expected)
    {
        AggregationEngine.IsIdLike(column).ShouldBe(expected);
    }

    [Fact]
    public void Labels_NumericGroupKeys_ShouldSortNumerically()
    {
        var result = CreateResult(
            ["bucket", "val"],
            [
                new() { ["bucket"] = 10, ["val"] = 1 },
                new() { ["bucket"] = 2, ["val"] = 1 },
                new() { ["bucket"] = null!, ["val"] = 1 },
                new() { ["bucket"] = 1, ["val"] = 1 }
            ]);

        var agg = _sut.Aggregate(result, "bucket", "val", AggregateFunction.Count);

        agg.Labels.ShouldBe(["1", "2", "10", AggregationEngine.NullLabel]);
    }

    [Fact]
    public void Labels_NumericStringGroupKeys_ShouldSortNumerically()
    {
        var result = CreateResult(
            ["bucket"],
            [
                new() { ["bucket"] = "10" },
                new() { ["bucket"] = "2" },
                new() { ["bucket"] = "1.5" }
            ]);

        var agg = _sut.Aggregate(result, "bucket", null, AggregateFunction.Count);

        agg.Labels.ShouldBe(["1.5", "2", "10"]);
    }

    [Fact]
    public void Labels_MixedTextAndNumberKeys_ShouldSortAsStrings()
    {
        var result = CreateResult(
            ["code"],
            [
                new() { ["code"] = "b10" },
                new() { ["code"] = "10" },
                new() { ["code"] = "b2" },
                new() { ["code"] = "2" }
            ]);

        var agg = _sut.Aggregate(result, "code", null, AggregateFunction.Count);

        agg.Labels.ShouldBe(["10", "2", "b10", "b2"]);
    }

    [Fact]
    public void Labels_DoubleGroupKeys_ShouldDropFloatingPointNoise()
    {
        var result = CreateResult(
            ["total"],
            [new() { ["total"] = 259.96999999999997 }]);

        var agg = _sut.Aggregate(result, "total", null, AggregateFunction.Count);

        agg.Labels.ShouldBe(["259.97"]);
    }

    [Fact]
    public void ValueDescendingSort_ShouldPutLargestGroupFirst()
    {
        var result = CreateResult(
            ["name", "val"],
            [
                new() { ["name"] = "Alice", ["val"] = 5 },
                new() { ["name"] = "Bob", ["val"] = 20 },
                new() { ["name"] = "Charlie", ["val"] = 10 }
            ]);

        var agg = _sut.Aggregate(result, "name", "val", AggregateFunction.Sum, AggregationSort.ValueDescending);

        agg.Labels.ShouldBe(["Bob", "Charlie", "Alice"]);
        agg.Values.ShouldBe([20.0, 10.0, 5.0]);
    }

    [Fact]
    public void ValueAscendingSort_ShouldPutSmallestGroupFirst()
    {
        var result = CreateResult(
            ["name", "val"],
            [
                new() { ["name"] = "Alice", ["val"] = 5 },
                new() { ["name"] = "Bob", ["val"] = 20 },
                new() { ["name"] = "Charlie", ["val"] = 10 }
            ]);

        var agg = _sut.Aggregate(result, "name", "val", AggregateFunction.Sum, AggregationSort.ValueAscending);

        agg.Labels.ShouldBe(["Alice", "Charlie", "Bob"]);
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
