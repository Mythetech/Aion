namespace Aion.Contracts.Queries;

public enum QueryErrorKind
{
    General,
    UnknownColumn,
    UnknownTable,
    Syntax
}

/// <summary>
/// A failed statement's error in a form the results pane can present: the engine's message without
/// driver decoration, a short title, the engine's own error code, and where in the SQL it failed when
/// that is known. <see cref="Raw"/> keeps the driver's text untouched for copying.
/// </summary>
public sealed record QueryError
{
    /// <summary>The error exactly as the driver reported it.</summary>
    public required string Raw { get; init; }

    /// <summary>The engine's message with driver prefixes and stack traces removed.</summary>
    public required string Message { get; init; }

    /// <summary>A short headline, such as "No such column". Read together with <see cref="Token"/> when it is set.</summary>
    public required string Title { get; init; }

    public QueryErrorKind Kind { get; init; }

    /// <summary>The identifier or token the engine complained about, when it named one.</summary>
    public string? Token { get; init; }

    /// <summary>The engine's error code as its own tools print it, such as "SQLSTATE 42703" or "SQLITE_ERROR (code 1)".</summary>
    public string? Code { get; init; }

    /// <summary>1-based line of the failure in the SQL that was run, when the engine reported or implied one.</summary>
    public int? Line { get; init; }

    /// <summary>1-based column where the offending text starts on <see cref="Line"/>.</summary>
    public int? Column { get; init; }

    /// <summary>1-based column just past the offending text on <see cref="Line"/>, as editor ranges expect.</summary>
    public int? EndColumn { get; init; }

    /// <summary>
    /// Moves a position measured in a run selection into the text around it, given where the
    /// selection started. Only the selection's first line shares a line with the text before it.
    /// </summary>
    public QueryError ShiftedTo(int line, int column)
    {
        if (Line is not { } errorLine) return this;
        if (errorLine != 1) return this with { Line = errorLine + line - 1 };

        return this with
        {
            Line = line,
            Column = Column + column - 1,
            EndColumn = EndColumn + column - 1
        };
    }
}

/// <summary>
/// Structured error data a provider read from its driver's exception, which is more reliable than
/// anything parsed back out of the message text.
/// </summary>
public sealed record EngineErrorDetails
{
    /// <summary>The engine's message without the driver's decoration.</summary>
    public string? Message { get; init; }

    public string? Code { get; init; }

    /// <summary>1-based character offset of the failure, as PostgreSQL reports it (characters, not UTF-16 units).</summary>
    public int? Position { get; init; }

    /// <summary>
    /// 0-based index in the SQL that was run of the statement <see cref="Position"/> is measured
    /// from, for drivers that send a multi-statement command as separate statements.
    /// </summary>
    public int StatementOffset { get; init; }

    /// <summary>1-based line of the failure, as SQL Server reports it.</summary>
    public int? Line { get; init; }
}
