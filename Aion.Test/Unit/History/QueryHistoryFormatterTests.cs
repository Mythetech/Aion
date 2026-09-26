using System.Globalization;
using Aion.Components.History;
using Aion.Test.TestDoubles;
using Shouldly;

namespace Aion.Test.Unit.History;

public class QueryHistoryFormatterTests
{
    private static readonly DateTimeOffset Now = HistoryEntries.Now;

    [Fact]
    public void GroupByDay_LabelsTodayYesterdayAndOlderDaysNewestFirst()
    {
        var older = HistoryEntries.Success("SELECT older", Now.AddDays(-3));
        var yesterday = HistoryEntries.Success("SELECT yesterday", Now.AddDays(-1));
        var earlierToday = HistoryEntries.Success("SELECT earlier", Now.AddHours(-2));
        var latest = HistoryEntries.Success("SELECT latest", Now);

        var groups = QueryHistoryFormatter.GroupByDay([older, earlierToday, yesterday, latest], Now);

        groups.Select(g => g.Label).ShouldBe(["Today", "Yesterday", Now.AddDays(-3).Date.ToString("dddd, MMMM d", CultureInfo.CurrentCulture)]);
        groups[0].Entries.ShouldBe([latest, earlierToday]);
    }

    [Fact]
    public void GroupByDay_UsesTheViewersOffsetToDecideTheDay()
    {
        var localNow = new DateTimeOffset(2026, 9, 25, 0, 30, 0, TimeSpan.FromHours(2));
        var lateLastNightUtc = new DateTimeOffset(2026, 9, 24, 21, 0, 0, TimeSpan.Zero);

        var groups = QueryHistoryFormatter.GroupByDay([HistoryEntries.Success("SELECT 1", lateLastNightUtc)], localNow);

        groups.Single().Label.ShouldBe("Yesterday");
    }

    [Fact]
    public void DayLabel_IncludesTheYearForEarlierYears()
    {
        var label = QueryHistoryFormatter.DayLabel(new DateTime(2025, 12, 31), Now);

        label.ShouldBe(new DateTime(2025, 12, 31).ToString("MMMM d, yyyy", CultureInfo.CurrentCulture));
    }

    [Theory]
    [InlineData(10, "just now")]
    [InlineData(5 * 60, "5 min ago")]
    [InlineData(60 * 60, "1 hour ago")]
    [InlineData(3 * 60 * 60 + 59, "3 hours ago")]
    public void RelativeTime_DescribesRunsFromToday(int secondsAgo, string expected)
    {
        QueryHistoryFormatter.RelativeTime(Now.AddSeconds(-secondsAgo), Now).ShouldBe(expected);
    }

    [Fact]
    public void RelativeTime_ShowsTheTimeOfDayForEarlierDays()
    {
        var yesterday = Now.AddDays(-1);

        QueryHistoryFormatter.RelativeTime(yesterday, Now).ShouldBe(yesterday.ToString("t", CultureInfo.CurrentCulture));
    }

    [Fact]
    public void Duration_ScalesUnits()
    {
        QueryHistoryFormatter.Duration(TimeSpan.FromMilliseconds(16.7)).ShouldBe("16 ms");
        QueryHistoryFormatter.Duration(TimeSpan.FromMilliseconds(1520)).ShouldBe($"{1.5.ToString("0.0", CultureInfo.CurrentCulture)} s");
        QueryHistoryFormatter.Duration(TimeSpan.FromSeconds(123)).ShouldBe("2 min 3 s");
    }

    [Fact]
    public void Rows_UsesSingularForOne()
    {
        QueryHistoryFormatter.Rows(1).ShouldBe("1 row");
        QueryHistoryFormatter.Rows(0).ShouldBe("0 rows");
        QueryHistoryFormatter.Rows(3).ShouldBe("3 rows");
    }
}
