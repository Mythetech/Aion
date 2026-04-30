namespace Aion.Contracts.Database;

public class ForeignKeyInfo
{
    public string ConstraintName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string ReferencedTable { get; set; } = string.Empty;
    public string ReferencedColumn { get; set; } = string.Empty;
    public string? ReferencedSchema { get; set; }
}
