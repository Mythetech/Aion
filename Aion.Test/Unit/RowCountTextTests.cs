using Aion.Components.Connections;
using Aion.Contracts.Database;
using Shouldly;

namespace Aion.Test.Unit;

public class RowCountTextTests
{
    [Theory]
    [InlineData(0, "0 rows")]
    [InlineData(1, "1 row")]
    [InlineData(15, "15 rows")]
    [InlineData(9_999, "9,999 rows")]
    [InlineData(12_345, "12k rows")]
    [InlineData(2_500_000, "2.5M rows")]
    public void Short_ExactCount_ShowsTheNumberUntilItNeedsShortening(long rows, string expected)
    {
        RowCountText.Short(TableRowCount.Exact(rows)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, "~0 rows")]
    [InlineData(1, "~1 row")]
    [InlineData(999, "~999 rows")]
    [InlineData(1_000, "~1k rows")]
    [InlineData(1_234, "~1.2k rows")]
    [InlineData(9_950, "~10k rows")]
    [InlineData(123_456, "~123k rows")]
    [InlineData(999_999, "~1M rows")]
    [InlineData(45_600_000, "~46M rows")]
    [InlineData(3_200_000_000, "~3.2B rows")]
    public void Short_Estimate_IsMarkedAndRounded(long rows, string expected)
    {
        RowCountText.Short(TableRowCount.Estimated(rows)).ShouldBe(expected);
    }

    [Fact]
    public void Describe_Estimate_SaysWhereTheNumberCameFrom()
    {
        RowCountText.Describe(TableRowCount.Estimated(1_234)).ShouldBe("About 1,234 rows, estimated from table statistics");
    }

    [Fact]
    public void Describe_ExactCount_SaysWhenItWasCounted()
    {
        RowCountText.Describe(TableRowCount.Exact(12_345)).ShouldBe("12,345 rows, counted when the tables were listed");
        RowCountText.Describe(TableRowCount.Exact(1)).ShouldBe("1 row, counted when the tables were listed");
    }
}
