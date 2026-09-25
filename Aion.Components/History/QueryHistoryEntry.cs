using System.Text.Json.Serialization;

namespace Aion.Components.History;

/// <summary>
/// An immutable record of a single query run. Result rows are deliberately not kept.
/// </summary>
public sealed record QueryHistoryEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Sql { get; init; }

    public Guid? ConnectionId { get; init; }

    public string? ConnectionName { get; init; }

    public string? DatabaseName { get; init; }

    public QueryHistoryStatus Status { get; init; }

    public string? ErrorMessage { get; init; }

    public int? RowCount { get; init; }

    public int? RowsAffected { get; init; }

    public TimeSpan? Duration { get; init; }

    public DateTimeOffset ExecutedAt { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<QueryHistoryStatus>))]
public enum QueryHistoryStatus
{
    Success,
    Failed,
    Cancelled
}
