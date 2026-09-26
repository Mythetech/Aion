using System.Text.RegularExpressions;

namespace Aion.Contracts.Queries;

/// <summary>
/// Turns the text a driver reports for a failed statement into a <see cref="QueryError"/>. Every engine
/// words its errors differently and several drivers wrap them in their own prefixes, so the rules for
/// all of them live here rather than in each provider.
/// </summary>
public static partial class QueryErrorNormalizer
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly (Regex Pattern, QueryErrorKind Kind)[] TokenPatterns =
    [
        (SqliteNoSuchColumn(), QueryErrorKind.UnknownColumn),
        (PostgresQuotedColumn(), QueryErrorKind.UnknownColumn),
        (PostgresQualifiedColumn(), QueryErrorKind.UnknownColumn),
        (MySqlUnknownColumn(), QueryErrorKind.UnknownColumn),
        (SqlServerInvalidColumn(), QueryErrorKind.UnknownColumn),
        (SqliteNoSuchTable(), QueryErrorKind.UnknownTable),
        (PostgresMissingRelation(), QueryErrorKind.UnknownTable),
        (MySqlMissingTable(), QueryErrorKind.UnknownTable),
        (SqlServerInvalidObject(), QueryErrorKind.UnknownTable),
        (SqliteSyntaxNear(), QueryErrorKind.Syntax),
        (SqliteUnrecognizedToken(), QueryErrorKind.Syntax),
        (PostgresSyntaxNear(), QueryErrorKind.Syntax),
        (SqlServerSyntaxNear(), QueryErrorKind.Syntax)
    ];

    /// <param name="raw">The driver's error text, kept unchanged on the result.</param>
    /// <param name="sql">The SQL that was run, used to locate the error when the engine gives no position.</param>
    /// <param name="engine">Structured data the provider read from the driver's exception.</param>
    public static QueryError Normalize(string raw, string? sql = null, EngineErrorDetails? engine = null)
    {
        var (message, parsedCode) = StripDriverText(engine?.Message ?? raw);
        var classified = Classify(message);
        var line = engine?.Line ?? classified.Line;

        var span = string.IsNullOrEmpty(sql)
            ? null
            : SqlErrorLocator.Locate(sql, classified.Kind, classified.Token, engine?.Position,
                engine?.StatementOffset ?? 0, line, classified.QuotedText);

        return new QueryError
        {
            Raw = raw,
            Message = message,
            Title = classified.Title,
            Kind = classified.Kind,
            Token = classified.Token,
            Code = engine?.Code ?? parsedCode,
            Line = span?.Line ?? line,
            Column = span?.Column,
            EndColumn = span?.EndColumn
        };
    }

    /// <param name="QuotedText">SQL the message quotes verbatim from the failure point (MySQL's "near '...'").</param>
    /// <param name="Line">A line number the message states (MySQL's "at line N").</param>
    private sealed record Classification(
        QueryErrorKind Kind, string? Token, string Title, string? QuotedText = null, int? Line = null);

    private static (string Message, string? Code) StripDriverText(string text)
    {
        var trimmed = DropStackTrace(text).Trim();
        var prefix = DriverPrefix().Match(trimmed);
        var message = trimmed[prefix.Length..];
        if (message.Length == 0)
        {
            message = trimmed;
        }

        var sqliteName = prefix.Groups["name"].Success ? prefix.Groups["name"].Value : null;
        var sqliteNumber = prefix.Groups["number"].Success ? prefix.Groups["number"].Value : null;
        var sqlState = prefix.Groups["state"].Success ? prefix.Groups["state"].Value : null;

        var code = (sqliteName, sqliteNumber, sqlState) switch
        {
            ({ } name, { } number, _) => $"{name} (code {number})",
            ({ } name, null, _) => name,
            (null, { } number, _) => $"SQLite code {number}",
            (_, _, { } state) => $"SQLSTATE {state}",
            _ => null
        };

        return (message, code);
    }

    /// <summary>
    /// Errors thrown in JavaScript reach .NET as the message, a newline, then the stack, which starts
    /// with a header repeating the message.
    /// </summary>
    private static string DropStackTrace(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var firstFrame = Array.FindIndex(lines, line => StackFrame().IsMatch(line));
        if (firstFrame < 0) return text;

        var end = firstFrame;
        if (end > 1 && lines[end - 1].TrimEnd().EndsWith(lines[0].Trim(), StringComparison.Ordinal))
        {
            end--;
        }

        return string.Join('\n', lines[..end]);
    }

    private static Classification Classify(string message)
    {
        var mySqlSyntax = MySqlSyntaxNear().Match(message);
        if (mySqlSyntax.Success)
        {
            var near = mySqlSyntax.Groups["near"].Value;
            var line = int.TryParse(mySqlSyntax.Groups["line"].Value, out var parsed) ? parsed : (int?)null;
            var firstWord = near.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            return firstWord == null
                ? new Classification(QueryErrorKind.Syntax, null, "Syntax error at end of input", Line: line)
                : new Classification(QueryErrorKind.Syntax, firstWord, TitleFor(QueryErrorKind.Syntax), near.TrimStart(), line);
        }

        var firstLine = FirstLine(message);
        foreach (var (pattern, kind) in TokenPatterns)
        {
            var match = pattern.Match(firstLine);
            if (!match.Success) continue;

            var token = match.Groups["t"].Value.Trim();
            if (token.Length > 0)
            {
                return new Classification(kind, token, TitleFor(kind));
            }
        }

        var bareKind = SyntaxWithoutToken().IsMatch(firstLine) ? QueryErrorKind.Syntax : QueryErrorKind.General;
        return new Classification(bareKind, null, Capitalize(firstLine));
    }

    private static string TitleFor(QueryErrorKind kind) => kind switch
    {
        QueryErrorKind.UnknownColumn => "No such column",
        QueryErrorKind.UnknownTable => "No such table",
        QueryErrorKind.Syntax => "Syntax error near",
        _ => string.Empty
    };

    private static string FirstLine(string message)
    {
        var newline = message.IndexOf('\n');
        return (newline < 0 ? message : message[..newline]).Trim();
    }

    private static string Capitalize(string text) =>
        text.Length > 0 && char.IsLower(text[0]) ? char.ToUpperInvariant(text[0]) + text[1..] : text;

    [GeneratedRegex(@"^\s+at\s+\S", Options)]
    private static partial Regex StackFrame();

    // SqliteWasmBlazor wraps worker failures as "Worker error: SQLITE_ERROR: sqlite3 result code 1: <message>",
    // and Npgsql starts PostgresException.Message with the SQLSTATE. Every SQLSTATE class starts with a
    // digit except F0, HV, P0 and XX, which keeps words such as "ERROR:" from being read as a code.
    [GeneratedRegex(@"^(?:Worker error:\s*)?(?:(?-i:(?<name>SQLITE_[A-Z_]+)):\s*)?(?:sqlite3 result code (?<number>\d+):\s*)?(?:(?-i:(?<state>(?:[0-9][0-9A-Z]|F0|HV|P0|XX)[0-9A-Z]{3})):\s+)?", Options)]
    private static partial Regex DriverPrefix();

    [GeneratedRegex(@"^no such column:\s*(?<t>.+?)\s*$", Options)]
    private static partial Regex SqliteNoSuchColumn();

    [GeneratedRegex(@"^column\s+""(?<t>[^""]+)""(?:\s+of\s+relation\s+""[^""]+"")?\s+does not exist", Options)]
    private static partial Regex PostgresQuotedColumn();

    [GeneratedRegex(@"^column\s+(?<t>[^\s""]+)\s+does not exist", Options)]
    private static partial Regex PostgresQualifiedColumn();

    [GeneratedRegex(@"^Unknown column '(?<t>[^']+)' in ", Options)]
    private static partial Regex MySqlUnknownColumn();

    [GeneratedRegex(@"^Invalid column name '(?<t>[^']+)'", Options)]
    private static partial Regex SqlServerInvalidColumn();

    [GeneratedRegex(@"^no such table:\s*(?<t>.+?)\s*$", Options)]
    private static partial Regex SqliteNoSuchTable();

    [GeneratedRegex(@"^relation\s+""(?<t>[^""]+)""\s+does not exist", Options)]
    private static partial Regex PostgresMissingRelation();

    [GeneratedRegex(@"^Table '(?<t>[^']+)' doesn't exist", Options)]
    private static partial Regex MySqlMissingTable();

    [GeneratedRegex(@"^Invalid object name '(?<t>[^']+)'", Options)]
    private static partial Regex SqlServerInvalidObject();

    [GeneratedRegex(@"^near ""(?<t>.*)"": syntax error", Options)]
    private static partial Regex SqliteSyntaxNear();

    [GeneratedRegex(@"^unrecognized token: ""(?<t>.*)""", Options)]
    private static partial Regex SqliteUnrecognizedToken();

    [GeneratedRegex(@"^syntax error at or near ""(?<t>.*)""", Options)]
    private static partial Regex PostgresSyntaxNear();

    [GeneratedRegex(@"^Incorrect syntax near (?:the keyword )?'(?<t>.*)'", Options)]
    private static partial Regex SqlServerSyntaxNear();

    [GeneratedRegex(@"^You have an error in your SQL syntax;.*?near '(?<near>.*)' at line (?<line>\d+)", Options | RegexOptions.Singleline)]
    private static partial Regex MySqlSyntaxNear();

    [GeneratedRegex(@"^(?:syntax error|incomplete input)", Options)]
    private static partial Regex SyntaxWithoutToken();
}
