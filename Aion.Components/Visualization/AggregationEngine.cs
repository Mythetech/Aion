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
    public string MeasureColumn { get; set; } = string.Empty;
    public AggregateFunction Function { get; set; }
    public ChartTypeRecommendation RecommendedChartType { get; set; }
}

public enum ChartTypeRecommendation
{
    Bar,
    Pie,
    Line
}

public class AggregationEngine
{
    public const string NullLabel = "(null)";

    public AggregationResult Aggregate(
        QueryResult result,
        string groupByColumn,
        string measureColumn,
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
            MeasureColumn = measureColumn,
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
            .Where(col => IsNumericColumn(result, col))
            .ToList();
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

    private static bool IsNumericColumn(QueryResult result, string column)
    {
        var sample = result.Rows.Take(20).ToList();
        var numericCount = sample.Count(row => ToNumber(GetValue(row, column)).HasValue);
        return numericCount > sample.Count * 0.5;
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
}
