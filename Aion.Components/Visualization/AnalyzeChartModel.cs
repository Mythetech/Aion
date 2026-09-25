using System.Globalization;

namespace Aion.Components.Visualization;

public enum AnalyzeChartType
{
    HorizontalBar,
    VerticalBar,
    Line,
    Table
}

public sealed record ChartDatum(string Label, double Value, string ValueText);

/// <summary>
/// Everything a chart view needs to draw an aggregation: formatted items, a shared value scale and the headings.
/// </summary>
public sealed class AnalyzeChartModel
{
    // Beyond this many bars or points the marks are too thin to read; the table view still lists every group.
    public const int MaxChartItems = 100;

    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public required string SeriesName { get; init; }

    public required string GroupByColumn { get; init; }

    public required IReadOnlyList<ChartDatum> Items { get; init; }

    public required IReadOnlyList<ChartDatum> AllItems { get; init; }

    public required ChartScale Scale { get; init; }

    public bool IsTruncated => Items.Count < AllItems.Count;

    public static AnalyzeChartModel? Create(AggregationResult aggregation, CultureInfo? culture = null, int maxItems = MaxChartItems)
    {
        if (aggregation.Labels.Length == 0)
            return null;

        culture ??= CultureInfo.CurrentCulture;

        var allItems = aggregation.Labels
            .Zip(aggregation.Values, (label, value) => new ChartDatum(label, value, ChartText.FormatValue(value, culture)))
            .ToList();
        var items = allItems.Take(maxItems).ToList();
        var seriesName = ChartText.SeriesName(aggregation.Function, aggregation.MeasureColumn);

        return new AnalyzeChartModel
        {
            Title = ChartText.Title(aggregation.Function, aggregation.MeasureColumn, aggregation.GroupByColumn),
            Subtitle = string.Join(" · ",
                seriesName,
                $"{allItems.Count.ToString("N0", culture)} {ChartText.Pluralize(ChartText.Humanize(aggregation.GroupByColumn), allItems.Count)}",
                "from current result"),
            SeriesName = seriesName,
            GroupByColumn = aggregation.GroupByColumn,
            Items = items,
            AllItems = allItems,
            Scale = ChartScale.Create(items.Select(i => i.Value).ToList())
        };
    }
}

public static class ChartText
{
    public static string FormatValue(double value, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;

        if (Math.Abs(value - Math.Round(value)) < 1e-9)
            return value.ToString("N0", culture);

        // Two decimals would print tiny averages or ratios as 0.00.
        if (Math.Abs(value) < 0.01)
            return value.ToString("G2", culture);

        return value.ToString("N2", culture);
    }

    public static string FunctionName(AggregateFunction function) => function switch
    {
        AggregateFunction.Avg => "Average",
        _ => function.ToString()
    };

    public static string SeriesName(AggregateFunction function, string? measureColumn)
        => function == AggregateFunction.Count || string.IsNullOrEmpty(measureColumn)
            ? "Count of rows"
            : $"{FunctionName(function)} of {measureColumn}";

    public static string Title(AggregateFunction function, string? measureColumn, string groupByColumn)
    {
        var group = Humanize(groupByColumn);
        if (function == AggregateFunction.Count || string.IsNullOrEmpty(measureColumn))
            return $"Row count by {group}";

        var measure = Humanize(measureColumn);
        return function switch
        {
            AggregateFunction.Sum => $"{Capitalize(measure)} by {group}",
            AggregateFunction.Avg => $"Average {measure} by {group}",
            AggregateFunction.Min => $"Minimum {measure} by {group}",
            AggregateFunction.Max => $"Maximum {measure} by {group}",
            _ => $"{Capitalize(measure)} by {group}"
        };
    }

    public static string Humanize(string columnName)
        => string.Join(' ', columnName.Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries));

    public static string Pluralize(string noun, int count)
    {
        if (count == 1 || noun.Length == 0)
            return noun;

        if (!char.IsLetter(noun[^1]))
            return $"{noun} values";

        var lower = noun.ToLowerInvariant();
        var shouting = !noun.Any(char.IsLower);

        if (lower.EndsWith("ss") || lower.EndsWith("us") || lower.EndsWith('x') || lower.EndsWith('z')
            || lower.EndsWith("ch") || lower.EndsWith("sh"))
            return noun + Suffix("es", shouting);

        // Column names like "orders" or "sales" are usually already plural.
        if (lower.EndsWith('s'))
            return noun;

        if (lower.EndsWith('y') && lower.Length > 1 && !"aeiou".Contains(lower[^2]))
            return noun[..^1] + Suffix("ies", shouting);

        return noun + Suffix("s", shouting);
    }

    private static string Suffix(string suffix, bool upperCase) => upperCase ? suffix.ToUpperInvariant() : suffix;

    private static string Capitalize(string text)
        => text.Length == 0 ? text : char.ToUpper(text[0], CultureInfo.InvariantCulture) + text[1..];
}
