namespace Aion.Contracts.Database;

public record IndexInfo(
    string Schema,
    string TableSchema,
    string TableName,
    string Name,
    bool IsUnique,
    bool IsPrimary,
    IReadOnlyList<string> Columns)
{
    public string DisplayName =>
        string.IsNullOrEmpty(TableSchema)
            ? $"{TableName}.{Name}"
            : $"{TableSchema}.{TableName}.{Name}";
}
