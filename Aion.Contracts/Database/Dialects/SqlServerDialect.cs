namespace Aion.Contracts.Database.Dialects;

/// <summary>
/// SQL Server literal and identifier rules.
/// </summary>
public sealed class SqlServerDialect : SqlDialect
{
    public static SqlServerDialect Instance { get; } = new();

    public override string QuoteIdentifier(string identifier) =>
        $"[{identifier.Replace("]", "]]")}]";

    protected override string FormatString(string value) => $"N'{value.Replace("'", "''")}'";

    protected override string FormatBoolean(bool value) => value ? "1" : "0";

    protected override string FormatBytes(byte[] value) => $"0x{Convert.ToHexString(value)}";
}
