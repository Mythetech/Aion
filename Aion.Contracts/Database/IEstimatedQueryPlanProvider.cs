using Aion.Contracts.Queries;

namespace Aion.Contracts.Database;

/// <summary>
/// Implemented by providers that can explain a statement without executing it. The UI hides the
/// Estimated Query Plan toggle for providers that don't implement it.
/// </summary>
public interface IEstimatedQueryPlanProvider
{
    /// <summary>
    /// Returns the planner's estimate for <paramref name="query"/> without running it.
    /// Throws when no plan can be produced, so an error is never mistaken for a plan.
    /// </summary>
    Task<QueryPlan> GetEstimatedPlanAsync(string connectionString, string query, CancellationToken cancellationToken);
}
