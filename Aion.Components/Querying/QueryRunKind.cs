namespace Aion.Components.Querying;

/// <summary>What running a tab's SQL asks the engine for.</summary>
public enum QueryRunKind
{
    /// <summary>Runs the statement, with the plans the tab's toggles ask for.</summary>
    Execute,

    /// <summary>Only the planner's estimate; the statement is not run.</summary>
    Explain,

    /// <summary>
    /// Only the actual plan, captured by running the statement in a transaction that is rolled back,
    /// whatever the tab's plan toggles say.
    /// </summary>
    ExplainAnalyze
}
