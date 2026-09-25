using System.Globalization;

namespace Aion.Components.History;

public record QueryHistoryGroup(string Label, IReadOnlyList<QueryHistoryEntry> Entries);

/// <summary>
/// Display text for history entries. Takes "now" explicitly so labels are deterministic in tests and
/// consistent across a single render.
/// </summary>
public static class QueryHistoryFormatter
{
    public static IReadOnlyList<QueryHistoryGroup> GroupByDay(IEnumerable<QueryHistoryEntry> entries, DateTimeOffset now) =>
        entries
            .GroupBy(e => LocalDate(e.ExecutedAt, now))
            .OrderByDescending(g => g.Key)
            .Select(g => new QueryHistoryGroup(DayLabel(g.Key, now), g.OrderByDescending(e => e.ExecutedAt).ToList()))
            .ToList();

    public static string DayLabel(DateTime day, DateTimeOffset now)
    {
        var today = now.Date;
        if (day == today) return "Today";
        if (day == today.AddDays(-1)) return "Yesterday";

        var format = day.Year == today.Year ? "dddd, MMMM d" : "MMMM d, yyyy";
        return day.ToString(format, CultureInfo.CurrentCulture);
    }

    public static string RelativeTime(DateTimeOffset executedAt, DateTimeOffset now)
    {
        var local = executedAt.ToOffset(now.Offset);
        if (local.Date != now.Date) return local.ToString("t", CultureInfo.CurrentCulture);

        var elapsed = now - local;
        if (elapsed < TimeSpan.FromMinutes(1)) return "just now";
        if (elapsed < TimeSpan.FromHours(1)) return $"{(int)elapsed.TotalMinutes} min ago";

        var hours = (int)elapsed.TotalHours;
        return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
    }

    public static string Duration(TimeSpan duration)
    {
        if (duration < TimeSpan.FromSeconds(1)) return $"{Math.Max(0, (int)duration.TotalMilliseconds)} ms";
        if (duration < TimeSpan.FromMinutes(1)) return $"{duration.TotalSeconds.ToString("0.0", CultureInfo.CurrentCulture)} s";
        return $"{(int)duration.TotalMinutes} min {duration.Seconds} s";
    }

    public static string Rows(int count) =>
        count == 1 ? "1 row" : $"{count.ToString("N0", CultureInfo.CurrentCulture)} rows";

    private static DateTime LocalDate(DateTimeOffset executedAt, DateTimeOffset now) => executedAt.ToOffset(now.Offset).Date;
}
