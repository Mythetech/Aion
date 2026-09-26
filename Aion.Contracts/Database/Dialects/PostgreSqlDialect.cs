namespace Aion.Contracts.Database.Dialects;

/// <summary>
/// PostgreSQL and PGlite literal and identifier rules.
/// </summary>
public sealed class PostgreSqlDialect : SqlDialect
{
    public static PostgreSqlDialect Instance { get; } = new();

    public override string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"")}\"";

    protected override string FormatString(string value)
    {
        // A plain '...' literal only treats backslashes literally when standard_conforming_strings is on.
        // An E'...' literal with doubled backslashes means the same text under either setting, so a
        // value like \' can never end the literal early.
        if (value.Contains('\\'))
        {
            return $"E'{value.Replace("\\", "\\\\").Replace("'", "''")}'";
        }

        return $"'{value.Replace("'", "''")}'";
    }

    protected override string FormatBoolean(bool value) => value ? "TRUE" : "FALSE";

    protected override string FormatBytes(byte[] value) => $"'\\x{Convert.ToHexString(value)}'::bytea";

    protected override int FractionalSecondDigits => 6;

    public override string CreateTableTemplate() =>
        """
        CREATE TABLE "public"."new_table" (
            "id" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            "name" text NOT NULL,
            "created_at" timestamptz NOT NULL DEFAULT now()
        );
        """;

    // Npgsql reads timestamptz as UTC; without the Z the server would reinterpret it in the session time zone.
    protected override string FormatDateTime(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? FormatString(value.ToString(DateTimePattern + "'Z'", System.Globalization.CultureInfo.InvariantCulture))
            : base.FormatDateTime(value);
}
