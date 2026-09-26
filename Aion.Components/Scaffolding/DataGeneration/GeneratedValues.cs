using System.Globalization;

namespace Aion.Components.Scaffolding.DataGeneration;

/// <summary>
/// Turns a generated value into one the column accepts, so the dialect writes a literal of the right kind:
/// a flag becomes 1 or 0 in an integer column, a moment keeps only its date in a date column, and text is
/// cut to the column's length instead of being rejected by the engine.
/// </summary>
public static class GeneratedValues
{
    // The form SQLite's CURRENT_TIMESTAMP writes, which its date functions and text comparisons expect.
    private const string TextDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    public static object? Fit(object? value, ColumnTypeShape type) => value is null ? null : type.Family switch
    {
        ColumnTypeFamily.Integer => ToInteger(value),
        ColumnTypeFamily.Decimal or ColumnTypeFamily.Float => ToNumber(value),
        ColumnTypeFamily.Boolean => ToBoolean(value),
        ColumnTypeFamily.Text => Shorten(ToText(value), type.MaxLength),
        ColumnTypeFamily.Date => value is DateTime date ? DateOnly.FromDateTime(date) : value,
        ColumnTypeFamily.Time => value is DateTime time ? TimeOnly.FromDateTime(time) : value,
        ColumnTypeFamily.DateTimeOffset => value is DateTime moment ? new DateTimeOffset(DateTime.SpecifyKind(moment, DateTimeKind.Unspecified), TimeSpan.Zero) : value,
        ColumnTypeFamily.Uuid => value is string text && Guid.TryParse(text, out var id) ? id : value,
        _ => value
    };

    private static object ToInteger(object value) => value switch
    {
        bool flag => flag ? 1L : 0L,
        string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
        string text when ToBoolean(text) is bool flag => flag ? 1L : 0L,
        _ => value
    };

    private static object ToNumber(object value) => value switch
    {
        bool flag => flag ? 1L : 0L,
        string text when decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) => number,
        _ => value
    };

    private static object ToBoolean(object value) => value switch
    {
        string text => text.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "y" or "t" => true,
            "false" or "0" or "no" or "n" or "f" => false,
            _ => text
        },
        long or int or short or byte => Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0,
        _ => value
    };

    private static string ToText(object value) => value switch
    {
        string text => text,
        bool flag => flag ? "true" : "false",
        DateTime moment => moment.ToString(TextDateTimeFormat, CultureInfo.InvariantCulture),
        Guid id => id.ToString("D"),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string Shorten(string text, int? maxLength) =>
        maxLength is { } length && text.Length > length ? text[..length] : text;
}
