namespace Aion.Contracts.Queries;

/// <summary>A span on one line of SQL, 1-based, with an exclusive end column as editor ranges expect.</summary>
internal readonly record struct SqlSpan(int Line, int Column, int EndColumn);

/// <summary>
/// Finds where a failed statement went wrong in the SQL that was run. An engine-reported position
/// wins; otherwise the text or token the message names is looked up, since SQLite and MySQL rarely
/// say where they failed.
/// </summary>
internal static class SqlErrorLocator
{
    public static SqlSpan? Locate(
        string sql,
        QueryErrorKind kind,
        string? token,
        int? position,
        int statementOffset,
        int? line,
        string? quotedText)
    {
        if (position is > 0 && FromPosition(sql, position.Value, statementOffset, token) is { } fromPosition)
            return fromPosition;

        if (!string.IsNullOrEmpty(quotedText) && FromQuotedText(sql, quotedText, line, token) is { } fromQuote)
            return fromQuote;

        if (line is { } lineNumber)
            return OnLine(sql, lineNumber, token);

        return token == null ? null : FromToken(sql, kind, token);
    }

    private static SqlSpan? FromPosition(string sql, int position, int statementOffset, string? token)
    {
        if (statementOffset < 0 || statementOffset > sql.Length) return null;

        var index = AdvanceCharacters(sql, statementOffset, position - 1);
        if (index >= sql.Length || sql[index..].All(char.IsWhiteSpace))
            return AtEndOfInput(sql);

        return ToSpan(sql, index, ExtentAt(sql, index, token));
    }

    /// <summary>
    /// PostgreSQL counts characters, but .NET strings and Monaco columns count UTF-16 units, which
    /// differ once the SQL holds a character outside the Basic Multilingual Plane.
    /// </summary>
    private static int AdvanceCharacters(string sql, int start, int characters)
    {
        var index = start;
        for (var remaining = characters; remaining > 0 && index < sql.Length; remaining--)
        {
            index += char.IsHighSurrogate(sql[index]) && index + 1 < sql.Length && char.IsLowSurrogate(sql[index + 1]) ? 2 : 1;
        }

        return index;
    }

    private static SqlSpan? AtEndOfInput(string sql)
    {
        var last = sql.Length - 1;
        while (last >= 0 && char.IsWhiteSpace(sql[last])) last--;
        if (last < 0) return null;

        var start = last;
        while (start > 0 && IsIdentifierChar(sql[start]) && IsIdentifierChar(sql[start - 1])) start--;

        return ToSpan(sql, start, last - start + 1);
    }

    private static SqlSpan? FromQuotedText(string sql, string quotedText, int? line, string? token)
    {
        var lineStart = line is { } lineNumber ? LineBounds(sql, lineNumber)?.Start ?? 0 : 0;
        var index = sql.IndexOf(quotedText, lineStart, StringComparison.Ordinal);
        if (index < 0) index = sql.IndexOf(quotedText, StringComparison.Ordinal);
        if (index < 0) return null;

        return ToSpan(sql, index, token?.Length ?? ExtentAt(sql, index, token));
    }

    private static SqlSpan? OnLine(string sql, int line, string? token)
    {
        if (LineBounds(sql, line) is not { } bounds) return null;
        var (start, end) = bounds;

        if (token != null)
        {
            // SQL Server reports the line a statement starts on for some errors, so a name that is not
            // on that line is looked for in the rest of the statement.
            foreach (var searchEnd in new[] { end, sql.Length })
            {
                foreach (var candidate in Candidates(token))
                {
                    var index = FindWord(sql, candidate, start, searchEnd);
                    if (index >= 0) return ToSpan(sql, index, candidate.Length);
                }
            }
        }

        var first = start;
        while (first < end && char.IsWhiteSpace(sql[first])) first++;
        var last = end - 1;
        while (last >= first && char.IsWhiteSpace(sql[last])) last--;

        return first > last ? new SqlSpan(line, 1, 2) : ToSpan(sql, first, last - first + 1);
    }

