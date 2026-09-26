namespace Aion.Components.RequestContextPanel;

/// <summary>
/// Asks the side panel to show one of its views. The page keeps the latest request and passes it to the panel
/// as a parameter, so a request made while the panel is closed is applied when the panel first renders.
/// </summary>
/// <remarks>
/// A class rather than a record: asking for the same view twice is two requests, and the panel must switch
/// back to that view the second time even though nothing else differs.
/// </remarks>
public sealed class SidePanelRequest
{
    public const string InfoView = "Info";
    public const string JsonView = "JsonView";
    public const string ForeignKeyView = "ForeignKey";
    public const string TransactionsView = "Transactions";

    public required string View { get; init; }

    /// <summary>
    /// JSON text to show in the JSON view when it is opened by name rather than from a result cell.
    /// </summary>
    public string? JsonArgs { get; init; }

    public QueryResponseJsonDetail? JsonDetail { get; init; }

    public ForeignKeyDetail? ForeignKey { get; init; }
}
