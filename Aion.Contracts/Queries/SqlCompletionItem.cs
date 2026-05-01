namespace Aion.Contracts.Queries;

public enum SqlCompletionKind
{
    Keyword,
    Table,
    Column,
    Schema,
    Function,
    Procedure
}

public class SqlCompletionItem
{
    public string Label { get; set; } = string.Empty;
    public SqlCompletionKind Kind { get; set; }
    public string? Detail { get; set; }
    public string? InsertText { get; set; }
    public string? FilterText { get; set; }
    public string? SortText { get; set; }
}
