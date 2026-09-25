using System.Text.RegularExpressions;

namespace Aion.Contracts.Queries;

/// <summary>
/// Screens statement text before a provider prefixes it with EXPLAIN or runs it to capture a plan.
/// Drivers happily send several statements in one command, and only the first would be explained:
/// the rest would simply execute. The checks are deliberately conservative (a semicolon inside a
/// string literal is also refused) because a false refusal is harmless and a false pass is not.
/// </summary>
public static partial class QueryPlanStatementGuard
{
    public const string MultipleStatementsMessage =
        "Query plans can only be captured for a single statement. Select one statement and run it again.";

    public const string TransactionControlMessage =
        "Actual plans can't be captured for statements that commit or roll back, because the plan run is rolled back afterwards.";

    public static string TrimTrailingTerminators(string query) =>
        query.TrimEnd().TrimEnd(';', ' ', '\t', '\r', '\n');

    public static string? RequireSingleStatement(string query) =>
        TrimTrailingTerminators(query).Contains(';') ? MultipleStatementsMessage : null;

    public static string? RejectTransactionControl(string query) =>
        TransactionControlPattern().IsMatch(query) ? TransactionControlMessage : null;

    /// <summary>
    /// Refusal reason for running <paramref name="query"/> inside a rolled-back transaction to capture
    /// its actual plan, or null when that is safe.
    /// </summary>
    public static string? GetActualPlanRefusal(string query) =>
        RequireSingleStatement(query) ?? RejectTransactionControl(query);

    [GeneratedRegex(@"\b(COMMIT|ROLLBACK)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TransactionControlPattern();
}
