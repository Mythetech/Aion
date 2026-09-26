using System.Text.Json.Serialization;
using Aion.Components.Querying.Consumers;
using Aion.Contracts.Queries;
using MudBlazor;

namespace Aion.Components.Querying;

public class QueryModel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Query { get; set; } = "";
    public QueryResult? Result { get; set; }

    /// <summary>
    /// Informational message shown alongside a successful result, such as how an actual plan was captured.
    /// </summary>
    [JsonIgnore]
    public string? ResultNotice { get; private set; }

    // Not persisted: a saved "executing" flag would leave a tab stuck showing Cancel after a restart.
    [JsonIgnore]
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

    [JsonIgnore]
    public bool HasOpenTransaction => Transaction is { Status: TransactionStatus.Active };
    public DateTimeOffset? ExecutionStartTime { get; private set; }
    public DateTimeOffset? ExecutionEndTime { get; private set; }
    public TimeSpan? ExecutionDuration => ExecutionEndTime - ExecutionStartTime;

    public int Order { get; set; }
    public string? SavedQuery { get; set; }
    public bool IsDirty => Query != (SavedQuery ?? "");
    public string? EmphasisColor { get; set; }

    /// <summary>
    /// Metadata for edit mode support.
    /// </summary>
    public QueryEditMetadata? EditMetadata { get; set; }

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

    public void SetResult(QueryResult result, string? notice = null)
    {
        // Providers without structured driver errors, and failures raised before a provider ran, only
        // set the text. Query still holds the SQL that was run here, which locates the error.
        if (result is { Error: { } raw, ErrorDetail: null })
        {
            result.ErrorDetail = QueryErrorNormalizer.Normalize(raw, Query);
        }

        Result = result;
        ResultNotice = notice;
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
            ResultNotice = ResultNotice,
            IsExecuting = !newId && IsExecuting,
            ConnectionId = ConnectionId,
            DatabaseName = DatabaseName,
            IncludeEstimatedPlan = IncludeEstimatedPlan,
            IncludeActualPlan = IncludeActualPlan,
            EstimatedPlan = EstimatedPlan?.Clone(),
            ActualPlan = ActualPlan?.Clone(),
            UseTransaction = UseTransaction,
            // A duplicated tab must not share the original tab's open transaction.
            Transaction = newId ? null : Transaction,
            ExecutionStartTime = ExecutionStartTime,
            ExecutionEndTime = ExecutionEndTime,
            Order = Order,
            SavedQuery = SavedQuery,
            EmphasisColor = EmphasisColor
        };
    }
}