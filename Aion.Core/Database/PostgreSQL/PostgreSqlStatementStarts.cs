using System.Text.RegularExpressions;

namespace Aion.Core.Database.PostgreSQL;

/// <summary>
/// Finds where each statement of a multi-statement command starts, the way Npgsql splits it before
/// sending: at semicolons outside strings, quoted identifiers, dollar quotes and comments, with the
/// whitespace after each semicolon dropped. PostgreSQL reports error positions from that start.
/// </summary>
public static partial class PostgreSqlStatementStarts
{
    public static int? StartOf(string sql, int statementIndex)
    {
        if (statementIndex < 0) return null;
        if (statementIndex == 0) return 0;

        var statement = 0;
        var index = 0;
        while (index < sql.Length)
        {
            switch (sql[index])
            {
                case '\'':
                    index = SkipQuoted(sql, index, '\'', backslashEscapes: IsEscapeString(sql, index));
                    continue;
                case '"':
                    index = SkipQuoted(sql, index, '"', backslashEscapes: false);
                    continue;
                case '-' when Next(sql, index) == '-':
                    index = SkipLineComment(sql, index);
                    continue;
                case '/' when Next(sql, index) == '*':
                    index = SkipBlockComment(sql, index);
                    continue;
                case '$' when DollarTag(sql, index) is { } tag:
                    var close = sql.IndexOf(tag, index + tag.Length, StringComparison.Ordinal);
                    index = close < 0 ? sql.Length : close + tag.Length;
                    continue;
                case ';':
                    index++;
                    while (index < sql.Length && char.IsWhiteSpace(sql[index])) index++;
                    if (index >= sql.Length) return null;
                    if (++statement == statementIndex) return index;
                    continue;
                default:
                    index++;
                    continue;
            }
        }

        return null;
    }

    private static char Next(string sql, int index) => index + 1 < sql.Length ? sql[index + 1] : '\0';

    private static bool IsEscapeString(string sql, int quote) =>
        quote > 0 && sql[quote - 1] is 'E' or 'e' && (quote == 1 || !IsIdentifierChar(sql[quote - 2]));

    private static int SkipQuoted(string sql, int open, char quote, bool backslashEscapes)
    {
        var index = open + 1;
        while (index < sql.Length)
        {
            var c = sql[index];
            if (backslashEscapes && c == '\\')
            {
                index += 2;
                continue;
            }

            if (c == quote)
            {
                if (Next(sql, index) != quote) return index + 1;
                index += 2;
                continue;
            }

            index++;
        }

        return sql.Length;
    }

    private static int SkipLineComment(string sql, int start)
    {
        var newline = sql.IndexOf('\n', start);
        return newline < 0 ? sql.Length : newline + 1;
    }

    private static int SkipBlockComment(string sql, int start)
    {
        var depth = 0;
        var index = start;
        while (index < sql.Length)
        {
            if (sql[index] == '/' && Next(sql, index) == '*')
            {
                depth++;
                index += 2;
            }
            else if (sql[index] == '*' && Next(sql, index) == '/')
            {
                index += 2;
                if (--depth == 0) return index;
            }
            else
            {
                index++;
            }
        }

        return sql.Length;
    }

    private static string? DollarTag(string sql, int index)
    {
        if (index > 0 && IsIdentifierChar(sql[index - 1])) return null;

        var match = DollarTagPattern().Match(sql, index);
        return match.Success && match.Index == index ? match.Value : null;
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '$';

    [GeneratedRegex(@"\G\$(?:[A-Za-z_][A-Za-z0-9_]*)?\$")]
    private static partial Regex DollarTagPattern();
}
