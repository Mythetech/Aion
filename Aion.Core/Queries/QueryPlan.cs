namespace Aion.Core.Queries;

public class QueryPlan
{
    public string PlanType { get; set; } = string.Empty;
    public string PlanFormat { get; set; } = string.Empty; // e.g. "TEXT", "XML", "JSON"
    public string PlanContent { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.Now;

    public QueryPlan Clone()
    {
        return new QueryPlan
        {
            PlanType = PlanType,
            PlanFormat = PlanFormat,
            PlanContent = PlanContent,
            GeneratedAt = GeneratedAt
        };
    }
} 