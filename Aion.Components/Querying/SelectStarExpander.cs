using System.Text.RegularExpressions;

namespace Aion.Components.Querying;

public static partial class SelectStarExpander
{
    [GeneratedRegex(
        @"SELECT\s+\*\s+FROM\s+(?:[""'\[]?(\w+)[""'\]]?\.)?[""'\[]?(\w+)[""'\]]?",
        RegexOptions.IgnoreCase)]
    private static partial Regex SelectStarPattern();

    public static string BuildExpandedSelect(List<string> columnNames, string? schema, string table)
    {
        var columns = string.Join(", ", columnNames);
        var tableName = string.IsNullOrEmpty(schema) ? table : $"{schema}.{table}";
        return $"SELECT {columns} FROM {tableName}";
    }

    public static bool TryParse(string sql, out string? schema, out string? table)
    {
        schema = null;
        table = null;

        if (string.IsNullOrWhiteSpace(sql))
            return false;

        var match = SelectStarPattern().Match(sql);
        if (!match.Success)
            return false;

        schema = match.Groups[1].Success && match.Groups[1].Length > 0
            ? match.Groups[1].Value
            : null;
        table = match.Groups[2].Value;
        return true;
    }
}
