using System.Globalization;
using Aion.Contracts.Database;

namespace Aion.Components.Connections;

/// <summary>
/// Formats a table's row count for the schema tree: a short label beside the table name and a hover text that
/// says whether the number was counted or estimated. Estimates are marked with a tilde and rounded, since
/// catalog statistics are rarely right to the last row.
/// </summary>
public static class RowCountText
{
    private static readonly string[] Suffixes = ["k", "M", "B", "T"];

    // Exact counts stay exact while they fit beside a table name.
    private const long LongestExactLabel = 9_999;

    public static string Short(TableRowCount count)
    {
        var number = count.IsEstimate ? "~" + Compact(count.Rows)
            : count.Rows <= LongestExactLabel ? Grouped(count.Rows)
            : Compact(count.Rows);

        return $"{number} {Rows(count.Rows)}";
    }

    public static string Describe(TableRowCount count) =>
        count.IsEstimate
            ? $"About {Grouped(count.Rows)} {Rows(count.Rows)}, estimated from table statistics"
            : $"{Grouped(count.Rows)} {Rows(count.Rows)}, counted when the tables were listed";

    private static string Rows(long rows) => rows == 1 ? "row" : "rows";

    private static string Grouped(long rows) => rows.ToString("N0", CultureInfo.InvariantCulture);

    // One decimal below ten of a unit (1.2k), whole numbers above (123k). A value that rounds up to a
    // thousand moves to the next unit, so 999,999 reads 1M rather than 1000k. Decimal keeps 9,950 at
    // exactly 9.95k, which a double would hold as 9.9499... and round down.
    private static string Compact(long rows)
    {
        if (rows < 1_000)
            return rows.ToString(CultureInfo.InvariantCulture);

        decimal value = rows;
        var unit = -1;
        do
        {
            value /= 1_000;
            unit++;
        } while (Round(value) >= 1_000 && unit < Suffixes.Length - 1);

        return Round(value).ToString("0.#", CultureInfo.InvariantCulture) + Suffixes[unit];
    }

    private static decimal Round(decimal value) => Math.Round(value, value < 10 ? 1 : 0, MidpointRounding.AwayFromZero);
}
