using System.Globalization;

namespace Aion.Components.Visualization;

/// <summary>
/// A linear value axis whose bounds and ticks are rounded to readable steps (1, 2, 2.5 or 5 times a power of ten).
/// </summary>
public sealed class ChartScale
{
    private ChartScale(double min, double max, double step, IReadOnlyList<double> ticks)
    {
        Min = min;
        Max = max;
        Step = step;
        Ticks = ticks;
    }

    public double Min { get; }

    public double Max { get; }

    public double Step { get; }

    public IReadOnlyList<double> Ticks { get; }

    public double BaselinePercent => Percent(0);

    public static ChartScale Create(IReadOnlyCollection<double> values, int targetTickCount = 6)
    {
        var dataMin = Math.Min(0, values.Count == 0 ? 0 : values.Min());
        var dataMax = Math.Max(0, values.Count == 0 ? 0 : values.Max());
        var range = dataMax - dataMin;
        if (range <= 0)
            range = 1;

        var step = NiceStep(range / Math.Max(1, targetTickCount));

        // Counts and other whole number results shouldn't get fractional ticks like 0.2.
        if (values.All(IsWhole))
            step = Math.Max(1, Math.Ceiling(step));

        var min = Math.Round(Math.Floor(dataMin / step) * step, 10);
        var max = Math.Round(Math.Ceiling(dataMax / step) * step, 10);
        if (max <= min)
            max = min + step;

        var ticks = new List<double>();
        var count = (int)Math.Round((max - min) / step);
        for (var i = 0; i <= count; i++)
            ticks.Add(Math.Round(min + i * step, 10));

        return new ChartScale(min, max, step, ticks);
    }

    public double Percent(double value)
        => Math.Clamp((value - Min) / (Max - Min) * 100, 0, 100);

    public string FormatTick(double value, CultureInfo? culture = null)
        => value.ToString("N" + DecimalsFor(Step), culture ?? CultureInfo.CurrentCulture);

    private static double NiceStep(double roughStep)
    {
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(roughStep)));
        var residual = roughStep / magnitude;
        var nice = residual switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 2.5 => 2.5,
            <= 5 => 5,
            _ => 10
        };
        return nice * magnitude;
    }

    private static int DecimalsFor(double step)
    {
        for (var decimals = 0; decimals < 8; decimals++)
        {
            var scaled = step * Math.Pow(10, decimals);
            if (Math.Abs(scaled - Math.Round(scaled)) < 1e-6)
                return decimals;
        }

        return 8;
    }

    private static bool IsWhole(double value) => Math.Abs(value - Math.Round(value)) < 1e-9;
}
