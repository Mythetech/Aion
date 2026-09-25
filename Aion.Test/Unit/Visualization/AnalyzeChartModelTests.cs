using System.Globalization;
using Aion.Components.Visualization;
using Shouldly;

namespace Aion.Test.Unit.Visualization;

public class AnalyzeChartModelTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(259.96999999999997, "259.97")]
    [InlineData(1234.5, "1,234.50")]
    [InlineData(3.0, "3")]
    [InlineData(12000.0, "12,000")]
    [InlineData(-7.126, "-7.13")]
    [InlineData(0.0034, "0.0034")]
    public void FormatValue_RoundsNonIntegersToTwoDecimals(double value, string expected)
    {
        ChartText.FormatValue(value, Invariant).ShouldBe(expected);
    }

    [Fact]
    public void Scale_RoundsTheMaximumUpToANiceStep()
    {
        var scale = ChartScale.Create([259.97, 44.99]);

        scale.Min.ShouldBe(0);
        scale.Max.ShouldBe(300);
        scale.Ticks.ShouldBe([0, 50, 100, 150, 200, 250, 300]);
        scale.FormatTick(50, Invariant).ShouldBe("50");
    }

    [Fact]
    public void Scale_WholeNumberValues_UseWholeNumberTicks()
    {
        var scale = ChartScale.Create([1, 1, 1]);

        scale.Max.ShouldBe(1);
        scale.Ticks.ShouldBe([0, 1]);
    }

    [Fact]
    public void Scale_FractionalValues_FormatTicksWithTheStepsDecimals()
    {
        var scale = ChartScale.Create([0.4, 1.1]);

        scale.Max.ShouldBe(1.2);
        scale.FormatTick(scale.Ticks[1], Invariant).ShouldBe("0.2");
    }

    [Fact]
    public void Scale_NegativeValues_ExtendBelowZero()
    {
        var scale = ChartScale.Create([-40, 90]);

        scale.Min.ShouldBe(-50);
        scale.Max.ShouldBe(100);
        scale.BaselinePercent.ShouldBe(50 / 150.0 * 100, 0.0001);
    }

    [Fact]
    public void Create_BuildsTitleAndSubtitleFromTheAggregation()
    {
        var aggregation = new AggregationResult
        {
            Labels = ["Charlie Davis", "Alice Johnson"],
            Values = [259.96999999999997, 234.95],
            GroupByColumn = "customer",
            MeasureColumn = "revenue",
            Function = AggregateFunction.Sum
        };

        var model = AnalyzeChartModel.Create(aggregation, Invariant)!;

        model.Title.ShouldBe("Revenue by customer");
        model.Subtitle.ShouldBe("Sum of revenue · 2 customers · from current result");
        model.Items.Select(i => i.ValueText).ShouldBe(["259.97", "234.95"]);
    }

    [Fact]
    public void Create_CountHasARowCountTitle()
    {
        var aggregation = new AggregationResult
        {
            Labels = ["Austin"],
            Values = [2],
            GroupByColumn = "ship_city",
            Function = AggregateFunction.Count
        };

        var model = AnalyzeChartModel.Create(aggregation, Invariant)!;

        model.Title.ShouldBe("Row count by ship city");
        model.Subtitle.ShouldBe("Count of rows · 1 ship city · from current result");
    }

    [Fact]
    public void Create_CapsChartItemsButKeepsEveryGroupForTheTable()
    {
        var aggregation = new AggregationResult
        {
            Labels = Enumerable.Range(1, 5).Select(i => $"g{i}").ToArray(),
            Values = [5, 4, 3, 2, 1],
            GroupByColumn = "g",
            Function = AggregateFunction.Count
        };

        var model = AnalyzeChartModel.Create(aggregation, Invariant, maxItems: 3)!;

        model.Items.Count.ShouldBe(3);
        model.AllItems.Count.ShouldBe(5);
        model.IsTruncated.ShouldBeTrue();
    }

    [Theory]
    [InlineData("customer", "customers")]
    [InlineData("city", "cities")]
    [InlineData("status", "statuses")]
    [InlineData("orders", "orders")]
    [InlineData("day", "days")]
    [InlineData("CITY", "CITIES")]
    [InlineData("col1", "col1 values")]
    public void Pluralize_HandlesCommonColumnNames(string noun, string expected)
    {
        ChartText.Pluralize(noun, 2).ShouldBe(expected);
    }
}
