using Aion.Core.Database;

namespace Aion.Core.Queries.Editing;

/// <summary>
/// Extends QueryResult with metadata required for editing operations.
/// </summary>
public class EditableQueryResult : QueryResult
{
    /// <summary>
    /// The source table name for this result set.
    /// </summary>
    public string? SourceTable { get; set; }

    /// <summary>
    /// The source database name.
    /// </summary>
    public string? SourceDatabase { get; set; }

    /// <summary>
    /// The connection ID for executing updates.
    /// </summary>
    public Guid? ConnectionId { get; set; }

    /// <summary>
    /// Column metadata including primary key and type information.
    /// </summary>
    public List<ColumnInfo> ColumnMetadata { get; set; } = [];

    /// <summary>
    /// Get the primary key column names.
    /// </summary>
    public List<string> PrimaryKeyColumns => ColumnMetadata
        .Where(c => c.IsPrimaryKey)
        .Select(c => c.Name)
        .ToList();

    /// <summary>
    /// Whether this result has an identifiable primary key.
    /// </summary>
    public bool HasPrimaryKey => PrimaryKeyColumns.Count > 0;

    /// <summary>
    /// Whether this result can be edited (has primary key and source table).
    /// </summary>
    public bool IsEditable => HasPrimaryKey && !string.IsNullOrEmpty(SourceTable);

    /// <summary>
    /// Get column metadata by name.
    /// </summary>
    public ColumnInfo? GetColumnInfo(string columnName)
    {
        return ColumnMetadata.FirstOrDefault(c =>
            c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Create an EditableQueryResult from a standard QueryResult.
    /// </summary>
    public static EditableQueryResult FromQueryResult(
        QueryResult result,
        string? sourceTable = null,
        string? sourceDatabase = null,
        Guid? connectionId = null,
        List<ColumnInfo>? columnMetadata = null)
    {
        return new EditableQueryResult
        {
            Columns = result.Columns,
            Rows = result.Rows,
            ExecutedAt = result.ExecutedAt,
            Error = result.Error,
            Cancelled = result.Cancelled,
            SourceTable = sourceTable,
            SourceDatabase = sourceDatabase,
            ConnectionId = connectionId,
            ColumnMetadata = columnMetadata ?? []
        };
    }
}
