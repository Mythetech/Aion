using Aion.Contracts.Queries;

namespace Aion.Contracts.Database;

/// <summary>
/// Implemented by providers that can capture a runtime plan without keeping the statement's side
/// effects. The UI hides the Actual Query Plan toggle for providers that don't implement it.
/// </summary>
public interface IActualQueryPlanProvider
{
    /// <summary>
    /// Executes <paramref name="query"/> inside a transaction that is always rolled back and returns
    /// the plan captured while it ran. Throws when the statement is refused or fails, so an error is
    /// never mistaken for a plan.
    /// </summary>
    Task<QueryPlan> GetActualPlanAsync(string connectionString, string query, CancellationToken cancellationToken);
}
