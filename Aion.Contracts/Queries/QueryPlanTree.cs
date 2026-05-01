namespace Aion.Contracts.Queries;

public class QueryPlanTree
{
    public required QueryPlanNode Root { get; init; }
    public double TotalCost { get; init; }
    public string SourceFormat { get; init; } = string.Empty;
    public string RawContent { get; init; } = string.Empty;
}

public class QueryPlanNode
{
    public string Operation { get; set; } = string.Empty;
    public string? Target { get; set; }
    public double StartupCost { get; set; }
    public double TotalCost { get; set; }
    public long EstimatedRows { get; set; }
    public long? ActualRows { get; set; }
    public double? ActualTime { get; set; }
    public int? Loops { get; set; }
    public string? Filter { get; set; }
    public string? JoinCondition { get; set; }
    public string? SortKey { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
    public List<QueryPlanNode> Children { get; set; } = [];
}
