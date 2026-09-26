using System.Text;

namespace Aion.Contracts.Database.Dialects;

/// <summary>
/// MySQL literal and identifier rules. Double quotes are string delimiters under the default sql_mode,
/// so identifiers must use backticks.
/// </summary>
public sealed class MySqlDialect : SqlDialect
{
    public static MySqlDialect Instance { get; } = new();

    public override string QuoteIdentifier(string identifier) =>
        $"`{identifier.Replace("`", "``")}`";

    protected override string FormatString(string value)
    {
        // Backslash is an escape character under the default sql_mode. Quotes are doubled rather than
        // backslash escaped so the literal still terminates correctly under NO_BACKSLASH_ESCAPES.
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('\'');
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\'':
                    builder.Append("''");
                    break;
                case '\0':
                    builder.Append("\\0");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }
        builder.Append('\'');
        return builder.ToString();
    }

    protected override string FormatBoolean(bool value) => value ? "TRUE" : "FALSE";

    protected override string FormatBytes(byte[] value) => $"X'{Convert.ToHexString(value)}'";

    protected override int FractionalSecondDigits => 6;

    protected override bool UseIsoDateTimeSeparator => false;

    // The connection's database is the only schema level MySQL has, so the table is not qualified.
    public override string CreateTableTemplate() =>
        """
        CREATE TABLE `new_table` (
            `id` int NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `name` varchar(100) NOT NULL,
            `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP
        );
        """;
}
