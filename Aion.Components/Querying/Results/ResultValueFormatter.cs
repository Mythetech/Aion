using System.Globalization;
using System.Numerics;

namespace Aion.Components.Querying.Results;

/// <summary>
/// Turns fetched values into the text the results grid shows. Floating point values are rounded so binary
/// noise such as 259.96999999999997 reads as 259.97; integers and decimals are exact, so they are never
/// rounded. Copying and exporting keep using the full value, which <see cref="Full"/> gives for cell titles.
/// </summary>
public static class ResultValueFormatter
{
    // Below this a fixed six decimals would read as 0, so the value keeps its significant digits instead.
    private const double SmallestFixed = 0.5e-6;

    // Past this a double no longer holds every integer digit, so fixed notation would print invented digits.
    private const double LargestFixed = 1e15;

    private const int MaxBinaryBytes = 32;

    private const string FixedFormat = "0.######";
    private const string SignificantFormat = "G6";

    private static readonly HashSet<string> NumericTypeNames = new(StringComparer.Ordinal)
    {
        "int", "integer", "bigint", "smallint", "tinyint", "mediumint",
        "int2", "int4", "int8", "serial", "bigserial", "smallserial",
        "real", "float", "float4", "float8", "double", "double precision",
        "numeric", "decimal", "number", "money", "smallmoney"
    };

    /// <summary>
    /// The text shown for a value, or null for NULL so the grid can mark it as missing rather than empty.
    /// </summary>
    public static string? Display(object? value, string? columnType = null, IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.CurrentCulture;

        return value switch
        {
            null or DBNull => null,
            string text => text,
            double d => FormatDouble(d, provider),
            float f => FormatDouble(double.Parse(f.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture), provider),
            bool b => b ? "true" : "false",
            DateTime dateTime => IsDateType(columnType)
                ? dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : dateTime.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
            DateTimeOffset offset => offset.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF zzz", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
            TimeSpan span => span.ToString("c", CultureInfo.InvariantCulture),
            byte[] bytes => FormatBinary(bytes),
            IFormattable formattable => formattable.ToString(null, provider),
            _ => value.ToString()
        };
    }

    /// <summary>
    /// The value with every digit, as Copy Cell and the exports write it.
    /// </summary>
    public static string? Full(object? value, IFormatProvider? provider = null) => value switch
    {
        null or DBNull => null,
        string text => text,
        IFormattable formattable => formattable.ToString(null, provider ?? CultureInfo.CurrentCulture),
        _ => value.ToString()
    };

    public static bool IsNumber(object? value) => value is byte or sbyte or short or ushort or int or uint or long or ulong
        or float or double or decimal or BigInteger;

    /// <summary>
    /// Whether a column type, as a provider names it ("integer", "NUMERIC(10,2)", "INT UNSIGNED"), holds numbers.
    /// </summary>
    public static bool IsNumericType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return false;

        var name = type.Trim().ToLowerInvariant();
        var open = name.IndexOf('(');
        if (open >= 0)
            name = name[..open].TrimEnd();

        if (NumericTypeNames.Contains(name))
            return true;

        // MySQL appends modifiers such as "unsigned" after the type name.
        var space = name.IndexOf(' ');
        return space > 0 && NumericTypeNames.Contains(name[..space]);
    }

    /// <summary>
    /// Whether a column should be aligned as numbers. The values decide when there are any, because SQLite
    /// columns take whatever type each value has and a MySQL tinyint(1) arrives as booleans.
    /// </summary>
    public static bool IsNumericColumn(string? type, IEnumerable<object?> sample)
    {
        var typeIsNumeric = IsNumericType(type);
        var sawValue = false;

        foreach (var value in sample)
        {
            if (value is null or DBNull)
                continue;

            sawValue = true;
            var numberLike = IsNumber(value)
                || (typeIsNumeric && value is string text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _));
            if (!numberLike)
                return false;
        }

        return sawValue || typeIsNumeric;
    }

    private static bool IsDateType(string? type) =>
        string.Equals(type?.Trim(), "date", StringComparison.OrdinalIgnoreCase);

    private static string FormatDouble(double value, IFormatProvider provider)
    {
        if (value == 0)
            return "0";

        if (!double.IsFinite(value) || Math.Abs(value) >= LargestFixed)
            return value.ToString(provider);

        if (Math.Abs(value) < SmallestFixed)
            return value.ToString(SignificantFormat, provider);

        return value.ToString(FixedFormat, provider);
    }

    private static string FormatBinary(byte[] bytes)
    {
        var shown = Convert.ToHexString(bytes, 0, Math.Min(bytes.Length, MaxBinaryBytes));
        return bytes.Length > MaxBinaryBytes
            ? $"0x{shown}… ({bytes.Length} bytes)"
            : $"0x{shown}";
    }
}
