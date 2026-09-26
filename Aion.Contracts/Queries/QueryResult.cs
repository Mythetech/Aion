namespace Aion.Contracts.Queries;

public class QueryResult
{
    public List<string> Columns { get; set; } = [];
    public List<Dictionary<string, object>> Rows { get; set; } = [];
    public int RowCount => Rows.Count;
    public DateTime ExecutedAt { get; set; } = DateTime.Now;
    public string? Error { get; set; }

    /// <summary>
    /// The structured form of <see cref="Error"/>, with the message normalized and the engine's code
    /// and position kept separately. Null for successful results.
    /// </summary>
    public QueryError? ErrorDetail { get; set; }

    public bool Success => Error == null;

    public bool Cancelled { get; set; } = false;

    /// <summary>
    /// Rows inserted, updated or deleted by the statement, or null when the provider could not report it
    /// (for example a SELECT, or a driver that does not expose the count).
    /// </summary>
    public int? RowsAffected { get; set; }

    public void SetError(QueryError error)
    {
        Error = error.Raw;
        ErrorDetail = error;
    }

    public QueryResult Clone()
    {
        return new QueryResult()
        {
            Columns = Columns.ToList(),
            Rows = Rows.ToList(),
            ExecutedAt = ExecutedAt,
            Error = Error,
            ErrorDetail = ErrorDetail,
            Cancelled = Cancelled,
            RowsAffected = RowsAffected
        };
    }
}
