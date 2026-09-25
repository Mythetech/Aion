namespace Aion.Contracts.Queries.Editing;

public record SqlGenerationResult(
    List<GeneratedStatement> Statements,
    bool RequiresTransaction,
    string? ValidationError = null
)
{
    public bool IsValid => string.IsNullOrEmpty(ValidationError);
    public int StatementCount => Statements.Count;
}

/// <summary>
/// One statement generated for a pending change, kept with its change so failures can be reported per row.
/// </summary>
public record GeneratedStatement(PendingChange Change, string Sql)
{
    /// <summary>
    /// UPDATE and DELETE statements target a single row by primary key, so any other affected row count means
    /// the statement did not do what the preview showed.
    /// </summary>
    public bool ExpectsSingleRow => Change.Type is ChangeType.Update or ChangeType.Delete;
}
