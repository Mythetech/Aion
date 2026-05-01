using Aion.Contracts.Queries;

namespace Aion.Contracts.Database;

public interface IQueryPlanParsingProvider
{
    QueryPlanTree? ParsePlan(QueryPlan plan) => null;
}
