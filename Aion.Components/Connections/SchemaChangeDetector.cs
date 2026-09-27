using System.Text.RegularExpressions;

namespace Aion.Components.Connections;

/// <summary>
/// What a statement changed of what the schema tree shows, from least to most to reload: each value includes
/// everything before it, since listing the tables again also counts their rows.
/// </summary>
public enum SchemaChange
{
    None,
    RowCounts,
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

        if (SchemaStatement().IsMatch(text))
            return SchemaChange.Tables;

        return RowStatement().IsMatch(text) ? SchemaChange.RowCounts : SchemaChange.None;
    }

    [GeneratedRegex(@"--[^\n]*|/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"(?:^|;)\s*(?:CREATE|ALTER|DROP|RENAME)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SchemaStatement();

    // MySQL treats SCHEMA as a synonym for DATABASE, and a PostgreSQL schema changes which tables are listed.
    [GeneratedRegex(@"(?:^|;)\s*(?:CREATE|DROP)\s+(?:DATABASE|SCHEMA)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DatabaseStatement();

    [GeneratedRegex(@"(?:^|;)\s*(?:INSERT|UPDATE|DELETE|MERGE|REPLACE|TRUNCATE)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RowStatement();
}
