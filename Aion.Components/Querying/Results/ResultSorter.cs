using System.Globalization;
using System.Numerics;

namespace Aion.Components.Querying.Results;

/// <summary>
/// Sorts result rows by one column the way a reader expects: numbers (and text that is a number) by value,
/// text ignoring case, NULLs last in either direction, and rows with equal values in their fetched order.
/// Values of different kinds order numbers, then text, then everything else, as SQLite does.
/// </summary>
internal static class ResultSorter
{
    public static IReadOnlyList<ResultRow> Sort(IReadOnlyList<ResultRow> rows, ResultSort sort)
    {
        // Each value is classified once, since a sort compares every row many times.
        var keys = new SortKey[rows.Count];
        var order = new int[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            keys[i] = SortKey.From(rows[i].Values.GetValueOrDefault(sort.Key));
            order[i] = i;
        }

        var descending = sort.Direction == ResultSortDirection.Descending;
        Array.Sort(order, (a, b) =>
        {
            var compared = SortKey.Compare(keys[a], keys[b], descending);
            return compared != 0 ? compared : rows[a].Index.CompareTo(rows[b].Index);
        });

        var sorted = new ResultRow[rows.Count];
        for (var i = 0; i < order.Length; i++)
        {
            sorted[i] = rows[order[i]];
        }

        return sorted;
    }

    private enum Kind
    {
        Number,
        Text,
        Other,
        Null
    }

    private readonly record struct SortKey(Kind Kind, double Number, decimal? Exact, string? Text, object? Other)
    {
        public static SortKey From(object? value) => value switch
        {
            null or DBNull => new SortKey(Kind.Null, 0, null, null, null),
            string text => FromText(text),
            decimal m => new SortKey(Kind.Number, (double)m, m, null, null),
            double d => new SortKey(Kind.Number, d, null, null, null),
            float f => new SortKey(Kind.Number, f, null, null, null),
            BigInteger big => new SortKey(Kind.Number, (double)big, null, null, null),
            byte or sbyte or short or ushort or int or uint or long or ulong =>
                new SortKey(Kind.Number, Convert.ToDouble(value, CultureInfo.InvariantCulture), Convert.ToDecimal(value, CultureInfo.InvariantCulture), null, null),
            _ => new SortKey(Kind.Other, 0, null, null, value)
        };

        private static SortKey FromText(string text)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return new SortKey(Kind.Text, 0, null, text, null);

            decimal? exact = decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
            return new SortKey(Kind.Number, number, exact, null, null);
        }

        public static int Compare(in SortKey a, in SortKey b, bool descending)
        {
            if (a.Kind == Kind.Null || b.Kind == Kind.Null)
                return a.Kind == b.Kind ? 0 : a.Kind == Kind.Null ? 1 : -1;

            var compared = a.Kind != b.Kind
                ? a.Kind.CompareTo(b.Kind)
                : a.Kind switch
                {
                    Kind.Number => a.Exact is { } x && b.Exact is { } y ? x.CompareTo(y) : a.Number.CompareTo(b.Number),
                    Kind.Text => CompareText(a.Text!, b.Text!),
                    _ => CompareOther(a.Other!, b.Other!)
                };

            return descending ? -compared : compared;
        }

        private static int CompareText(string a, string b)
        {
            var compared = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            return compared != 0 ? compared : string.CompareOrdinal(a, b);
        }

        private static int CompareOther(object a, object b)
        {
            if (a is byte[] x && b is byte[] y)
                return x.AsSpan().SequenceCompareTo(y);

            if (a.GetType() == b.GetType() && a is IComparable comparable)
                return comparable.CompareTo(b);

            var byType = string.CompareOrdinal(a.GetType().Name, b.GetType().Name);
            return byType != 0 ? byType : string.CompareOrdinal(a.ToString(), b.ToString());
        }
    }
}
