using Aion.Core.Queries;

namespace Aion.Components.Querying;

public class QueryModel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "";

    public string Query { get; set; } = "";
    public QueryResult? Result { get; set; }
    public bool IsExecuting { get; set; }
    public DateTime? LastExecuted => Result?.ExecutedAt;
    
    public Guid? ConnectionId { get; set; }
    public string? DatabaseName { get; set; }

    public QueryModel Clone()
    {
        return new QueryModel()
        {
            Id = Id,
            Name = Name,
            Query = Query,
            Result = Result?.Clone(),
            IsExecuting = IsExecuting,
            ConnectionId = ConnectionId,
            DatabaseName = DatabaseName
        };
    }
}