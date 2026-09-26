using System.Text.RegularExpressions;

namespace Aion.Components.Connections;

public enum SchemaChange
{
    None,
    Tables,
    Databases
}

/// <summary>
/// Tells from SQL text whether running it changed what the schema tree shows. It only reads the keywords
/// that start each statement, so a false positive costs one extra refresh and a miss leaves the manual Refresh.
/// </summary>
public static partial class SchemaChangeDetector
{
    public static SchemaChange Detect(string sql)
    {
        var text = Comments().Replace(sql, " ");

        if (DatabaseStatement().IsMatch(text))
            return SchemaChange.Databases;

        return SchemaStatement().IsMatch(text) ? SchemaChange.Tables : SchemaChange.None;
    }

    [GeneratedRegex(@"--[^\n]*|/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"(?:^|;)\s*(?:CREATE|ALTER|DROP|RENAME)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SchemaStatement();

    // MySQL treats SCHEMA as a synonym for DATABASE, and a PostgreSQL schema changes which tables are listed.
    [GeneratedRegex(@"(?:^|;)\s*(?:CREATE|DROP)\s+(?:DATABASE|SCHEMA)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DatabaseStatement();
}
