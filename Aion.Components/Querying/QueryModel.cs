namespace Aion.Components.Querying;

public class QueryModel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "";

    public string Query { get; set; } = "";
    public QueryResult? Result { get; set; }
    public bool IsExecuting { get; set; }
    public DateTime? LastExecuted => Result?.ExecutedAt;
}