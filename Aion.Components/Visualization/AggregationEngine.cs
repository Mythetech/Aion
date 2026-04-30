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
    public AggregationResult Aggregate(
        QueryResult result,
        string groupByColumn,
        string measureColumn,
        AggregateFunction function)
    {
        if (result.Rows.Count == 0 || !result.Columns.Contains(groupByColumn))
            return new AggregationResult();

        var groups = result.Rows
            .GroupBy(row => GetStringValue(row, groupByColumn))
            .OrderBy(g => g.Key)
            .ToList();

        var labels = groups.Select(g => g.Key).ToArray();
        var values = groups.Select(g => ComputeAggregate(g, measureColumn, function)).ToArray();

        return new AggregationResult
        {
            Labels = labels,
            Values = values,
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

    private static double ComputeAggregate(
        IGrouping<string, Dictionary<string, object>> group,
        string measureColumn,
        AggregateFunction function)
    {
        if (function == AggregateFunction.Count)
            return group.Count();

        var numericValues = group
            .Select(row => GetNumericValue(row, measureColumn))
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

    private static string GetStringValue(Dictionary<string, object> row, string column)
    {
        if (row.TryGetValue(column, out var value) && value is not null)
            return value.ToString() ?? "(null)";
        return "(null)";
    }

    private static double? GetNumericValue(Dictionary<string, object> row, string column)
    {
        if (!row.TryGetValue(column, out var value) || value is null)
            return null;

        if (value is double d) return d;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is float f) return f;
        if (value is decimal dec) return (double)dec;
        if (value is short s) return s;

        if (double.TryParse(value.ToString(), out var parsed))
            return parsed;

        return null;
    }

    private static bool IsNumericColumn(QueryResult result, string column)
    {
        var sample = result.Rows.Take(20).ToList();
        var numericCount = sample.Count(row => GetNumericValue(row, column).HasValue);
        return numericCount > sample.Count * 0.5;
    }

    private static int CountDistinct(QueryResult result, string column)
    {
        return result.Rows
            .Select(row => GetStringValue(row, column))
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
}
