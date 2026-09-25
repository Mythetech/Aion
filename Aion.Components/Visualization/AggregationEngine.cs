using System.Globalization;
using Aion.Contracts.Queries;

namespace Aion.Components.Visualization;

public enum AggregateFunction
{
    Count,
    Sum,
    Avg,
    Min,
    Max
}

public enum AggregationSort
{
    ValueDescending,
    ValueAscending,
    Label
}

public class AggregationResult
{
    public string[] Labels { get; set; } = [];
    public double[] Values { get; set; } = [];
    public string GroupByColumn { get; set; } = string.Empty;
    public string? MeasureColumn { get; set; }
    public AggregateFunction Function { get; set; }
    public ChartTypeRecommendation RecommendedChartType { get; set; }
}

public enum ChartTypeRecommendation
{
    Bar,
    Pie,
    Line
}

public sealed record AnalysisSelection(string GroupByColumn, string? MeasureColumn, AggregateFunction Function);

public class AggregationEngine
{
    public const string NullLabel = "(null)";

    // Enough rows to classify a column without scanning very large results on every redraw.
    private const int ProfileSampleSize = 200;

    // A few stray non-numeric values (for example "N/A") shouldn't hide an otherwise numeric column.
    private const double NumericShareThreshold = 0.9;

    public AggregationResult Aggregate(
        QueryResult result,
        string groupByColumn,
        string? measureColumn,
        AggregateFunction function,
        AggregationSort sort = AggregationSort.Label)
    {
        if (result.Rows.Count == 0 || !result.Columns.Contains(groupByColumn))
            return new AggregationResult();

        var groups = result.Rows
            .GroupBy(row => FormatKey(GetValue(row, groupByColumn)))
            .Select(g => new Group(g.Key, ComputeAggregate(g, measureColumn, function)))
            .ToList();

        var ordered = Order(groups, sort);
        var labels = ordered.Select(g => g.Label).ToArray();

        return new AggregationResult
        {
            Labels = labels,
            Values = ordered.Select(g => g.Value).ToArray(),
            GroupByColumn = groupByColumn,
            MeasureColumn = function == AggregateFunction.Count ? null : measureColumn,
            Function = function,
            RecommendedChartType = RecommendChartType(labels.Length, function)
        };
    }

    public IReadOnlyList<string> GetGroupableColumns(QueryResult result)
    {
        if (result.Rows.Count == 0)
            return [];

        return result.Columns
            .Where(col => CountDistinct(result, col) <= result.Rows.Count * 0.5 || CountDistinct(result, col) <= 50)
            .ToList();
    }

    public IReadOnlyList<string> GetMeasurableColumns(QueryResult result)
    {
        if (result.Rows.Count == 0)
            return [];

        return result.Columns
            .Where(col => ProfileColumn(result, col).IsNumeric)
            .ToList();
    }

    public AnalysisSelection? SuggestSelection(QueryResult result)
    {
        if (result.Columns.Count == 0 || result.Rows.Count == 0)
            return null;

        var profiles = result.Columns.Select(col => ProfileColumn(result, col)).ToList();
        var textColumns = profiles.Where(p => p.IsTextLike).ToList();
        var measureCandidates = profiles.Where(p => p.IsNumeric && !IsIdLike(p.Name)).ToList();

        var groupBy = ChooseGroupBy(profiles, textColumns, hasMeasure: measureCandidates.Count > 0);

        var eligibleMeasures = measureCandidates.Where(p => p.Name != groupBy).ToList();
        var measure = eligibleMeasures.OrderByDescending(p => p.SortOrder).FirstOrDefault();

        return measure is null
            ? new AnalysisSelection(groupBy, null, AggregateFunction.Count)
            : new AnalysisSelection(groupBy, measure.Name, AggregateFunction.Sum);
    }

    public static bool IsIdLike(string columnName)
    {
        var name = columnName.Trim();
        if (name.Length < 2)
            return false;

        var lower = name.ToLowerInvariant();
        if (lower is "id" or "uuid" or "guid" || lower.EndsWith("_id") || lower.EndsWith(" id") || lower.EndsWith("-id"))
            return true;

        // camelCase and PascalCase keys (customerId, CustomerID) end in "Id" or "ID" after a lowercase letter.
        return name.Length > 2
               && (name.EndsWith("Id", StringComparison.Ordinal) || name.EndsWith("ID", StringComparison.Ordinal))
               && char.IsLower(name[^3]);
    }

