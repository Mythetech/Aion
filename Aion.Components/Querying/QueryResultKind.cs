namespace Aion.Components.Querying;

/// <summary>What a tab's latest run produced, which decides where the results panel opens.</summary>
public enum QueryResultKind
{
    /// <summary>The statement ran; its rows, row count or error are the result.</summary>
    Results,

    /// <summary>Only the planner's estimate; the statement was not run.</summary>
    EstimatedPlan,

    /// <summary>The actual plan, from a run whose changes were rolled back.</summary>
    ActualPlan
}

public static class QueryResultKindExtensions
{
    /// <summary>What a plan run did with the statement, or null for a run that returned results.</summary>
    public static string? PlanSummary(this QueryResultKind kind) => kind switch
    {
        QueryResultKind.EstimatedPlan => "Estimated plan, statement not run",
        QueryResultKind.ActualPlan => "Actual plan, changes rolled back",
        _ => null
    };
}
