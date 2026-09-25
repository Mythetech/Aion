namespace Aion.Contracts.Database.Dialects;

/// <summary>
/// SQLite literal and identifier rules. SQLite strings have no backslash escapes.
/// </summary>
public sealed class SqliteDialect : SqlDialect
{
    public static SqliteDialect Instance { get; } = new();

    public override string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"")}\"";

    protected override string FormatString(string value) => $"'{value.Replace("'", "''")}'";

    protected override string FormatBoolean(bool value) => value ? "1" : "0";

    protected override string FormatBytes(byte[] value) => $"X'{Convert.ToHexString(value)}'";

    // SQLite compares dates as text, so match the space separated form SQLite and Microsoft.Data.Sqlite store.
    protected override bool UseIsoDateTimeSeparator => false;
}
