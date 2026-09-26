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
    /// Whether the latest run returned results or only a plan.
    /// </summary>
    [JsonIgnore]
    public QueryResultKind ResultKind { get; private set; }

    /// <summary>
    /// The tab's full SQL when its result arrived. Error positions refer to this text, so the editor
    /// only marks the error while the tab still holds it.
    /// </summary>
    [JsonIgnore]
    public string? ResultSourceText { get; internal set; }

    [JsonIgnore]
    public bool ErrorLocationIsCurrent => Result?.ErrorDetail?.Line != null && ResultSourceText == Query;

    /// <summary>
    /// The SQL the latest run executed: only the selection when a selection was run. It is kept apart
    /// from <see cref="Query"/> so running never changes the tab's text, even for a moment.
    /// </summary>
    [JsonIgnore]
    public string? ExecutedSql { get; private set; }

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

    /// <summary>
    /// Whether the tab holds SQL. Closing a tab also deletes its saved copy, so this SQL would be lost.
    /// </summary>
    [JsonIgnore]
    public bool HasSql => !string.IsNullOrWhiteSpace(Query);
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

    /// <param name="sql">The SQL being run, when it is not the tab's whole text.</param>
    public void StartExecution(string? sql = null)
    {
        IsExecuting = true;
        ExecutedSql = sql ?? Query;
        ExecutionStartTime = DateTimeOffset.Now;
        ExecutionEndTime = null;
    }

    public void SetResult(QueryResult result, QueryResultKind kind = QueryResultKind.Results)
    {
        // Providers without structured driver errors, and failures raised before a provider ran, only
        // set the text. The SQL that ran locates the error.
        if (result is { Error: { } raw, ErrorDetail: null })
        {
            result.ErrorDetail = QueryErrorNormalizer.Normalize(raw, ExecutedSql ?? Query);
        }

        Result = result;
        ResultKind = kind;
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
            ResultKind = ResultKind,
            ExecutedSql = ExecutedSql,
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