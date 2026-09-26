using System.Globalization;
using System.Numerics;

namespace Aion.Contracts.Database.Dialects;

/// <summary>
/// Renders identifiers and values as SQL text for one database engine. Grid edits are executed as
/// literal SQL rather than parameterized commands so the preview can show exactly what will run,
/// which makes every quoting rule here a correctness and injection boundary.
/// </summary>
public abstract class SqlDialect
{
    public abstract string QuoteIdentifier(string identifier);

    public string FormatLiteral(object? value) => value switch
    {
        null or DBNull => "NULL",
        string s => FormatString(s),
        char c => FormatString(c.ToString()),
        bool b => FormatBoolean(b),
        byte[] bytes => FormatBytes(bytes),
        sbyte or byte or short or ushort or int or uint or long or ulong or decimal or BigInteger
            => ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture),
        float f => float.IsFinite(f) ? f.ToString("R", CultureInfo.InvariantCulture) : FormatString(f.ToString(CultureInfo.InvariantCulture)),
        double d => double.IsFinite(d) ? d.ToString("R", CultureInfo.InvariantCulture) : FormatString(d.ToString(CultureInfo.InvariantCulture)),
        DateTime dt => FormatDateTime(dt),
        DateTimeOffset dto => FormatString(dto.ToString(DateTimePattern + "zzz", CultureInfo.InvariantCulture)),
        DateOnly date => FormatString(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        TimeOnly time => FormatString(FormatTimeOfDay(time.ToTimeSpan())),
        TimeSpan span => FormatString(FormatTimeOfDay(span)),
        Guid g => FormatString(g.ToString("D")),
        IFormattable formattable => FormatString(formattable.ToString(null, CultureInfo.InvariantCulture)),
        _ => FormatString(value.ToString() ?? string.Empty)
    };

    /// <summary>
    /// Builds a WHERE predicate that matches the row identified by the given key values.
    /// </summary>
    public string BuildKeyPredicate(IEnumerable<ColumnValue> keyValues)
    {
        var conditions = keyValues
            .Select(k => k.Value is null or DBNull
                ? $"{QuoteIdentifier(k.Column)} IS NULL"
                : $"{QuoteIdentifier(k.Column)} = {FormatLiteral(k.Value)}")
            .ToList();

        if (conditions.Count == 0)
        {
            throw new ArgumentException("At least one key value is required to target a single row.", nameof(keyValues));
        }

        return string.Join(" AND ", conditions);
    }

    public string BuildAssignments(IEnumerable<ColumnValue> values)
    {
        var assignments = values
            .Select(v => $"{QuoteIdentifier(v.Column)} = {FormatLiteral(v.Value)}")
            .ToList();

        if (assignments.Count == 0)
        {
            throw new ArgumentException("At least one column value is required for an update.", nameof(values));
        }

        return string.Join(", ", assignments);
    }

    public string QualifyTable(string? schema, string table) =>
        string.IsNullOrEmpty(schema) ? QuoteIdentifier(table) : $"{QuoteIdentifier(schema)}.{QuoteIdentifier(table)}";

    /// <summary>
    /// Builds a SELECT of every column of an already quoted table, optionally filtered and cut to the first rows
    /// with the engine's own row limit.
    /// </summary>
    public virtual string SelectRows(string qualifiedTable, string? predicate = null, int? limit = null) =>
        $"SELECT * FROM {qualifiedTable}{WhereClause(predicate)}{(limit is { } count ? $"\nLIMIT {count.ToString(CultureInfo.InvariantCulture)}" : "")};";

    /// <summary>
    /// A CREATE TABLE statement with placeholder names, a generated key and the engine's usual types, for the
    /// user to edit and run. It goes in the engine's default schema where the engine has one.
    /// </summary>
    public abstract string CreateTableTemplate();

    protected static string WhereClause(string? predicate) =>
        string.IsNullOrWhiteSpace(predicate) ? "" : $"\nWHERE {predicate}";

    public string BuildColumnList(IEnumerable<ColumnValue> values) =>
        string.Join(", ", values.Select(v => QuoteIdentifier(v.Column)));

    public string BuildValueList(IEnumerable<ColumnValue> values) =>
        string.Join(", ", values.Select(v => FormatLiteral(v.Value)));

    protected abstract string FormatString(string value);

    protected abstract string FormatBoolean(bool value);

    protected abstract string FormatBytes(byte[] value);

    protected virtual int FractionalSecondDigits => 7;

    protected virtual bool UseIsoDateTimeSeparator => true;

    protected string DateTimePattern =>
        $"yyyy-MM-dd{(UseIsoDateTimeSeparator ? "'T'" : " ")}HH:mm:ss.{new string('F', FractionalSecondDigits)}";

    protected virtual string FormatDateTime(DateTime value) =>
        FormatString(value.ToString(DateTimePattern, CultureInfo.InvariantCulture));

    private string FormatTimeOfDay(TimeSpan value)
    {
        var sign = value < TimeSpan.Zero ? "-" : "";
        var duration = value.Duration();
        var fraction = (duration.Ticks % TimeSpan.TicksPerSecond)
            .ToString("D7", CultureInfo.InvariantCulture)[..FractionalSecondDigits]
            .TrimEnd('0');

        return string.Create(CultureInfo.InvariantCulture,
            $"{sign}{(long)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}{(fraction.Length > 0 ? "." + fraction : "")}");
    }
}