    public static double? ToNumber(object? value) => value switch
    {
        null or bool => null,
        byte b => b,
        sbyte sb => sb,
        short s => s,
        ushort us => us,
        int i => i,
        uint ui => ui,
        long l => l,
        ulong ul => ul,
        float f => float.IsFinite(f) ? f : null,
        double d => double.IsFinite(d) ? d : null,
        decimal m => (double)m,
        string text => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed)
            ? parsed
            : null,
        _ => null
    };

    private static string ChooseGroupBy(List<ColumnProfile> profiles, List<ColumnProfile> textColumns, bool hasMeasure)
    {
        var namedText = textColumns.Where(p => !IsIdLike(p.Name)).ToList();

        // Counting rows per unique value draws one bar of height 1 per row, so without a measure prefer a column that repeats.
        if (!hasMeasure)
        {
            var repeating = namedText.FirstOrDefault(p => p.HasRepeats);
            if (repeating is not null)
                return repeating.Name;
        }

        return namedText.FirstOrDefault()?.Name
               ?? textColumns.FirstOrDefault()?.Name
               ?? profiles[0].Name;
    }

    private static List<Group> Order(List<Group> groups, AggregationSort sort)
    {
        var numericKeys = groups.Where(g => g.Label != NullLabel).All(g => ToNumber(g.Label).HasValue);

        var byLabel = numericKeys
            ? groups.OrderBy(g => g.Label == NullLabel).ThenBy(g => ToNumber(g.Label) ?? 0)
            : groups.OrderBy(g => g.Label == NullLabel).ThenBy(g => g.Label, StringComparer.OrdinalIgnoreCase);

        var labelRank = byLabel.Select((g, index) => (g, index)).ToDictionary(x => x.g, x => x.index);

        return sort switch
        {
            AggregationSort.ValueDescending => groups.OrderByDescending(g => g.Value).ThenBy(g => labelRank[g]).ToList(),
            AggregationSort.ValueAscending => groups.OrderBy(g => g.Value).ThenBy(g => labelRank[g]).ToList(),
            _ => byLabel.ToList()
        };
    }

    private static double ComputeAggregate(
        IEnumerable<Dictionary<string, object>> group,
        string? measureColumn,
        AggregateFunction function)
    {
        if (function == AggregateFunction.Count || measureColumn is null)
            return group.Count();

        var numericValues = group
            .Select(row => ToNumber(GetValue(row, measureColumn)))
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        if (numericValues.Count == 0)
            return 0;

        return function switch
        {
            AggregateFunction.Sum => numericValues.Sum(),
            AggregateFunction.Avg => numericValues.Average(),
            AggregateFunction.Min => numericValues.Min(),
            AggregateFunction.Max => numericValues.Max(),
            _ => 0
        };
    }

    private static object? GetValue(Dictionary<string, object> row, string column)
        => row.TryGetValue(column, out var value) ? value : null;

    private static string FormatKey(object? value) => value switch
    {
        null => NullLabel,
        // Sums computed by the database often carry binary floating point noise (259.96999999999997).
        double d => d.ToString("G15", CultureInfo.InvariantCulture),
        float f => ((double)f).ToString("G7", CultureInfo.InvariantCulture),
        DateTime dt => dt.TimeOfDay == TimeSpan.Zero
            ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? NullLabel
    };

    private static bool IsMissing(object? value) => value is null || value is string s && string.IsNullOrWhiteSpace(s);

    private static ColumnProfile ProfileColumn(QueryResult result, string column)
    {
        var sample = result.Rows
            .Take(ProfileSampleSize)
            .Select(row => GetValue(row, column))
            .ToList();
        var values = sample.Where(v => !IsMissing(v)).ToList();

        var numbers = values.Select(ToNumber).ToList();
        var numericCount = numbers.Count(n => n.HasValue);
        var isNumeric = numericCount > 0 && numericCount >= values.Count * NumericShareThreshold;

        return new ColumnProfile(
            column,
            IsNumeric: isNumeric,
            IsTextLike: values.Count > 0 && !isNumeric,
            SortOrder: isNumeric ? DetectSortOrder(numbers) : SortOrder.None,
            HasRepeats: sample.Select(FormatKey).Distinct().Count() < sample.Count);
    }

    // A result ordered by a numeric column (ORDER BY revenue DESC) signals the value the query is about.
    // Small counts next to it are often in order by coincidence, but rarely without ties.
    private static SortOrder DetectSortOrder(List<double?> numbers)
    {
        var present = numbers.Where(n => n.HasValue).Select(n => n!.Value).ToList();
        if (present.Count < 3 || present.Distinct().Count() < 2)
            return SortOrder.None;

        var ascending = true;
        var descending = true;
        var ties = false;
        for (var i = 1; i < present.Count; i++)
        {
            if (present[i] < present[i - 1]) ascending = false;
            if (present[i] > present[i - 1]) descending = false;
            if (present[i] == present[i - 1]) ties = true;
        }

        if (!ascending && !descending)
            return SortOrder.None;

        return ties ? SortOrder.SortedWithTies : SortOrder.Sorted;
    }

    private enum SortOrder
    {
        None,
        SortedWithTies,
        Sorted
    }

    private static int CountDistinct(QueryResult result, string column)
    {
        return result.Rows
            .Select(row => FormatKey(GetValue(row, column)))
            .Distinct()
            .Count();
    }

    private static ChartTypeRecommendation RecommendChartType(int groupCount, AggregateFunction function)
    {
        if (function == AggregateFunction.Count && groupCount <= 8)
            return ChartTypeRecommendation.Pie;

        if (groupCount > 15)
            return ChartTypeRecommendation.Line;

        return ChartTypeRecommendation.Bar;
    }

    private sealed record Group(string Label, double Value);

    private sealed record ColumnProfile(string Name, bool IsNumeric, bool IsTextLike, SortOrder SortOrder, bool HasRepeats);
}
