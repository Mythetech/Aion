using System.Text;

namespace Aion.Components.Querying.Editing;

/// <summary>
/// The single table a query reads from, and the columns it selects (null for <c>*</c>).
/// </summary>
public record EditableQueryTarget(string? Schema, string Table, IReadOnlyList<string>? Columns);

public record EditableQueryParseResult(EditableQueryTarget? Target, string? Error)
{
    public bool Success => Target != null;
}

/// <summary>
/// Recognizes the only query shape edit mode can safely write back to: <c>SELECT [TOP n] * | col, ... FROM [schema.]table</c>
/// optionally followed by WHERE, ORDER BY, LIMIT, OFFSET or FETCH. Anything that could put rows from another table
/// into the grid, or rename a column, is rejected, because edits are written back by column name and primary key.
/// </summary>
public static class EditableQueryParser
{
    private static readonly HashSet<string> AllowedClauseStarts = new(StringComparer.OrdinalIgnoreCase)
    {
        "WHERE", "ORDER", "LIMIT", "OFFSET", "FETCH"
    };

    private static readonly HashSet<string> RejectedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "JOIN", "UNION", "INTERSECT", "EXCEPT", "GROUP", "HAVING", "INTO", "WINDOW"
    };

    private const string SimpleSelectRequired =
        "Edit mode requires a simple SELECT from a single table (no JOINs, aliases or expressions)";

    public static EditableQueryParseResult Parse(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return Fail(SimpleSelectRequired);
        }

        List<Token> tokens;
        try
        {
            tokens = Tokenize(sql);
        }
        catch (FormatException ex)
        {
            return Fail(ex.Message);
        }

        var position = 0;

        if (!IsWord(tokens, position, "SELECT"))
        {
            return Fail(SimpleSelectRequired);
        }
        position++;

        if (IsWord(tokens, position, "TOP") && !TrySkipTop(tokens, ref position))
        {
            return Fail(SimpleSelectRequired);
        }

        List<string>? columns = null;
        if (IsSymbol(tokens, position, "*"))
        {
            position++;
        }
        else
        {
            columns = [];
            while (true)
            {
                if (!IsIdentifier(tokens, position))
                {
                    return Fail(SimpleSelectRequired);
                }
                columns.Add(tokens[position].Text);
                position++;

                if (!IsSymbol(tokens, position, ","))
                {
                    break;
                }
                position++;
            }
        }

        if (!IsWord(tokens, position, "FROM"))
        {
            return Fail(SimpleSelectRequired);
        }
        position++;

        if (!IsIdentifier(tokens, position))
        {
            return Fail(SimpleSelectRequired);
        }

        string? schema = null;
        var table = tokens[position].Text;
        position++;

        if (IsSymbol(tokens, position, "."))
        {
            position++;
            if (!IsIdentifier(tokens, position))
            {
                return Fail(SimpleSelectRequired);
            }
            schema = table;
            table = tokens[position].Text;
            position++;
        }

        if (IsSymbol(tokens, position, "."))
        {
            return Fail("Edit mode only supports schema.table names, not names qualified with a database");
        }

        if (position < tokens.Count && !IsSymbol(tokens, position, ";"))
        {
            var next = tokens[position];
            if (next.Kind != TokenKind.Word || !AllowedClauseStarts.Contains(next.Text))
            {
                var joinsTables = IsSymbol(tokens, position, ",")
                    || tokens.Skip(position).Any(t => t.Kind == TokenKind.Word && t.Text.Equals("JOIN", StringComparison.OrdinalIgnoreCase));

                return Fail(joinsTables
                    ? "Edit mode does not support JOINs or multiple tables"
                    : "Edit mode does not support table aliases or table hints");
            }
        }

        for (var i = position; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind == TokenKind.Word && RejectedKeywords.Contains(token.Text))
            {
                return Fail($"Edit mode does not support {token.Text.ToUpperInvariant()} queries");
            }

            if (token.Kind == TokenKind.Symbol && token.Text == ";" && i != tokens.Count - 1)
            {
                return Fail("Edit mode requires a single statement");
            }
        }

        return new EditableQueryParseResult(new EditableQueryTarget(schema, table, columns), null);
    }

    private static EditableQueryParseResult Fail(string error) => new(null, error);

    private static bool TrySkipTop(List<Token> tokens, ref int position)
    {
        position++;
        if (IsSymbol(tokens, position, "("))
        {
            if (!IsKind(tokens, position + 1, TokenKind.Number) || !IsSymbol(tokens, position + 2, ")"))
            {
                return false;
            }
            position += 3;
            return true;
        }

        if (!IsKind(tokens, position, TokenKind.Number))
        {
            return false;
        }
        position++;
        return true;
    }

    private static bool IsWord(List<Token> tokens, int position, string word) =>
        position < tokens.Count
        && tokens[position].Kind == TokenKind.Word
        && tokens[position].Text.Equals(word, StringComparison.OrdinalIgnoreCase);

    private static bool IsSymbol(List<Token> tokens, int position, string symbol) =>
        position < tokens.Count
        && tokens[position].Kind == TokenKind.Symbol
        && tokens[position].Text == symbol;

    private static bool IsKind(List<Token> tokens, int position, TokenKind kind) =>
        position < tokens.Count && tokens[position].Kind == kind;

    private static bool IsIdentifier(List<Token> tokens, int position) =>
        position < tokens.Count
        && tokens[position].Kind is TokenKind.Word or TokenKind.QuotedIdentifier
        && !(tokens[position].Kind == TokenKind.Word && IsReservedWord(tokens[position].Text));

    private static bool IsReservedWord(string word) =>
        word.Equals("FROM", StringComparison.OrdinalIgnoreCase)
        || word.Equals("SELECT", StringComparison.OrdinalIgnoreCase)
        || word.Equals("DISTINCT", StringComparison.OrdinalIgnoreCase)
        || word.Equals("AS", StringComparison.OrdinalIgnoreCase)
        || AllowedClauseStarts.Contains(word)
        || RejectedKeywords.Contains(word);

    private enum TokenKind
    {
        Word,
        QuotedIdentifier,
        String,
        Number,
        Symbol
    }

    private readonly record struct Token(TokenKind Kind, string Text);

    private static List<Token> Tokenize(string sql)
    {
        var tokens = new List<Token>();
        var i = 0;

        while (i < sql.Length)
        {
            var c = sql[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '-' && Peek(sql, i + 1) == '-')
            {
                // MySQL only treats -- as a comment when whitespace follows, so 1--1 is arithmetic there.
                if (i + 2 < sql.Length && !char.IsWhiteSpace(sql[i + 2]))
                {
                    throw new FormatException("Edit mode needs a space after -- in comments");
                }

                while (i < sql.Length && sql[i] != '\n')
                {
                    i++;
                }
                continue;
            }

            if (c == '/' && Peek(sql, i + 1) == '*')
            {
                // MySQL runs the body of /*! ... */ comments, so they cannot be skipped as comments.
                if (Peek(sql, i + 2) == '!')
                {
                    throw new FormatException("Edit mode does not support MySQL executable comments");
                }

                var end = sql.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    throw new FormatException("The query has an unterminated comment");
                }
                i = end + 2;
                continue;
            }

            if (c == '\'' || c == '"' || c == '`' || c == '[')
            {
                var close = c == '[' ? ']' : c;
                i = ReadQuoted(sql, i, c, close, out var text);

                // MySQL lets a backslash escape the closing quote of '...' and "..." while other engines do
                // not, so where the literal ends, and what runs after it, would be ambiguous.
                if (c is '\'' or '"' && text.Contains('\\'))
                {
                    throw new FormatException("Edit mode does not support quoted text containing backslashes");
                }

                tokens.Add(new Token(c == '\'' ? TokenKind.String : TokenKind.QuotedIdentifier, text));
                continue;
            }

            if (char.IsDigit(c))
            {
                var start = i;
                while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '.'))
                {
                    i++;
                }
                tokens.Add(new Token(TokenKind.Number, sql[start..i]));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_' || sql[i] == '$'))
                {
                    i++;
                }
                tokens.Add(new Token(TokenKind.Word, sql[start..i]));
                continue;
            }

            if (c == '$')
            {
                throw new FormatException("Edit mode does not support dollar-quoted strings or parameters");
            }

            // # starts a comment in MySQL but is an operator in PostgreSQL.
            if (c == '#')
            {
                throw new FormatException("Edit mode does not support # comments or operators");
            }

            tokens.Add(new Token(TokenKind.Symbol, c.ToString()));
            i++;
        }

        return tokens;
    }

    private static char Peek(string sql, int index) => index < sql.Length ? sql[index] : '\0';

    private static int ReadQuoted(string sql, int start, char open, char close, out string text)
    {
        var builder = new StringBuilder();
        var i = start + 1;

        while (i < sql.Length)
        {
            if (sql[i] == close)
            {
                if (Peek(sql, i + 1) == close)
                {
                    builder.Append(close);
                    i += 2;
                    continue;
                }

                text = builder.ToString();
                return i + 1;
            }

            builder.Append(sql[i]);
            i++;
        }

        throw new FormatException(open == '\'' ? "The query has an unterminated string" : "The query has an unterminated identifier");
    }
}
