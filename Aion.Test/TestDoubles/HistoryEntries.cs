using Aion.Components.History;

namespace Aion.Test.TestDoubles;

internal static class HistoryEntries
{
    public static readonly DateTimeOffset Now = new(2026, 9, 25, 14, 30, 0, TimeSpan.Zero);

    public static QueryHistoryEntry Success(string sql, DateTimeOffset? at = null, int rows = 3) => new()
    {
        Sql = sql,
        Status = QueryHistoryStatus.Success,
        RowCount = rows,
        Duration = TimeSpan.FromMilliseconds(16),
        ExecutedAt = at ?? Now
    };

    public static QueryHistoryEntry Failed(string sql, string error, DateTimeOffset? at = null) => new()
    {
        Sql = sql,
        Status = QueryHistoryStatus.Failed,
        ErrorMessage = error,
        Duration = TimeSpan.FromMilliseconds(4),
        ExecutedAt = at ?? Now
    };
}
