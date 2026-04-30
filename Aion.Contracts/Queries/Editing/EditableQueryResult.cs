using Aion.Contracts.Database;

namespace Aion.Contracts.Queries.Editing;

public class EditableQueryResult : QueryResult
{
    public string? SourceTable { get; set; }
    public string? SourceSchema { get; set; }
    public string? SourceDatabase { get; set; }
    public Guid? ConnectionId { get; set; }
    public List<ColumnInfo> ColumnMetadata { get; set; } = [];

    public List<string> PrimaryKeyColumns => ColumnMetadata
        .Where(c => c.IsPrimaryKey)
        .Select(c => c.Name)
        .ToList();

    public bool HasPrimaryKey => PrimaryKeyColumns.Count > 0;
    public bool IsEditable => HasPrimaryKey && !string.IsNullOrEmpty(SourceTable);

    public ColumnInfo? GetColumnInfo(string columnName)
    {
        return ColumnMetadata.FirstOrDefault(c =>
            c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
    }

    public static EditableQueryResult FromQueryResult(
        QueryResult result,
        string? sourceTable = null,
        string? sourceSchema = null,
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
            SourceSchema = sourceSchema,
            SourceDatabase = sourceDatabase,
            ConnectionId = connectionId,
            ColumnMetadata = columnMetadata ?? []
        };
    }
}
