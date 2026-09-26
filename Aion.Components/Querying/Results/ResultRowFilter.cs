namespace Aion.Components.Querying.Results;

/// <summary>
/// Decides which rows the find-in-results box keeps: those with a value containing the text, either as
/// fetched or as the grid shows it.
/// </summary>
public static class ResultRowFilter
{
    public static bool Matches(Dictionary<string, object> row, string? filter)
    {
        if (string.IsNullOrEmpty(filter))
            return true;

        foreach (var value in row.Values)
        {
            if (value is null)
                continue;

            var text = value as string ?? value.ToString();
            if (text != null && text.Contains(filter, StringComparison.OrdinalIgnoreCase))
                return true;

            // The grid rounds and reformats some values, so what the user reads there has to match too.
            if (HasOwnDisplay(value) && ResultValueFormatter.Display(value) is { } shown
                && shown.Contains(filter, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool HasOwnDisplay(object value) =>
        value is double or float or bool or DateTime or DateTimeOffset or DateOnly or TimeOnly or byte[];
}
