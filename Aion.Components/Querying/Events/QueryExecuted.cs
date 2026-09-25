using Aion.Contracts.Queries;

namespace Aion.Components.Querying.Events;

/// <summary>
/// Published after a query run finishes, fails or is cancelled.
/// </summary>
/// <remarks>
/// <see cref="Query"/> is the live tab model: later edits and runs overwrite it, and the editor swaps its text
/// to the selection only for the duration of a run. The remaining properties are captured when the message
/// is created so consumers that need to know what actually ran are not affected by those mutations.
/// </remarks>
public record QueryExecuted(QueryModel Query)
{
    public string ExecutedSql { get; init; } = Query.Query;

    public Guid? ConnectionId { get; init; } = Query.ConnectionId;

    public string? DatabaseName { get; init; } = Query.DatabaseName;

    // The actual plan path publishes before a result is set, so a result left over from an earlier run
    // must not be reported as the outcome of this one.
    public QueryResult? Result { get; init; } = Query.ExecutionEndTime is null ? null : Query.Result;

    public TimeSpan? Duration { get; init; } = Query.ExecutionDuration;

    public DateTimeOffset ExecutedAt { get; init; } = Query.ExecutionStartTime ?? DateTimeOffset.Now;
}
