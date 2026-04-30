namespace Aion.Contracts.Queries.Editing;

public record SqlGenerationResult(
    List<string> Statements,
    bool RequiresTransaction,
    string? ValidationError = null
)
{
    public bool IsValid => string.IsNullOrEmpty(ValidationError);
    public int StatementCount => Statements.Count;
}
