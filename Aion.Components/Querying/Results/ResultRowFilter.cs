namespace Aion.Components.Querying.Results;

/// <summary>
/// Decides which rows the find-in-results box keeps. The grid and the export buttons share it, so an
/// export of a filtered result holds exactly the rows the grid showed.
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
        }

        return false;
    }
}
