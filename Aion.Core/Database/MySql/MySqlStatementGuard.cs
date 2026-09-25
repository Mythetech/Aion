using Aion.Contracts.Queries;

namespace Aion.Core.Database.MySql;

/// <summary>
/// MySQL commits implicitly before (and after) DDL and many administrative statements, and a
/// deadlock or a COMMIT typed by the user ends the transaction without the driver noticing. Any of
/// those would leave Aion reporting an open transaction while later statements autocommit, so
/// statements are checked against an allowlist of leading keywords before they run inside one.
/// Anything unrecognised is refused, which keeps the check safe when the parse is imperfect.
/// </summary>
public static class MySqlStatementGuard
{
    private static readonly HashSet<string> ActualPlanKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "WITH", "TABLE", "UPDATE", "DELETE", "("
    };

    private static readonly HashSet<string> TransactionKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "WITH", "TABLE", "VALUES", "INSERT", "UPDATE", "DELETE", "REPLACE",
        "SHOW", "DESCRIBE", "DESC", "EXPLAIN", "DO", "SET", "SAVEPOINT", "RELEASE", "("
    };

    private static readonly HashSet<string> TransactionControlKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "COMMIT", "ROLLBACK", "BEGIN", "START", "XA"
    };

    public static string? GetActualPlanRefusal(string query)
    {
        var refusal = QueryPlanStatementGuard.GetActualPlanRefusal(query);
        if (refusal != null) return refusal;

        var keyword = GetLeadingKeyword(query);
        return ActualPlanKeywords.Contains(keyword)
            ? null
            : "MySQL can only capture actual plans for SELECT, TABLE, UPDATE and DELETE statements. " +
              "DDL and other statements commit implicitly, so their changes could not be rolled back.";
    }

    public static string? GetTransactionRefusal(string query)
    {
        foreach (var statement in QueryPlanStatementGuard.TrimTrailingTerminators(query).Split(';'))
        {
            if (string.IsNullOrWhiteSpace(statement)) continue;

            var keyword = GetLeadingKeyword(statement);

            if (TransactionControlKeywords.Contains(keyword))
            {
                return "Use the Commit and Rollback buttons to end the transaction. " +
                       $"Running {keyword.ToUpperInvariant()} directly would end it without Aion knowing.";
            }

            var changesAutocommit = keyword.Equals("SET", StringComparison.OrdinalIgnoreCase)
                                    && statement.Contains("autocommit", StringComparison.OrdinalIgnoreCase);

            if (!TransactionKeywords.Contains(keyword) || changesAutocommit)
            {
                return "MySQL implicitly commits an open transaction for DDL and many other statements, so only " +
                       "SELECT, INSERT, UPDATE, DELETE, REPLACE and similar statements run inside one. " +
                       "Commit or roll back first to run this outside a transaction.";
            }
        }

        return null;
    }

    /// <summary>
    /// First keyword of the statement after skipping whitespace and comments. MySQL executable
    /// comments (<c>/*! ... */</c>) are not skipped because the server runs their contents.
    /// </summary>
    public static string GetLeadingKeyword(string statement)
    {
        var i = 0;
        while (i < statement.Length)
        {
            if (char.IsWhiteSpace(statement[i]))
            {
                i++;
            }
            else if (statement[i] == '#' || StartsWith(statement, i, "--"))
            {
                var newline = statement.IndexOf('\n', i);
                i = newline < 0 ? statement.Length : newline + 1;
            }
            else if (StartsWith(statement, i, "/*") && !StartsWith(statement, i, "/*!"))
            {
                var end = statement.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? statement.Length : end + 2;
            }
            else
            {
                break;
            }
        }

        if (i >= statement.Length) return string.Empty;

        if (!char.IsLetter(statement[i])) return statement[i].ToString();

        var start = i;
        while (i < statement.Length && (char.IsLetter(statement[i]) || statement[i] == '_'))
        {
            i++;
        }

        return statement[start..i];
    }

    private static bool StartsWith(string text, int index, string value) =>
        string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
}
