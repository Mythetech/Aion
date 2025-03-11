using Aion.Core.Queries;
using MudBlazor;

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
    public bool IncludeEstimatedPlan { get; set; }
    public bool IncludeActualPlan { get; set; }
    public QueryPlan? EstimatedPlan { get; set; }
    public QueryPlan? ActualPlan { get; set; }
    public bool UseTransaction { get; set; }
    public TransactionInfo? Transaction { get; set; }
    public DateTimeOffset? ExecutionStartTime { get; private set; }
    public DateTimeOffset? ExecutionEndTime { get; private set; }
    public TimeSpan? ExecutionDuration => ExecutionEndTime - ExecutionStartTime;
    
    public string? EmphasisColor { get; set; }

    public void UpdateEmphasisColor(string color)
    {
        if (color.Equals(EmphasisColor, StringComparison.OrdinalIgnoreCase))
        {
            EmphasisColor = null;
            return;
        }
        
        EmphasisColor = color;
    }

    public void StartExecution()
    {
        IsExecuting = true;
        ExecutionStartTime = DateTimeOffset.Now;
        ExecutionEndTime = null;
    }

    public void SetResult(QueryResult result)
    {
        Result = result;
        IsExecuting = false;
        ExecutionEndTime = DateTimeOffset.Now;
    }

    public QueryModel Clone(bool newId = false)
    {
        return new QueryModel
        {
            Id = newId ? Guid.NewGuid() : Id,
            Name = Name,
            Query = Query,
            Result = Result?.Clone(),
            IsExecuting = IsExecuting,
            ConnectionId = ConnectionId,
            DatabaseName = DatabaseName,
            IncludeEstimatedPlan = IncludeEstimatedPlan,
            IncludeActualPlan = IncludeActualPlan,
            EstimatedPlan = EstimatedPlan?.Clone(),
            ActualPlan = ActualPlan?.Clone(),
            UseTransaction = UseTransaction,
            Transaction = Transaction,
            ExecutionStartTime = ExecutionStartTime,
            ExecutionEndTime = ExecutionEndTime
        };
    }
}