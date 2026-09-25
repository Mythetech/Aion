namespace Aion.Components.Connections;

/// <summary>
/// Opens the host's flow for adding or editing a connection. The desktop host connects to database servers,
/// while the browser host can only create in-browser databases, so each host registers its own prompt.
/// </summary>
public interface IConnectionPrompt
{
    Task PromptAsync(ConnectionDialogModel? initialValues);
}
