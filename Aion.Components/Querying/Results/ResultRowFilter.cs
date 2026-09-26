using System.Text;
using Aion.Contracts.Queries;

namespace Aion.Components.Querying.Results;

/// <summary>
/// Decides which rows the find-in-results box keeps: those with a value containing the text, either as
/// fetched or as the grid shows it.
/// </summary>
public static class ResultRowFilter
{
    // Joins a row's values into one searchable text; no one types it, so a match never spans two values.
    private const char ValueSeparator = '\u001F';

    public static bool Matches(Dictionary<string, object> row, string? filter) =>
        string.IsNullOrEmpty(filter) || SearchTextOf(row).Contains(filter, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Every value of the row as fetched, plus the grid's text for values it shows differently (such as a
    /// rounded float), in one string. Grid rows keep it, so each keystroke only searches text already built.
    /// </summary>
    public static string SearchTextOf(Dictionary<string, object> row)
    {
        var text = new StringBuilder();
        foreach (var value in row.Values)
        {
            if (value is null)
                continue;

            var fetched = value as string ?? value.ToString();
            text.Append(fetched).Append(ValueSeparator);

            if (HasOwnDisplay(value) && ResultValueFormatter.Display(value) is { } shown && shown != fetched)
                text.Append(shown).Append(ValueSeparator);
        }

        return text.ToString();
    }

    /// <summary>
    /// The result with only the rows the filter keeps, or the result itself when there is no filter.
    /// </summary>
    public static QueryResult Apply(QueryResult result, string? filter)
    {
        if (string.IsNullOrEmpty(filter))
            return result;

        var filtered = result.Clone();
        filtered.Rows = result.Rows.Where(row => Matches(row, filter)).ToList();
        return filtered;
    }

    private static bool HasOwnDisplay(object value) =>
        value is double or float or bool or DateTime or DateTimeOffset or DateOnly or TimeOnly or byte[];
}
