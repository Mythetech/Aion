namespace Aion.Components.Querying.Errors;

/// <summary>
/// Picks the known name a misspelled identifier most likely meant, comparing case-insensitively by
/// edit distance (insertions, deletions, substitutions and swapped neighbours each cost one).
/// </summary>
public static class IdentifierMatcher
{
    public static string? Closest(string name, IEnumerable<string> candidates)
    {
        var maxDistance = MaxDistance(name.Length);
        var lowered = name.ToLowerInvariant();

        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var candidate in candidates)
        {
            // The name exists as written, so it is not a misspelling of it.
            if (candidate.Equals(name, StringComparison.Ordinal)) continue;

            var distance = Distance(lowered, candidate.ToLowerInvariant());
            if (distance > maxDistance) continue;

            if (distance < bestDistance || (distance == bestDistance && string.CompareOrdinal(candidate, best) < 0))
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>
    /// Longer names tolerate more typos. Names of one or two characters only match when they differ
    /// by case, since nearly every short name is one edit from another.
    /// </summary>
    private static int MaxDistance(int length) => length <= 2 ? 0 : Math.Clamp(length / 3, 1, 3);

    private static int Distance(string a, string b)
    {
        var previousPrevious = new int[b.Length + 1];
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);

                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    current[j] = Math.Min(current[j], previousPrevious[j - 2] + 1);
                }
            }

            (previousPrevious, previous, current) = (previous, current, previousPrevious);
        }

        return previous[b.Length];
    }
}
