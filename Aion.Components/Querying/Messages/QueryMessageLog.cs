using System.Globalization;
using System.Text.RegularExpressions;
using Aion.Components.Querying.Errors;
using Aion.Contracts.Queries;

namespace Aion.Components.Querying.Messages;

/// <summary>
/// Each query tab's Messages log: transactions starting and ending, and every statement run with what it
/// returned or changed. Kept in memory for the session only, and only the latest lines of each tab.
/// </summary>
public partial class QueryMessageLog
{
    public const int MaxMessagesPerTab = 200;

    public const int StatementPreviewLength = 120;

    private readonly Dictionary<Guid, List<QueryMessage>> _messages = new();

    /// <summary>Raised with the tab whose log changed.</summary>
    public event Action<Guid>? MessagesChanged;

    public IReadOnlyList<QueryMessage> For(Guid queryId) =>
        _messages.TryGetValue(queryId, out var messages) ? messages : [];

    public void RecordBegin(Guid queryId, DateTimeOffset at) =>
        Append(queryId, new QueryMessage(at, QueryMessageKind.Transaction, "BEGIN"));

    public void RecordEnd(Guid queryId, bool committed, DateTimeOffset at) =>
        Append(queryId, new QueryMessage(at, QueryMessageKind.Transaction, committed ? "COMMIT" : "ROLLBACK"));

    /// <param name="result">Null when the run ended without one, which is logged as completed.</param>
    public void RecordRun(Guid queryId, string sql, QueryResult? result, DateTimeOffset startedAt, TimeSpan? duration)
    {
        var full = sql.Trim();
        var preview = Preview(full);
        var (kind, text) = Outcome(result);

        Append(queryId,
            new QueryMessage(startedAt, QueryMessageKind.Statement, preview) { Detail = preview == full ? null : full },
            new QueryMessage(startedAt + (duration ?? TimeSpan.Zero), kind, text) { Duration = duration });
    }

    public void Forget(Guid queryId)
    {
        if (_messages.Remove(queryId))
        {
            MessagesChanged?.Invoke(queryId);
        }
    }

    private void Append(Guid queryId, params QueryMessage[] messages)
    {
        if (!_messages.TryGetValue(queryId, out var log))
        {
            log = [];
            _messages[queryId] = log;
        }

        log.AddRange(messages);
        if (log.Count > MaxMessagesPerTab)
        {
            log.RemoveRange(0, log.Count - MaxMessagesPerTab);
        }

        MessagesChanged?.Invoke(queryId);
    }

    private static (QueryMessageKind Kind, string Text) Outcome(QueryResult? result) => result switch
    {
        { Cancelled: true } => (QueryMessageKind.Info, "Cancelled"),
        { Success: false } => (QueryMessageKind.Error, QueryErrorText.LogLine(result.ErrorDetail ?? QueryErrorNormalizer.Normalize(result.Error!))),
        { Columns.Count: > 0 } => (QueryMessageKind.Success, $"{Count(result.RowCount, "row")} returned"),
        { RowsAffected: { } affected } => (QueryMessageKind.Success, $"{Count(affected, "row")} affected"),
        _ => (QueryMessageKind.Success, "Completed")
    };

    private static string Count(int count, string noun) =>
        count == 1 ? $"1 {noun}" : $"{count.ToString("N0", CultureInfo.CurrentCulture)} {noun}s";

    /// <summary>
    /// The statement on one line. Saved queries often open with comment lines that say nothing about what
    /// ran, so those are skipped when anything else is left.
    /// </summary>
    private static string Preview(string sql)
    {
        var code = string.Join(' ', sql.Split('\n')
            .Select(line => line.Trim())
            .SkipWhile(line => line.Length == 0 || line.StartsWith("--", StringComparison.Ordinal)));

        var oneLine = Whitespace().Replace(code.Length > 0 ? code : sql, " ").Trim().TrimEnd(';').TrimEnd();

        return oneLine.Length <= StatementPreviewLength
            ? oneLine
            : string.Concat(oneLine.AsSpan(0, StatementPreviewLength - 1), "…");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
