using System.Globalization;
using Aion.Components.Querying.Results;

namespace Aion.Components.Querying.Editing;

/// <summary>
/// The text a grid cell editor starts from, and what the text it commits means for the cell. Edits run as SQL
/// literals, so numbers and dates are written the way the database reads them rather than in the user's culture.
/// </summary>
public static class CellEditText
{
    /// <summary>
    /// The editor's starting text for a stored value, or null for NULL so the editor can tell it from empty text.
    /// </summary>
    public static string? From(object? value, string? columnType = null) => value switch
    {
        null or DBNull => null,
        string text => text,
        bool or DateTime or DateTimeOffset or DateOnly or TimeOnly or TimeSpan
            => ResultValueFormatter.Display(value, columnType, CultureInfo.InvariantCulture),
        _ => ResultValueFormatter.Full(value, CultureInfo.InvariantCulture)
    };

    /// <summary>
    /// Binary values can't be written back through a text box, so their cells stay read-only.
    /// </summary>
    public static bool CanEdit(object? value) => value is not byte[];

    /// <summary>
    /// The value a committed edit stores. Text that still reads as the original keeps the original value, so
    /// opening an editor and leaving it, or retyping the same number, never records a change.
    /// </summary>
    public static object? Resolve(string? text, object? original, string? columnType = null)
    {
        if (text is null)
        {
            return original is null or DBNull ? original : null;
        }

        return original is not (null or DBNull) && text == From(original, columnType) ? original : text;
    }
}