    private static SqlSpan? FromToken(string sql, QueryErrorKind kind, string token)
    {
        foreach (var candidate in Candidates(token))
        {
            var first = FindWord(sql, candidate, 0, sql.Length);
            if (first < 0) continue;

            // A syntax error names punctuation or keywords that can appear anywhere, so a guess is
            // only made when the token is unambiguous. Every use of an unknown name is equally wrong.
            if (kind == QueryErrorKind.Syntax && FindWord(sql, candidate, first + candidate.Length, sql.Length) >= 0)
                return null;

            return ToSpan(sql, first, candidate.Length);
        }

        return null;
    }

    /// <summary>
    /// The name as reported, then its last part, because engines qualify names the user did not
    /// (MySQL reports "shop.prodcts" for a bare "prodcts").
    /// </summary>
    private static IEnumerable<string> Candidates(string token)
    {
        yield return token;

        var lastDot = token.LastIndexOf('.');
        if (lastDot > 0 && lastDot < token.Length - 1)
            yield return token[(lastDot + 1)..];
    }

    /// <summary>
    /// Finds <paramref name="word"/> case-insensitively (PostgreSQL folds unquoted names to lower
    /// case) where it is not part of a longer identifier.
    /// </summary>
    private static int FindWord(string sql, string word, int start, int end)
    {
        var index = start;
        while (index <= end - word.Length)
        {
            var found = sql.IndexOf(word, index, end - index, StringComparison.OrdinalIgnoreCase);
            if (found < 0) return -1;

            var boundedBefore = found == 0 || !IsIdentifierChar(sql[found - 1]) || !IsIdentifierChar(word[0]);
            var after = found + word.Length;
            var boundedAfter = after >= sql.Length || !IsIdentifierChar(sql[after]) || !IsIdentifierChar(word[^1]);
            if (boundedBefore && boundedAfter) return found;

            index = found + 1;
        }

        return -1;
    }

    private static int ExtentAt(string sql, int index, string? token)
    {
        var closing = sql[index] switch
        {
            '"' => '"',
            '`' => '`',
            '\'' => '\'',
            '[' => ']',
            _ => '\0'
        };
        if (closing != '\0')
        {
            var close = sql.IndexOf(closing, index + 1);
            return close < 0 ? 1 : close - index + 1;
        }

        if (IsIdentifierChar(sql[index]))
        {
            var end = index;
            while (end < sql.Length && (IsIdentifierChar(sql[end])
                                        || (sql[end] == '.' && end + 1 < sql.Length && IsIdentifierChar(sql[end + 1]))))
            {
                end++;
            }

            return end - index;
        }

        return token != null && sql.AsSpan(index).StartsWith(token, StringComparison.OrdinalIgnoreCase) ? token.Length : 1;
    }

    private static (int Start, int End)? LineBounds(string sql, int line)
    {
        if (line < 1) return null;

        var start = 0;
        for (var current = 1; current < line; current++)
        {
            var newline = sql.IndexOf('\n', start);
            if (newline < 0) return null;
            start = newline + 1;
        }

        var end = sql.IndexOf('\n', start);
        end = end < 0 ? sql.Length : end;
        if (end > start && sql[end - 1] == '\r') end--;

        return (start, end);
    }

    private static SqlSpan ToSpan(string sql, int index, int length)
    {
        var lineStart = index == 0 ? 0 : sql.LastIndexOf('\n', index - 1) + 1;
        var line = 1;
        for (var i = 0; i < lineStart; i++)
        {
            if (sql[i] == '\n') line++;
        }

        var lineEnd = sql.IndexOf('\n', index);
        lineEnd = lineEnd < 0 ? sql.Length : lineEnd;
        if (lineEnd > index && sql[lineEnd - 1] == '\r') lineEnd--;

        var end = Math.Max(index + 1, Math.Min(index + Math.Max(length, 1), lineEnd));
        return new SqlSpan(line, index - lineStart + 1, end - lineStart + 1);
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '$' or '#' or '@';
}
