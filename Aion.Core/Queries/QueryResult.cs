namespace Aion.Core.Queries;

public class QueryResult
{
    public List<string> Columns { get; set; } = [];
    public List<Dictionary<string, object>> Rows { get; set; } = [];
    public int RowCount => Rows.Count;
    public DateTime ExecutedAt { get; set; } = DateTime.Now;
    public string? Error { get; set; }
    public bool Success => Error == null;

    public bool Cancelled { get; set; } = false;

    public QueryResult Clone()
    {
        return new QueryResult()
        {
            Columns = Columns.ToList(),
            Rows = Rows.ToList(),
            ExecutedAt = ExecutedAt,
            Error = Error,
            Cancelled = Cancelled
        };
    }
} 