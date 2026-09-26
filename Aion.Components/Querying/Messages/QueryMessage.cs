namespace Aion.Components.Querying.Messages;

public enum QueryMessageKind
{
    /// <summary>BEGIN, COMMIT or ROLLBACK.</summary>
    Transaction,

    /// <summary>The SQL a run executed, shortened to one line.</summary>
    Statement,

    /// <summary>What a statement that worked returned or changed.</summary>
    Success,

    /// <summary>An outcome that is neither a success nor a failure, such as a cancelled run.</summary>
    Info,

    Error
}

/// <summary>One line of a query tab's Messages log.</summary>
/// <param name="Time">When it happened; a statement's outcome is timed from when the statement finished.</param>
public sealed record QueryMessage(DateTimeOffset Time, QueryMessageKind Kind, string Text)
{
    /// <summary>How long the statement ran, for an outcome.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>The full text when <see cref="Text"/> is shortened, such as a statement over several lines.</summary>
    public string? Detail { get; init; }
}
