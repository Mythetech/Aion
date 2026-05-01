using Aion.Contracts.Queries;
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;

namespace Aion.Components.Querying;

public class PlanNodeModel : NodeModel
{
    public PlanNodeModel(QueryPlanNode planNode, double relativeCost, Point position)
        : base(position)
    {
        PlanNode = planNode;
        RelativeCost = relativeCost;
    }

    public QueryPlanNode PlanNode { get; }
    public double RelativeCost { get; }

    public string CostLevel => RelativeCost switch
    {
        >= 0.4 => "hot",
        >= 0.1 => "warm",
        _ => "cool"
    };

    public bool HasRowDiscrepancy =>
        PlanNode.ActualRows.HasValue &&
        PlanNode.EstimatedRows > 0 &&
        (double)PlanNode.ActualRows.Value / PlanNode.EstimatedRows >= 10;
}
