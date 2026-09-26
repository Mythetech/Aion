namespace Aion.Contracts.Queries;

public class QueryResult
{
    /// <summary>
    /// The key each column's values are stored under in <see cref="Rows"/>. Keys are unique, so a name the
    /// database repeats (SELECT a.id, b.id) gets a suffix ("id", "id_2") and an unnamed column gets its
    /// position ("column1"); <see cref="ColumnNames"/> keeps the names themselves.
    /// </summary>
    public List<string> Columns { get; set; } = [];

    /// <summary>
    /// The names the database gave the columns, in the order of <see cref="Columns"/>, for headers in the grid and
    /// in exports. Empty when a provider built <see cref="Columns"/> itself, in which case the keys are the names.
    /// </summary>
    public List<string> ColumnNames { get; set; } = [];

    /// <summary>
    /// Each column's type as the provider names it ("integer", "VARCHAR", "double precision"), in the order of
    /// <see cref="Columns"/>. Shorter than <see cref="Columns"/>, or null for a column, when the provider cannot tell.
    /// </summary>
    public List<string?> ColumnTypes { get; set; } = [];

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

    public string? ColumnType(int ordinal) => ordinal < ColumnTypes.Count ? ColumnTypes[ordinal] : null;

    public string ColumnName(int ordinal) => ordinal < ColumnNames.Count ? ColumnNames[ordinal] : Columns[ordinal];

    /// <summary>
    /// The column names in order, as headers for the grid and for exports.
    /// </summary>
    public List<string> Headers() => Columns.Select((_, ordinal) => ColumnName(ordinal)).ToList();

    /// <summary>
    /// Adds a column as the database named it and returns the key its values are stored under.
    /// </summary>
    public string AddColumn(string name, string? type = null)
    {
        var key = UniqueKey(name.Length == 0 ? $"column{Columns.Count + 1}" : name);
        Columns.Add(key);
        ColumnNames.Add(name);
        ColumnTypes.Add(type);
        return key;
    }

    private string UniqueKey(string candidate)
    {
        if (!Columns.Contains(candidate))
            return candidate;

        for (var suffix = 2; ; suffix++)
        {
            var key = $"{candidate}_{suffix}";
            if (!Columns.Contains(key))
                return key;
        }
    }

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
            ColumnNames = ColumnNames.ToList(),
            ColumnTypes = ColumnTypes.ToList(),
            Rows = Rows.ToList(),
            ExecutedAt = ExecutedAt,
            Error = Error,
            ErrorDetail = ErrorDetail,
            Cancelled = Cancelled,
            RowsAffected = RowsAffected
        };
    }
}
