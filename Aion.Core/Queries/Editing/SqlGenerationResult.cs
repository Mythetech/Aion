namespace Aion.Core.Queries.Editing;

/// <summary>
/// Result of SQL generation for pending changes.
/// </summary>
public record SqlGenerationResult(
    List<string> Statements,
    bool RequiresTransaction,
    string? ValidationError = null
)
{
    public bool IsValid => string.IsNullOrEmpty(ValidationError);
    public int StatementCount => Statements.Count;
}
